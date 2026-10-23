using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Linq;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.Runtime.InteropServices;
using Pinta.Core;
using Pintora.Avalonia.Document;
using Pintora.Avalonia.Imaging;
using Pintora.Avalonia.Input;

namespace Pintora.Avalonia.Canvas;

/// <summary>
/// Avalonia viewport for a document. Rendering is intentionally minimal until the shared
/// image renderer is separated from the GTK canvas.
/// </summary>
public sealed class CanvasHost : Control
{
	public enum CanvasTool
	{
		Brush,
		Eraser
	}

	private AvaloniaDocumentSession session;
	private readonly PintoraKeyboardSettings keyboard;
	private WriteableBitmap? bitmap;
	private bool is_painting;
	private PointD? last_canvas_point;
	private ColorBgra stroke_color;
	private Rect rendered_canvas_rect;
	private readonly Stack<ColorBgra[]> undo_stack = new ();
	private readonly Stack<ColorBgra[]> redo_stack = new ();

	public CanvasHost () : this (new Pinta.Core.Size (800, 600))
	{
	}

	public CanvasHost (Pinta.Core.Size imageSize) : this (imageSize, null)
	{
	}

	public CanvasHost (Pinta.Core.Size imageSize, IImageSurface? surface, PintoraKeyboardSettings? keyboard = null)
	{
		session = new AvaloniaDocumentSession (imageSize, surface);
		this.keyboard = keyboard ?? new PintoraKeyboardSettings ();
		ClipToBounds = true;
		Focusable = true;
		PointerWheelChanged += OnPointerWheelChanged;
		PointerPressed += OnPointerPressed;
		PointerReleased += OnPointerReleased;
		PointerMoved += OnPointerMoved;
		KeyDown += OnKeyDown;
	}

	public double Scale => session.Scale;
	public int BrushSize { get; set; } = 1;
	public double BrushOpacity { get; set; } = 1.0;
	public double BrushHardness { get; set; } = 1.0;
	public ColorBgra BrushColor { get; set; } = ColorBgra.Black;
	public ColorBgra SecondaryBrushColor { get; set; } = ColorBgra.White;
	public CanvasTool ActiveTool { get; set; } = CanvasTool.Brush;
	public Pinta.Core.Size ImageSize => session.ImageSize;
	public Pinta.Core.Size ViewSize => session.ViewSize;
	public IImageSurface? Surface => session.Surface;
	public IReadOnlyList<AvaloniaLayer> Layers => session.Layers;
	public int ActiveLayerIndex => session.ActiveLayerIndex;

	public void AddLayer ()
	{
		session.AddLayer ($"Layer {session.Layers.Count}");
		bitmap = null;
		InvalidateVisual ();
	}

	public void SelectLayer (int index)
	{
		session.SelectLayer (index);
		bitmap = null;
		InvalidateVisual ();
	}

	public void RemoveLayer ()
	{
		if (session.RemoveActiveLayer ()) {
			bitmap = null;
			InvalidateVisual ();
		}
	}

	public void MoveLayer (int direction)
	{
		if (session.MoveActiveLayer (direction)) {
			bitmap = null;
			InvalidateVisual ();
		}
	}

	public void RenameLayer (string name)
	{
		session.RenameActiveLayer (name);
		InvalidateVisual ();
	}

	public void SetScale (double scale)
	{
		session.SetScale (scale);
		InvalidateVisual ();
	}

	public void FitToWindow ()
	{
		double hostWidth = Bounds.Width > 0 ? Bounds.Width : Width;
		double hostHeight = Bounds.Height > 0 ? Bounds.Height : Height;
		if (hostWidth <= 0 || hostHeight <= 0)
			return;

		double scale = Math.Min (hostWidth / ImageSize.Width, hostHeight / ImageSize.Height);
		SetScale (Math.Clamp (scale, 0.1, 32));
	}

	public void NewDocument ()
	{
		if (Surface is null)
			return;

		Surface.GetPixelData ().Clear ();
		undo_stack.Clear ();
		redo_stack.Clear ();
		session.SetScale (1.0);
		bitmap = null;
		InvalidateVisual ();
	}

	public void OpenPng (string path)
	{
		using Bitmap image = new (path);
		MemoryImageSurface surface = DecodeSurface (image);
		session = new AvaloniaDocumentSession (new Pinta.Core.Size (surface.Width, surface.Height), surface);
		undo_stack.Clear ();
		redo_stack.Clear ();
		bitmap = null;
		InvalidateVisual ();
	}

	public void OpenOra (string path)
	{
		using var archive = new ZipArchive (File.OpenRead (path), ZipArchiveMode.Read);
		XElement image = XElement.Parse (new StreamReader (archive.GetEntry ("stack.xml")!.Open ()).ReadToEnd ());
		List<XElement> layerNodes = image.Element ("stack")?.Elements ("layer").Reverse ().ToList () ?? new List<XElement> ();
		if (layerNodes.Count == 0)
			throw new InvalidDataException ("The ORA file contains no layers.");

		MemoryImageSurface firstSurface = DecodeSurface (new Bitmap (archive.GetEntry (layerNodes[0].Attribute ("src")!.Value)!.Open ()));
		session = new AvaloniaDocumentSession (new Pinta.Core.Size (firstSurface.Width, firstSurface.Height), firstSurface);
		ApplyLayerMetadata (session.ActiveLayer!, layerNodes[0]);
		for (int i = 1; i < layerNodes.Count; i++) {
			XElement node = layerNodes[i];
			MemoryImageSurface source = DecodeSurface (new Bitmap (archive.GetEntry (node.Attribute ("src")!.Value)!.Open ()));
			AvaloniaLayer layer = session.AddLayer (node.Attribute ("name")?.Value ?? $"Layer {i}");
			source.GetReadOnlyPixelData ().CopyTo (layer.Surface.GetPixelData ());
			ApplyLayerMetadata (layer, node);
		}
		undo_stack.Clear ();
		redo_stack.Clear ();
		bitmap = null;
		InvalidateVisual ();
	}

	private static void ApplyLayerMetadata (AvaloniaLayer layer, XElement node)
	{
		layer.Name = node.Attribute ("name")?.Value ?? layer.Name;
		layer.IsVisible = node.Attribute ("visibility")?.Value != "hidden";
		if (double.TryParse (node.Attribute ("opacity")?.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double opacity))
			layer.Opacity = Math.Clamp (opacity, 0, 1);
	}

	private static MemoryImageSurface DecodeSurface (Bitmap image)
	{
		using (image) {
			PixelSize size = image.PixelSize;
			var bytes = new byte[size.Width * size.Height * ColorBgra.SizeOf];
			using var decoded = new WriteableBitmap (size, new Vector (96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
			using (ILockedFramebuffer locked = decoded.Lock ()) {
				image.CopyPixels (locked);
				for (int y = 0; y < size.Height; y++)
					Marshal.Copy (locked.Address + y * locked.RowBytes, bytes, y * size.Width * ColorBgra.SizeOf, size.Width * ColorBgra.SizeOf);
			}
			var surface = new MemoryImageSurface (size.Width, size.Height);
			MemoryMarshal.Cast<byte, ColorBgra> (bytes).CopyTo (surface.GetPixelData ());
			return surface;
		}
	}

	public void SavePng (string path)
	{
		SaveBitmap (path, PngBitmapEncoderOptions.Default);
	}

	public void SaveJpeg (string path, int quality = 95)
	{
		SaveBitmap (path, new JpegBitmapEncoderOptions { Quality = Math.Clamp (quality, 1, 100) });
	}

	private void SaveBitmap (string path, BitmapEncoderOptions options)
	{
		if (Surface is null)
			throw new InvalidOperationException ("A document surface is required to save the image.");

		using MemoryImageSurface composite = CreateCompositeSurface ();
		WriteableBitmap image = CreateBitmap (composite);
		using FileStream stream = File.Create (path);
		image.Save (stream, options);
	}

	public void SaveOra (string path)
	{
		if (Surface is null)
			throw new InvalidOperationException ("A document surface is required to save the image.");

		using var archive = new ZipArchive (File.Create (path), ZipArchiveMode.Create);
		WriteTextEntry (archive, "mimetype", "image/openraster");
		WritePngEntry (archive, "mergedimage.png", CreateCompositeSurface ());
		var xml = new StringBuilder ($"<image version=\"0.0.1\" w=\"{ImageSize.Width}\" h=\"{ImageSize.Height}\"><stack>");
		for (int i = Layers.Count - 1; i >= 0; i--) {
			AvaloniaLayer layer = Layers[i];
			string source = $"data/layer{i}.png";
			WritePngEntry (archive, source, layer.Surface);
			xml.Append ($"<layer name=\"{System.Security.SecurityElement.Escape (layer.Name)}\" src=\"{source}\" opacity=\"{layer.Opacity.ToString (System.Globalization.CultureInfo.InvariantCulture)}\" visibility=\"{(layer.IsVisible ? "visible" : "hidden")}\" />");
		}
		xml.Append ("</stack></image>");
		WriteTextEntry (archive, "stack.xml", xml.ToString ());
	}

	private static void WriteTextEntry (ZipArchive archive, string name, string text)
	{
		using Stream stream = archive.CreateEntry (name).Open ();
		using var writer = new StreamWriter (stream, Encoding.UTF8);
		writer.Write (text);
	}

	private static void WritePngEntry (ZipArchive archive, string name, IImageSurface surface)
	{
		using Stream stream = archive.CreateEntry (name).Open ();
		using WriteableBitmap image = CreateBitmap (surface);
		image.Save (stream, PngBitmapEncoderOptions.Default);
	}

	public void PaintCanvasPoint (PointD canvasPoint, ColorBgra color)
	{
		if (Surface is null)
			return;

		int radius = Math.Max (BrushSize - 1, 0) / 2;
		Span<ColorBgra> pixels = Surface.GetPixelData ();
		int centerX = (int) Math.Round (canvasPoint.X);
		int centerY = (int) Math.Round (canvasPoint.Y);
		if (color.A > 0 && BrushOpacity < 1)
			color = ColorBgra.FromBgra (color.B, color.G, color.R, (byte) Math.Round (color.A * Math.Clamp (BrushOpacity, 0, 1)));
		for (int y = centerY - radius; y <= centerY + radius; y++)
			for (int x = centerX - radius; x <= centerX + radius; x++)
				if ((uint) x < Surface.Width && (uint) y < Surface.Height)
					pixels[y * Surface.Width + x] = color;

		bitmap = null;
		InvalidateVisual ();
	}

	public void PaintCanvasLine (PointD from, PointD to, ColorBgra color)
	{
		double distance = Math.Sqrt (Math.Pow (to.X - from.X, 2) + Math.Pow (to.Y - from.Y, 2));
		int steps = Math.Max ((int) Math.Ceiling (distance), 1);

		for (int i = 0; i <= steps; i++) {
			double progress = i / (double) steps;
			PaintCanvasPoint (new PointD (
				from.X + (to.X - from.X) * progress,
				from.Y + (to.Y - from.Y) * progress), color);
		}
	}

	public bool CanUndoStroke => undo_stack.Count > 0 && Surface is not null;
	public bool CanRedoStroke => redo_stack.Count > 0 && Surface is not null;

	public void BeginStroke ()
	{
		if (Surface is null)
			return;

		undo_stack.Push (Surface.GetReadOnlyPixelData ().ToArray ());
		redo_stack.Clear ();
	}

	public void UndoLastStroke ()
	{
		if (!CanUndoStroke)
			return;

		redo_stack.Push (Surface!.GetReadOnlyPixelData ().ToArray ());
		undo_stack.Pop ().AsSpan ().CopyTo (Surface.GetPixelData ());
		bitmap = null;
		InvalidateVisual ();
	}

	public void RedoLastStroke ()
	{
		if (!CanRedoStroke)
			return;

		undo_stack.Push (Surface!.GetReadOnlyPixelData ().ToArray ());
		redo_stack.Pop ().AsSpan ().CopyTo (Surface.GetPixelData ());
		bitmap = null;
		InvalidateVisual ();
	}

	public void AdjustBrushSize (int delta)
	{
		BrushSize = Math.Clamp (BrushSize + delta, 1, 1000);
	}

	public void PaintViewPoint (Point viewPoint, ColorBgra color)
	{
		Rect canvasRect = GetCanvasRect ();
		if (!canvasRect.Contains (viewPoint))
			return;

		PointD canvasPoint = session.Viewport.ViewPointToCanvas (new PointD (viewPoint.X - canvasRect.X, viewPoint.Y - canvasRect.Y));
		PaintCanvasPoint (canvasPoint, color);
	}

	public override void Render (DrawingContext context)
	{
		base.Render (context);

		context.FillRectangle (Brushes.LightGray, new Rect (Bounds.Size));

		Pinta.Core.Size viewSize = session.ViewSize;
		Point origin = GetCanvasOrigin ();
		rendered_canvas_rect = new Rect (origin, new global::Avalonia.Size (viewSize.Width, viewSize.Height));

		if (Surface is not null)
			DrawSurface (context, origin, CreateCompositeSurface ());
		else
			context.FillRectangle (Brushes.White, new Rect (origin, new global::Avalonia.Size (viewSize.Width, viewSize.Height)));
		context.DrawRectangle (new Pen (Brushes.Gray, 1), new Rect (origin, new global::Avalonia.Size (viewSize.Width, viewSize.Height)));

		var text = new FormattedText (
			$"Canvas {session.ImageSize.Width} × {session.ImageSize.Height}\n{Scale:P0}",
			System.Globalization.CultureInfo.InvariantCulture,
			FlowDirection.LeftToRight,
			Typeface.Default,
			14,
			Brushes.Gray);
		context.DrawText (text, origin + new Vector (16, 16));
	}

	private MemoryImageSurface CreateCompositeSurface ()
	{
		var composite = new MemoryImageSurface (ImageSize.Width, ImageSize.Height);
		Span<ColorBgra> destination = composite.GetPixelData ();
		foreach (AvaloniaLayer layer in Layers) {
			if (!layer.IsVisible || layer.Opacity <= 0)
				continue;
			ReadOnlySpan<ColorBgra> source = layer.Surface.GetReadOnlyPixelData ();
			int opacity = (int) Math.Round (Math.Clamp (layer.Opacity, 0, 1) * 255);
			for (int i = 0; i < destination.Length; i++) {
				ColorBgra top = source[i];
				int sourceAlpha = top.A * opacity / 255;
				int destinationAlpha = destination[i].A;
				int outputAlpha = sourceAlpha + destinationAlpha * (255 - sourceAlpha) / 255;
				if (outputAlpha == 0)
					continue;
				destination[i] = ColorBgra.FromBgra (
					(byte) ((top.B * sourceAlpha + destination[i].B * destinationAlpha * (255 - sourceAlpha) / 255) / outputAlpha),
					(byte) ((top.G * sourceAlpha + destination[i].G * destinationAlpha * (255 - sourceAlpha) / 255) / outputAlpha),
					(byte) ((top.R * sourceAlpha + destination[i].R * destinationAlpha * (255 - sourceAlpha) / 255) / outputAlpha),
					(byte) outputAlpha);
			}
		}
		return composite;
	}

	private void DrawSurface (DrawingContext context, Point origin, IImageSurface displaySurface)
	{
		IImageSurface surface = displaySurface;
		bitmap = CreateBitmap (surface);

		context.DrawImage (bitmap, new Rect (0, 0, surface.Width, surface.Height), new Rect (origin, new global::Avalonia.Size (ViewSize.Width, ViewSize.Height)));
	}

	private static WriteableBitmap CreateBitmap (IImageSurface surface)
	{
		var image = new WriteableBitmap (new PixelSize (surface.Width, surface.Height), new Vector (96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
		using ILockedFramebuffer locked = image.Lock ();
		ReadOnlySpan<byte> source = MemoryMarshal.AsBytes (surface.GetReadOnlyPixelData ());
		for (int y = 0; y < surface.Height; y++) {
			ReadOnlySpan<byte> sourceRow = source.Slice (y * surface.Width * ColorBgra.SizeOf, surface.Width * ColorBgra.SizeOf);
			Marshal.Copy (sourceRow.ToArray (), 0, locked.Address + y * locked.RowBytes, sourceRow.Length);
		}
		return image;
	}

	private void OnPointerWheelChanged (object? sender, PointerWheelEventArgs e)
	{
		double factor = e.Delta.Y > 0 ? 1.1 : 1 / 1.1;
		SetScale (Math.Clamp (Scale * factor, 0.1, 32));
		e.Handled = true;
	}

	private void OnPointerPressed (object? sender, PointerPressedEventArgs e)
	{
		var properties = e.GetCurrentPoint (this).Properties;
		if (!properties.IsLeftButtonPressed && !properties.IsRightButtonPressed)
			return;

		is_painting = true;
		BeginStroke ();
		stroke_color = properties.IsRightButtonPressed
			? SecondaryBrushColor
			: ActiveTool == CanvasTool.Eraser ? ColorBgra.Zero : BrushColor;
		e.Pointer.Capture (this);
		Point position = e.GetPosition (this);
		Point origin = GetCanvasOrigin ();
		last_canvas_point = session.Viewport.ViewPointToCanvas (new PointD (position.X - origin.X, position.Y - origin.Y));
		PaintCanvasPoint (last_canvas_point.Value, stroke_color);
		e.Handled = true;
	}

	private void OnPointerReleased (object? sender, PointerReleasedEventArgs e)
	{
		is_painting = false;
		last_canvas_point = null;
		e.Pointer.Capture (null);
	}

	private void OnPointerMoved (object? sender, PointerEventArgs e)
	{
		if (!is_painting)
			return;

		Point position = e.GetPosition (this);
		Point origin = GetCanvasOrigin ();
		PointD current = session.Viewport.ViewPointToCanvas (new PointD (position.X - origin.X, position.Y - origin.Y));
		if (last_canvas_point is PointD previous)
			PaintCanvasLine (previous, current, stroke_color);
		else
			PaintCanvasPoint (current, stroke_color);
		last_canvas_point = current;
		e.Handled = true;
	}

	private void OnKeyDown (object? sender, KeyEventArgs e)
	{
		if (keyboard.Matches (e, PintoraKeyCommands.Undo)) {
			UndoLastStroke ();
			e.Handled = true;
		} else if (keyboard.Matches (e, PintoraKeyCommands.Redo)) {
			RedoLastStroke ();
			e.Handled = true;
		} else if (keyboard.Matches (e, PintoraKeyCommands.DecreaseBrush)) {
			AdjustBrushSize (-1);
			e.Handled = true;
		} else if (keyboard.Matches (e, PintoraKeyCommands.IncreaseBrush)) {
			AdjustBrushSize (1);
			e.Handled = true;
		} else if (keyboard.Matches (e, PintoraKeyCommands.ToggleEraser)) {
			ActiveTool = ActiveTool == CanvasTool.Eraser ? CanvasTool.Brush : CanvasTool.Eraser;
			e.Handled = true;
		}
	}

	private Rect GetCanvasRect ()
	{
		if (rendered_canvas_rect.Width > 0 && rendered_canvas_rect.Height > 0)
			return rendered_canvas_rect;

		Pinta.Core.Size viewSize = ViewSize;
		return new Rect (GetCanvasOrigin (), new global::Avalonia.Size (viewSize.Width, viewSize.Height));
	}

	private Point GetCanvasOrigin ()
	{
		Pinta.Core.Size viewSize = ViewSize;
		double hostWidth = Bounds.Width > 0 ? Bounds.Width : Width;
		double hostHeight = Bounds.Height > 0 ? Bounds.Height : Height;
		return new Point (
			Math.Max ((hostWidth - viewSize.Width) / 2, 0),
			Math.Max ((hostHeight - viewSize.Height) / 2, 0));
	}
}
