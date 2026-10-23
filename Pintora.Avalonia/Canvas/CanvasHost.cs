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
		Pencil,
		Line,
		Rectangle,
		Ellipse,
		Fill,
		ColorPicker,
		Zoom,
		Eraser
	}

	private AvaloniaDocumentSession session;
	private readonly PintoraKeyboardSettings keyboard;
	private WriteableBitmap? bitmap;
	private bool is_painting;
	private PointD? last_canvas_point;
	private PointD? stroke_start_point;
	private PointD? line_preview_point;
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
	public int FillTolerance { get; set; } = 0;
	public bool ShapeFilled { get; set; }
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

		Surface.GetPixelData ().Fill (ColorBgra.White);
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

	private void PaintPencilLine (PointD from, PointD to, ColorBgra color)
	{
		double distance = Math.Sqrt (Math.Pow (to.X - from.X, 2) + Math.Pow (to.Y - from.Y, 2));
		int steps = Math.Max ((int) Math.Ceiling (distance), 1);
		Span<ColorBgra> pixels = Surface!.GetPixelData ();
		for (int i = 0; i <= steps; i++) {
			double progress = i / (double) steps;
			int x = (int) Math.Round (from.X + (to.X - from.X) * progress);
			int y = (int) Math.Round (from.Y + (to.Y - from.Y) * progress);
			if ((uint) x < Surface.Width && (uint) y < Surface.Height)
				pixels[y * Surface.Width + x] = color;
		}
		bitmap = null;
		InvalidateVisual ();
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

		context.FillRectangle (Brushes.White, new Rect (Bounds.Size));

		Pinta.Core.Size viewSize = session.ViewSize;
		Point origin = GetCanvasOrigin ();
		rendered_canvas_rect = new Rect (origin, new global::Avalonia.Size (viewSize.Width, viewSize.Height));
		DrawTransparencyGrid (context, rendered_canvas_rect);

		if (Surface is not null)
			DrawSurface (context, origin, CreateCompositeSurface ());
		else
			context.FillRectangle (Brushes.White, new Rect (origin, new global::Avalonia.Size (viewSize.Width, viewSize.Height)));
		context.DrawRectangle (new Pen (Brushes.Gray, 1), new Rect (origin, new global::Avalonia.Size (viewSize.Width, viewSize.Height)));
		if ((ActiveTool == CanvasTool.Line || ActiveTool == CanvasTool.Rectangle) && is_painting && stroke_start_point is PointD start && line_preview_point is PointD preview) {
			var preview_pen = new Pen (new SolidColorBrush (ToAvaloniaColor (stroke_color)), Math.Max (1, BrushSize * Scale));
			Point first = origin + new Vector (start.X * Scale, start.Y * Scale);
			Point last = origin + new Vector (preview.X * Scale, preview.Y * Scale);
			if (ActiveTool == CanvasTool.Line)
				context.DrawLine (preview_pen, first, last);
			else if (ActiveTool == CanvasTool.Rectangle)
				context.DrawRectangle (preview_pen, new Rect (Math.Min (first.X, last.X), Math.Min (first.Y, last.Y), Math.Abs (last.X - first.X), Math.Abs (last.Y - first.Y)));
			else
				context.DrawEllipse (null, preview_pen, new Rect (Math.Min (first.X, last.X), Math.Min (first.Y, last.Y), Math.Abs (last.X - first.X), Math.Abs (last.Y - first.Y)));
		}

		var text = new FormattedText (
			$"Canvas {session.ImageSize.Width} × {session.ImageSize.Height}\n{Scale:P0}",
			System.Globalization.CultureInfo.InvariantCulture,
			FlowDirection.LeftToRight,
			Typeface.Default,
			14,
			Brushes.Gray);
		context.DrawText (text, origin + new Vector (16, 16));
	}

	private static void DrawTransparencyGrid (DrawingContext context, Rect canvasRect)
	{
		const double tileSize = 16;
		IBrush light = new SolidColorBrush (Color.Parse ("#FFFFFF"));
		IBrush dark = new SolidColorBrush (Color.Parse ("#E5E7EB"));
		int columns = (int) Math.Ceiling (canvasRect.Width / tileSize);
		int rows = (int) Math.Ceiling (canvasRect.Height / tileSize);
		for (int y = 0; y < rows; y++)
			for (int x = 0; x < columns; x++)
			{
				double tileX = canvasRect.X + x * tileSize;
				double tileY = canvasRect.Y + y * tileSize;
				double width = Math.Min (tileSize, canvasRect.Right - tileX);
				double height = Math.Min (tileSize, canvasRect.Bottom - tileY);
				if (width <= 0 || height <= 0)
					continue;
				var tile = new Rect (tileX, tileY, width, height);
				context.FillRectangle ((x + y) % 2 == 0 ? light : dark, tile);
			}
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

		stroke_color = properties.IsRightButtonPressed
			? SecondaryBrushColor
			: ActiveTool == CanvasTool.Eraser ? ColorBgra.Zero : BrushColor;
		e.Pointer.Capture (this);
		Point position = e.GetPosition (this);
		Point origin = GetCanvasOrigin ();
		last_canvas_point = session.Viewport.ViewPointToCanvas (new PointD (position.X - origin.X, position.Y - origin.Y));
		if (ActiveTool == CanvasTool.Zoom) {
			SetScale (Math.Clamp (Scale * (properties.IsRightButtonPressed ? 1 / 1.25 : 1.25), 0.1, 32));
			is_painting = false;
			e.Pointer.Capture (null);
			e.Handled = true;
			return;
		}
		if (ActiveTool == CanvasTool.ColorPicker) {
			PickColor (last_canvas_point.Value);
			return;
		}
		if (ActiveTool == CanvasTool.Fill) {
			BeginStroke ();
			FillCanvas (last_canvas_point.Value, stroke_color);
			return;
		}
		is_painting = true;
		BeginStroke ();
		stroke_start_point = last_canvas_point;
		line_preview_point = last_canvas_point;
		if (ActiveTool != CanvasTool.Line && ActiveTool != CanvasTool.Rectangle && ActiveTool != CanvasTool.Ellipse)
			if (ActiveTool == CanvasTool.Pencil)
				PaintPencilLine (last_canvas_point.Value, last_canvas_point.Value, stroke_color);
			else
				PaintCanvasPoint (last_canvas_point.Value, stroke_color);
		e.Handled = true;
	}

	private void PickColor (PointD point)
	{
		if (Surface is null)
			return;
		int x = (int) Math.Round (point.X);
		int y = (int) Math.Round (point.Y);
		if ((uint) x < Surface.Width && (uint) y < Surface.Height)
			BrushColor = Surface.GetReadOnlyPixelData ()[y * Surface.Width + x];
	}

	private void FillCanvas (PointD point, ColorBgra color)
	{
		if (Surface is null)
			return;
		int startX = (int) Math.Round (point.X);
		int startY = (int) Math.Round (point.Y);
		if ((uint) startX >= Surface.Width || (uint) startY >= Surface.Height)
			return;
		Span<ColorBgra> pixels = Surface.GetPixelData ();
		ColorBgra target = pixels[startY * Surface.Width + startX];
		if (target == color)
			return;
		var queue = new Queue<(int X, int Y)> ();
		queue.Enqueue ((startX, startY));
		while (queue.Count > 0) {
			(int x, int y) = queue.Dequeue ();
			if ((uint) x >= Surface.Width || (uint) y >= Surface.Height || !MatchesFillColor (pixels[y * Surface.Width + x], target))
				continue;
			pixels[y * Surface.Width + x] = color;
			queue.Enqueue ((x + 1, y));
			queue.Enqueue ((x - 1, y));
			queue.Enqueue ((x, y + 1));
			queue.Enqueue ((x, y - 1));
		}
		bitmap = null;
		InvalidateVisual ();
	}

	private bool MatchesFillColor (ColorBgra actual, ColorBgra target)
	{
		int difference = Math.Abs (actual.R - target.R) + Math.Abs (actual.G - target.G) + Math.Abs (actual.B - target.B) + Math.Abs (actual.A - target.A);
		return difference <= Math.Clamp (FillTolerance, 0, 255) * 4;
	}

	private void OnPointerReleased (object? sender, PointerReleasedEventArgs e)
	{
		if (is_painting && (ActiveTool == CanvasTool.Line || ActiveTool == CanvasTool.Rectangle || ActiveTool == CanvasTool.Ellipse) && stroke_start_point is PointD start) {
			Point position = e.GetPosition (this);
			Point origin = GetCanvasOrigin ();
			PointD end = session.Viewport.ViewPointToCanvas (new PointD (position.X - origin.X, position.Y - origin.Y));
			if (ActiveTool == CanvasTool.Line)
				PaintCanvasLine (start, end, stroke_color);
			else if (ActiveTool == CanvasTool.Rectangle)
				PaintCanvasRectangle (start, end, stroke_color);
			else
				PaintCanvasEllipse (start, end, stroke_color);
		}
		is_painting = false;
		last_canvas_point = null;
		stroke_start_point = null;
		line_preview_point = null;
		InvalidateVisual ();
		e.Pointer.Capture (null);
	}

	private void OnPointerMoved (object? sender, PointerEventArgs e)
	{
		if (!is_painting)
			return;

		Point position = e.GetPosition (this);
		Point origin = GetCanvasOrigin ();
		PointD current = session.Viewport.ViewPointToCanvas (new PointD (position.X - origin.X, position.Y - origin.Y));
		if (ActiveTool == CanvasTool.Line || ActiveTool == CanvasTool.Rectangle || ActiveTool == CanvasTool.Ellipse) {
			line_preview_point = current;
			InvalidateVisual ();
			return;
		}
		if (last_canvas_point is PointD previous)
			if (ActiveTool == CanvasTool.Pencil)
				PaintPencilLine (previous, current, stroke_color);
			else
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

	private static global::Avalonia.Media.Color ToAvaloniaColor (ColorBgra color)
		=> global::Avalonia.Media.Color.FromArgb (color.A, color.R, color.G, color.B);

	private void PaintCanvasRectangle (PointD from, PointD to, ColorBgra color)
	{
		PointD topLeft = new (Math.Min (from.X, to.X), Math.Min (from.Y, to.Y));
		PointD bottomRight = new (Math.Max (from.X, to.X), Math.Max (from.Y, to.Y));
		if (ShapeFilled && Surface is not null) {
			for (int y = (int) topLeft.Y; y <= (int) bottomRight.Y; y++)
				PaintCanvasLine (new PointD (topLeft.X, y), new PointD (bottomRight.X, y), color);
			return;
		}
		PaintCanvasLine (topLeft, new PointD (bottomRight.X, topLeft.Y), color);
		PaintCanvasLine (new PointD (bottomRight.X, topLeft.Y), bottomRight, color);
		PaintCanvasLine (bottomRight, new PointD (topLeft.X, bottomRight.Y), color);
		PaintCanvasLine (new PointD (topLeft.X, bottomRight.Y), topLeft, color);
	}

	private void PaintCanvasEllipse (PointD from, PointD to, ColorBgra color)
	{
		PointD center = new ((from.X + to.X) / 2, (from.Y + to.Y) / 2);
		double radiusX = Math.Abs (to.X - from.X) / 2;
		double radiusY = Math.Abs (to.Y - from.Y) / 2;
		if (radiusX == 0 || radiusY == 0) {
			PaintCanvasLine (from, to, color);
			return;
		}
		int steps = Math.Max (64, (int) Math.Ceiling (2 * Math.PI * Math.Max (radiusX, radiusY)));
		for (int i = 0; i <= steps; i++) {
			double angle = 2 * Math.PI * i / steps;
			PointD point = new (center.X + radiusX * Math.Cos (angle), center.Y + radiusY * Math.Sin (angle));
			if (ShapeFilled) {
				PaintCanvasLine (center, point, color);
			} else if (i > 0) {
				double previousAngle = 2 * Math.PI * (i - 1) / steps;
				PaintCanvasLine (new PointD (center.X + radiusX * Math.Cos (previousAngle), center.Y + radiusY * Math.Sin (previousAngle)), point, color);
			}
		}
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
