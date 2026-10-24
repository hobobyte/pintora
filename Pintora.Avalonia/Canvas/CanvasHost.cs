using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Pinta.Core;
using Pintora.Avalonia.Document;
using Pintora.Avalonia.Imaging;
using Pintora.Avalonia.Input;
using SkiaSharp;

namespace Pintora.Avalonia.Canvas;

/// <summary>
/// Avalonia viewport for a document. Rendering is intentionally minimal until the shared
/// image renderer is separated from the GTK canvas.
/// </summary>
public sealed class CanvasHost : Control
{
	public enum CanvasTool
	{
		Selection,
		Brush,
		Pencil,
		Shapes,
		Gradient,
		Fill,
		ColorPicker,
		Zoom,
		Eraser
	}

	public enum ShapeKind { Line, Rectangle, Ellipse, RoundedRectangle }
	public enum ShapeFillStyle { Outline, Fill, FillAndOutline }
	public enum ShapeDashStyle { Solid, Dash, Dot, DashDot }

	private AvaloniaDocumentSession session;
	private readonly CanvasToolSet tool_set = CanvasToolFactory.CreateTools ();
	private ShapeTool shape_tool => tool_set.Get<ShapeTool> (CanvasTool.Shapes);
	private GradientTool gradient_tool => tool_set.Get<GradientTool> (CanvasTool.Gradient);
	private FillTool fill_tool => tool_set.Get<FillTool> (CanvasTool.Fill);
	private SelectionTool selection_tool => tool_set.Get<SelectionTool> (CanvasTool.Selection);
	private ColorPickerTool color_picker_tool => tool_set.Get<ColorPickerTool> (CanvasTool.ColorPicker);
	private ZoomTool zoom_tool => tool_set.Get<ZoomTool> (CanvasTool.Zoom);
	private BrushTool brush_tool => tool_set.Brush (CanvasTool.Brush);
	private BrushTool pencil_tool => tool_set.Brush (CanvasTool.Pencil);
	private BrushTool eraser_tool => tool_set.Brush (CanvasTool.Eraser);
	private readonly PintoraKeyboardSettings keyboard;
	private WriteableBitmap? bitmap;
	private bool is_painting;
	private CanvasTool? pointer_action_tool;
	private bool has_current_stroke;
	private PointD? last_canvas_point;
	private Point? pan_start_view_point;
	private Vector pan_start_offset;
	private ColorBgra stroke_color;
	private CanvasTool active_tool = CanvasTool.Brush;
	private ShapeKind shape_kind = ShapeKind.Rectangle;
	private Rect rendered_canvas_rect;
	private Vector viewport_offset;
	private readonly Stack<RasterSnapshot> undo_stack = new ();
	private readonly Stack<RasterSnapshot> redo_stack = new ();
	private readonly Stack<string> history_stack = new ();
	private readonly Stack<string> redo_history_stack = new ();
	private readonly Stack<LayerStackSnapshot> layer_undo_stack = new ();
	private readonly Stack<LayerStackSnapshot> layer_redo_stack = new ();
	private sealed record RasterSnapshot (AvaloniaLayer Layer, ColorBgra[] Pixels, ShapeTool.ShapeSnapshot? Shape);
	private sealed record LayerSnapshot (AvaloniaLayer Layer, string Name, bool IsVisible, bool IsLocked, double Opacity, ColorBgra[] Pixels);
	private sealed record LayerStackSnapshot (List<LayerSnapshot> Layers, int ActiveIndex);

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
	internal void InvalidateToolVisual () => InvalidateVisual ();
	internal void MarkCanvasChanged ()
	{
		bitmap = null;
		InvalidateVisual ();
	}
	public event EventHandler? ScaleChanged;
	private int brush_size = 1;
	public int BrushSize {
		get => brush_size;
		set {
			int next = Math.Clamp (value, 1, 256);
			if (brush_size == next)
				return;
			brush_size = next;
			BrushSizeChanged?.Invoke (this, EventArgs.Empty);
		}
	}
	public event EventHandler? BrushSizeChanged;
	public event EventHandler? ColorsChanged;
	private double brush_opacity = 1.0;
	public double BrushOpacity {
		get => brush_opacity;
		set => brush_opacity = Math.Clamp (value, 0, 1);
	}
	private double brush_hardness = 1.0;
	public double BrushHardness {
		get => brush_hardness;
		set => brush_hardness = Math.Clamp (value, 0, 1);
	}
	public ColorBgra BrushColor { get; set; } = ColorBgra.Black;
	public ColorBgra SecondaryBrushColor { get; set; } = ColorBgra.White;
	private int fill_tolerance;
	public int FillTolerance {
		get => fill_tolerance;
		set => fill_tolerance = Math.Clamp (value, 0, 255);
	}
	private ShapeFillStyle shape_fill_style;
	public ShapeFillStyle FillStyle {
		get => shape_fill_style;
		set {
			if (!Enum.IsDefined (value)) throw new ArgumentOutOfRangeException (nameof (value));
			if (shape_fill_style == value) return;
			shape_fill_style = value;
			UpdateSelectedShapeOptions ();
			InvalidateVisual ();
		}
	}
	public bool ShapeFilled {
		get => FillStyle != ShapeFillStyle.Outline;
		set => FillStyle = value ? ShapeFillStyle.Fill : ShapeFillStyle.Outline;
	}
	private int shape_outline_width = 2;
	public int ShapeOutlineWidth {
		get => shape_outline_width;
		set {
			int next = Math.Clamp (value, 1, 256);
			if (shape_outline_width == next) return;
			shape_outline_width = next;
			UpdateSelectedShapeOptions ();
			InvalidateVisual ();
		}
	}
	public ShapeDashStyle DashStyle {
		get;
		set {
			if (!Enum.IsDefined (value)) throw new ArgumentOutOfRangeException (nameof (value));
			if (field == value) return;
			field = value;
			UpdateSelectedShapeOptions ();
			InvalidateVisual ();
		}
	}
	private int shape_corner_radius = 8;
	public int ShapeCornerRadius {
		get => shape_corner_radius;
		set {
			int next = Math.Clamp (value, 1, 256);
			if (shape_corner_radius == next) return;
			shape_corner_radius = next;
			UpdateSelectedShapeOptions ();
			InvalidateVisual ();
		}
	}
	private bool shape_antialias = true;
	public bool ShapeAntialias {
		get => shape_antialias;
		set {
			if (shape_antialias == value) return;
			shape_antialias = value;
			UpdateSelectedShapeOptions ();
			InvalidateVisual ();
		}
	}
	public ShapeKind SelectedShape {
		get => shape_kind;
		set {
			if (!Enum.IsDefined (value))
				throw new ArgumentOutOfRangeException (nameof (value));
			if (shape_kind == value)
				return;
			shape_kind = value;
			UpdateSelectedShapeOptions ();
			SelectedShapeChanged?.Invoke (this, EventArgs.Empty);
			InvalidateVisual ();
		}
	}
	public event EventHandler? SelectedShapeChanged;
	private void UpdateSelectedShapeOptions ()
	{
		if (shape_tool.UpdateSelectedOptions (session.ActiveLayer, Surface as MemoryImageSurface,
			shape_kind, shape_fill_style, shape_outline_width, DashStyle, shape_corner_radius,
			shape_antialias, BeginStroke, CommitStroke))
			bitmap = null;
	}
	public bool GradientReversed { get; set; }
	public CanvasTool ActiveTool {
		get => active_tool;
		set {
			if (active_tool == value)
				return;
			if (active_tool == CanvasTool.Shapes && value != CanvasTool.Shapes && !shape_tool.IsEditing)
				shape_tool.ClearSelectedShape ();
			active_tool = value;
			ActiveToolChanged?.Invoke (this, EventArgs.Empty);
			InvalidateVisual ();
		}
	}
	public event EventHandler? ActiveToolChanged;
	public Pinta.Core.Size ImageSize => session.ImageSize;
	public Pinta.Core.Size ViewSize => session.ViewSize;
	public IImageSurface? Surface => session.Surface;
	public bool IsActiveLayerLocked => session.ActiveLayer?.IsLocked == true;
	internal AvaloniaLayer? ActiveLayer => session.ActiveLayer;
	public IReadOnlyList<AvaloniaLayer> Layers => session.Layers;
	public int ActiveLayerIndex => session.ActiveLayerIndex;
	public RectangleI? Selection => selection_tool.Selection;

	public bool SelectRectangle (PointI start, PointI end)
	{
		bool selected = selection_tool.SelectRectangle (start, end, ImageSize);
		InvalidateVisual ();
		return selected;
	}

	public bool SelectAll ()
		=> SelectRectangle (new PointI (0, 0), new PointI (ImageSize.Width, ImageSize.Height));

	public void ClearSelection ()
	{
		selection_tool.Clear ();
		InvalidateVisual ();
	}

	public bool CopySelection ()
	{
		return selection_tool.Copy (Surface);
	}

	public bool PasteSelection (PointI destination)
	{
		if (!selection_tool.Paste (destination, name => session.AddLayer ($"{name} {Layers.Count}")))
			return false;
		bitmap = null;
		InvalidateVisual ();
		return true;
	}

	public bool PasteSelection ()
	{
		PointI destination = Selection is RectangleI area ? new PointI (area.X, area.Y) : new PointI (0, 0);
		return PasteSelection (destination);
	}

	public bool MoveSelection (PointI delta)
	{
		if (IsActiveLayerLocked || !selection_tool.Move (Surface, delta, BeginStroke, CommitStroke))
			return false;
		bitmap = null;
		InvalidateVisual ();
		return true;
	}

	public bool FlipSelectionHorizontal ()
	{
		if (IsActiveLayerLocked || !selection_tool.FlipHorizontal (Surface, BeginStroke, CommitStroke))
			return false;
		bitmap = null;
		InvalidateVisual ();
		return true;
	}

	public bool FlipSelectionVertical ()
	{
		if (IsActiveLayerLocked || !selection_tool.FlipVertical (Surface, BeginStroke, CommitStroke))
			return false;
		bitmap = null;
		InvalidateVisual ();
		return true;
	}

	public bool RotateSelectionClockwise ()
	{
		if (IsActiveLayerLocked || !selection_tool.RotateClockwise (Surface, BeginStroke, CommitStroke))
			return false;
		bitmap = null;
		InvalidateVisual ();
		return true;
	}

	public void SetActiveLayerOpacity (double opacity)
	{
		if (session.ActiveLayer is null)
			return;
		double clamped = Math.Clamp (opacity, 0, 1);
		if (session.ActiveLayer.Opacity == clamped) return;
		layer_undo_stack.Push (CaptureLayerStack ());
		layer_redo_stack.Clear ();
		session.ActiveLayer.Opacity = clamped;
		bitmap = null;
		InvalidateVisual ();
	}

	public void SwapColors ()
	{
		(BrushColor, SecondaryBrushColor) = (SecondaryBrushColor, BrushColor);
		ColorsChanged?.Invoke (this, EventArgs.Empty);
	}

	public void SetActiveLayerVisibility (bool isVisible)
	{
		SetLayerVisibility (ActiveLayerIndex, isVisible);
	}

	public void SetLayerVisibility (int index, bool isVisible)
	{
		if ((uint) index >= Layers.Count)
			return;
		if (Layers[index].IsVisible == isVisible) return;
		layer_undo_stack.Push (CaptureLayerStack ());
		layer_redo_stack.Clear ();
		Layers[index].IsVisible = isVisible;
		bitmap = null;
		InvalidateVisual ();
	}

	public void SetLayerLocked (int index, bool isLocked)
	{
		if ((uint) index >= Layers.Count || Layers[index].IsLocked == isLocked)
			return;
		layer_undo_stack.Push (CaptureLayerStack ());
		layer_redo_stack.Clear ();
		Layers[index].IsLocked = isLocked;
		InvalidateVisual ();
	}

	public void AddLayer ()
	{
		layer_undo_stack.Push (CaptureLayerStack ());
		layer_redo_stack.Clear ();
		session.AddLayer ($"Layer {session.Layers.Count}");
		bitmap = null;
		InvalidateVisual ();
	}

	public bool DuplicateActiveLayer ()
	{
		AvaloniaLayer source = Layers[ActiveLayerIndex];
		layer_undo_stack.Push (CaptureLayerStack ());
		layer_redo_stack.Clear ();
		AvaloniaLayer duplicate = session.AddLayer ($"{source.Name} copy");
		source.Surface.GetReadOnlyPixelData ().CopyTo (duplicate.Surface.GetPixelData ());
		duplicate.IsVisible = source.IsVisible;
		duplicate.IsLocked = source.IsLocked;
		duplicate.Opacity = source.Opacity;
		bitmap = null;
		InvalidateVisual ();
		return true;
	}

	public void SelectLayer (int index)
	{
		session.SelectLayer (index);
		bitmap = null;
		InvalidateVisual ();
	}

	public void RemoveLayer ()
	{
		if (Layers.Count <= 1) return;
		layer_undo_stack.Push (CaptureLayerStack ());
		layer_redo_stack.Clear ();
		if (session.RemoveActiveLayer ()) {
			bitmap = null;
			InvalidateVisual ();
		}
	}

	public void MoveLayer (int direction)
	{
		int target = ActiveLayerIndex + Math.Sign (direction);
		if ((uint) target >= (uint) Layers.Count) return;
		layer_undo_stack.Push (CaptureLayerStack ());
		layer_redo_stack.Clear ();
		if (session.MoveActiveLayer (direction)) {
			bitmap = null;
			InvalidateVisual ();
		}
	}

	public bool MoveLayerTo (int index)
	{
		if ((uint) index >= (uint) Layers.Count || index == ActiveLayerIndex) return false;
		layer_undo_stack.Push (CaptureLayerStack ());
		layer_redo_stack.Clear ();
		bool moved = session.MoveLayerTo (ActiveLayerIndex, index);
		if (moved) {
			bitmap = null;
			InvalidateVisual ();
		}
		return moved;
	}

	public bool MoveLayerTo (int from, int to)
	{
		if ((uint) from >= (uint) Layers.Count || (uint) to >= (uint) Layers.Count || from == to)
			return false;
		layer_undo_stack.Push (CaptureLayerStack ());
		layer_redo_stack.Clear ();
		bool moved = session.MoveLayerTo (from, to);
		if (moved) {
			bitmap = null;
			InvalidateVisual ();
		}
		return moved;
	}

	public bool MergeActiveLayerDown ()
	{
		if (ActiveLayerIndex <= 0 || Layers.Count < 2 || IsActiveLayerLocked || Layers[ActiveLayerIndex - 1].IsLocked)
			return false;
		layer_undo_stack.Push (CaptureLayerStack ());
		layer_redo_stack.Clear ();
		AvaloniaLayer upper = Layers[ActiveLayerIndex];
		AvaloniaLayer lower = Layers[ActiveLayerIndex - 1];
		Span<ColorBgra> destination = lower.Surface.GetPixelData ();
		ReadOnlySpan<ColorBgra> source = upper.Surface.GetReadOnlyPixelData ();
		for (int i = 0; i < destination.Length; i++) {
			ColorBgra color = source[i];
			if (upper.Opacity < 1)
				color = ColorBgra.FromBgra (color.B, color.G, color.R, (byte) Math.Round (color.A * upper.Opacity));
			if (upper.IsVisible)
				destination[i] = Blend (destination[i], color);
		}
		lower.Name = $"{lower.Name} + {upper.Name}";
		int lowerIndex = ActiveLayerIndex - 1;
		session.RemoveLayerAt (ActiveLayerIndex);
		SelectLayer (lowerIndex);
		bitmap = null;
		InvalidateVisual ();
		return true;
	}

	public bool CanUndoLayerOperation => layer_undo_stack.Count > 0;
	public bool CanRedoLayerOperation => layer_redo_stack.Count > 0;

	public void UndoLayerOperation ()
	{
		if (!CanUndoLayerOperation) return;
		layer_redo_stack.Push (CaptureLayerStack ());
		RestoreLayerStack (layer_undo_stack.Pop ());
	}

	public void RedoLayerOperation ()
	{
		if (!CanRedoLayerOperation) return;
		layer_undo_stack.Push (CaptureLayerStack ());
		RestoreLayerStack (layer_redo_stack.Pop ());
	}

	private LayerStackSnapshot CaptureLayerStack ()
		=> new (Layers.Select (layer => new LayerSnapshot (layer, layer.Name, layer.IsVisible, layer.IsLocked, layer.Opacity, layer.Surface.GetReadOnlyPixelData ().ToArray ())).ToList (), ActiveLayerIndex);

	private void RestoreLayerStack (LayerStackSnapshot snapshot)
	{
		if (snapshot.Layers.Count == 0) return;
		session.Layers.Clear ();
		foreach (LayerSnapshot item in snapshot.Layers) {
			AvaloniaLayer layer = item.Layer;
			item.Pixels.CopyTo (layer.Surface.GetPixelData ());
			layer.Name = item.Name;
			layer.IsVisible = item.IsVisible;
			layer.IsLocked = item.IsLocked;
			layer.Opacity = item.Opacity;
			session.Layers.Add (layer);
		}
		session.SelectLayer (Math.Min (snapshot.ActiveIndex, Layers.Count - 1));
		bitmap = null; InvalidateVisual ();
	}

	public void RenameLayer (string name)
	{
		if (string.Equals (Layers[ActiveLayerIndex].Name, name, StringComparison.Ordinal))
			return;
		layer_undo_stack.Push (CaptureLayerStack ());
		layer_redo_stack.Clear ();
		session.RenameActiveLayer (name);
		InvalidateVisual ();
	}

	public void SetScale (double scale)
	{
		session.SetScale (scale);
		ScaleChanged?.Invoke (this, EventArgs.Empty);
		InvalidateVisual ();
	}

	public void ZoomAt (Point viewPoint, double factor)
	{
		Point oldOrigin = GetCanvasOrigin ();
		ZoomTool.ZoomResult result = zoom_tool.Calculate (viewPoint, factor, Scale, oldOrigin, GetCenteredCanvasOrigin (), session.Viewport);
		SetScale (result.Scale);
		viewport_offset = result.Offset;
		InvalidateVisual ();
	}

	public void PanBy (Vector delta)
	{
		viewport_offset += delta;
		InvalidateVisual ();
	}

	public void FitToWindow ()
	{
		double hostWidth = Bounds.Width > 0 ? Bounds.Width : Width;
		double hostHeight = Bounds.Height > 0 ? Bounds.Height : Height;
		if (hostWidth <= 0 || hostHeight <= 0)
			return;

		viewport_offset = Vector.Zero;
		double scale = Math.Min (hostWidth / ImageSize.Width, hostHeight / ImageSize.Height);
		SetScale (Math.Clamp (scale, 0.1, 32));
	}

	public void SetActualSize ()
	{
		viewport_offset = Vector.Zero;
		SetScale (1.0);
	}

	public void NewDocument ()
	{
		NewDocument (ImageSize.Width, ImageSize.Height);
	}

	public void NewDocument (int width, int height)
	{
		if (width <= 0 || height <= 0)
			throw new ArgumentOutOfRangeException (nameof (width), "Document dimensions must be positive.");
		MemoryImageSurface surface = Surface is MemoryImageSurface existing && existing.Width == width && existing.Height == height
			? existing
			: new MemoryImageSurface (width, height);
		surface.Fill (ColorBgra.White);
		session = new AvaloniaDocumentSession (new Pinta.Core.Size (width, height), surface);
		ClearHistory ();
		viewport_offset = Vector.Zero;
		SetScale (1.0);
		bitmap = null;
		InvalidateVisual ();
	}

	public void ResizeImage (int width, int height)
	{
		if (width <= 0 || height <= 0)
			throw new ArgumentOutOfRangeException (nameof (width), "Image dimensions must be positive.");
		session.Resize (new Pinta.Core.Size (width, height));
		ClearHistory ();
		viewport_offset = Vector.Zero;
		bitmap = null;
		InvalidateVisual ();
	}

	public void CropImage (int left, int top, int width, int height)
	{
		session.Crop (left, top, width, height);
		ClearHistory ();
		viewport_offset = Vector.Zero;
		bitmap = null;
		InvalidateVisual ();
	}

	public void OpenPng (string path)
	{
		using Bitmap image = new (path);
		MemoryImageSurface surface = DecodeSurface (image);
		session = new AvaloniaDocumentSession (new Pinta.Core.Size (surface.Width, surface.Height), surface);
		ClearHistory ();
		viewport_offset = Vector.Zero;
		bitmap = null;
		InvalidateVisual ();
	}

	public void OpenImage (string path)
	{
		if (string.Equals (Path.GetExtension (path), ".ora", StringComparison.OrdinalIgnoreCase)) {
			OpenOra (path);
			return;
		}
		OpenPng (path);
	}

	public void ImportPngAsLayer (string path)
		=> ImportImageAsLayer (path);

	public void ImportImageAsLayer (string path)
	{
		if (string.Equals (Path.GetExtension (path), ".svg", StringComparison.OrdinalIgnoreCase)) {
			ImportSvgAsLayer (path);
			return;
		}
		using Bitmap image = new (path);
		MemoryImageSurface source = DecodeSurface (image);
		if (source.Width != ImageSize.Width || source.Height != ImageSize.Height)
			throw new InvalidDataException ("Imported layer dimensions must match the document.");
		AvaloniaLayer layer = session.AddLayer (Path.GetFileNameWithoutExtension (path));
		source.GetReadOnlyPixelData ().CopyTo (layer.Surface.GetPixelData ());
		bitmap = null;
		InvalidateVisual ();
	}

	public void ImportSvgAsLayer (string path)
	{
		if (Surface is null)
			throw new InvalidOperationException ("A document surface is required to import an image.");
		XElement root = XElement.Load (path);
		using var image = new SKBitmap (ImageSize.Width, ImageSize.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
		using var canvas = new SKCanvas (image);
		canvas.Clear (SKColors.Transparent);
		foreach (XElement element in root.Descendants ()) {
			string? fill = element.Attribute ("fill")?.Value;
			if (string.IsNullOrWhiteSpace (fill) || !TryParseSvgColor (fill, out SKColor color)) continue;
			using var paint = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill };
			if (float.TryParse (element.Attribute ("opacity")?.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float opacity))
				paint.Color = paint.Color.WithAlpha ((byte) Math.Clamp (opacity * 255, 0, 255));
			if (element.Name.LocalName == "rect" && TrySvgFloat (element, "x", out float x) && TrySvgFloat (element, "y", out float y) && TrySvgFloat (element, "width", out float width) && TrySvgFloat (element, "height", out float height))
				canvas.DrawRect (x, y, width, height, paint);
			else if (element.Name.LocalName == "circle" && TrySvgFloat (element, "cx", out float cx) && TrySvgFloat (element, "cy", out float cy) && TrySvgFloat (element, "r", out float radius))
				canvas.DrawCircle (cx, cy, radius, paint);
			else if (element.Name.LocalName == "ellipse" && TrySvgFloat (element, "cx", out float ecx) && TrySvgFloat (element, "cy", out float ecy) && TrySvgFloat (element, "rx", out float rx) && TrySvgFloat (element, "ry", out float ry))
				canvas.DrawOval (ecx - rx, ecy - ry, ecx + rx, ecy + ry, paint);
			else if (element.Name.LocalName == "line" && TrySvgFloat (element, "x1", out float x1) && TrySvgFloat (element, "y1", out float y1) && TrySvgFloat (element, "x2", out float x2) && TrySvgFloat (element, "y2", out float y2))
				canvas.DrawLine (x1, y1, x2, y2, paint);
			else if (element.Name.LocalName == "path" && !string.IsNullOrWhiteSpace (element.Attribute ("d")?.Value)) {
				using SKPath? pathData = SKPath.ParseSvgPathData (element.Attribute ("d")!.Value);
				if (pathData is not null) canvas.DrawPath (pathData, paint);
			}
		}
		AvaloniaLayer layer = session.AddLayer (Path.GetFileNameWithoutExtension (path));
		var bytes = new byte[ImageSize.Width * ImageSize.Height * ColorBgra.SizeOf];
		Marshal.Copy (image.GetPixels (), bytes, 0, bytes.Length);
		MemoryMarshal.Cast<byte, ColorBgra> (bytes).CopyTo (layer.Surface.GetPixelData ());
		bitmap = null;
		InvalidateVisual ();
	}

	private static bool TrySvgFloat (XElement element, string name, out float value)
		=> float.TryParse (element.Attribute (name)?.Value?.TrimEnd ('p', 'x'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);

	private static bool TryParseSvgColor (string value, out SKColor color)
	{
		color = SKColors.Transparent;
		if (!value.StartsWith ('#') || value.Length is not (7 or 9)) return false;
		if (!uint.TryParse (value[1..], System.Globalization.NumberStyles.HexNumber, null, out uint rgb)) return false;
		byte alpha = value.Length == 9 ? (byte) (rgb & 255) : (byte) 255;
		if (value.Length == 9) rgb >>= 8;
		color = new SKColor ((byte) (rgb >> 16), (byte) (rgb >> 8), (byte) rgb, alpha);
		return true;
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
		ClearHistory ();
		viewport_offset = Vector.Zero;
		bitmap = null;
		InvalidateVisual ();
	}

	private static void ApplyLayerMetadata (AvaloniaLayer layer, XElement node)
	{
		layer.Name = node.Attribute ("name")?.Value ?? layer.Name;
		layer.IsVisible = node.Attribute ("visibility")?.Value != "hidden";
		layer.IsLocked = node.Attribute ("locked")?.Value == "true";
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

	public void SaveImage (string path)
	{
		switch (Path.GetExtension (path).ToLowerInvariant ()) {
			case ".jpg":
			case ".jpeg":
				SaveJpeg (path);
				break;
			case ".webp":
				SaveWebp (path);
				break;
			case ".tif":
			case ".tiff":
				SaveTiff (path);
				break;
			case ".ora":
				SaveOra (path);
				break;
			default:
				SavePng (path);
				break;
		}
	}

	public void SaveJpeg (string path, int quality = 95)
	{
		SaveBitmap (path, new JpegBitmapEncoderOptions { Quality = Math.Clamp (quality, 1, 100) });
	}

	public void SaveWebp (string path, int quality = 95)
	{
		if (Surface is null)
			throw new InvalidOperationException ("A document surface is required to save the image.");
		using MemoryImageSurface composite = CreateCompositeSurface ();
		using var image = new SKBitmap (composite.Width, composite.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
		ReadOnlySpan<ColorBgra> pixels = composite.GetReadOnlyPixelData ();
		for (int y = 0; y < composite.Height; y++)
			for (int x = 0; x < composite.Width; x++) {
				ColorBgra pixel = pixels[y * composite.Width + x];
				image.SetPixel (x, y, new SKColor (pixel.R, pixel.G, pixel.B, pixel.A));
			}
		using SKData encoded = image.Encode (SKEncodedImageFormat.Webp, Math.Clamp (quality, 1, 100));
		if (encoded is null)
			throw new InvalidOperationException ("WebP encoding is not available.");
		using FileStream stream = File.Create (path);
		encoded.SaveTo (stream);
	}

	public void SaveTiff (string path)
	{
		if (Surface is null)
			throw new InvalidOperationException ("A document surface is required to save the image.");
		using MemoryImageSurface composite = CreateCompositeSurface ();
		using var stream = new FileStream (path, FileMode.Create, FileAccess.Write);
		using var writer = new BinaryWriter (stream, Encoding.UTF8, leaveOpen: true);
		int pixelOffset = 8 + 2 + 10 * 12 + 4 + 8;
		writer.Write ((byte) 'I'); writer.Write ((byte) 'I'); writer.Write ((ushort) 42); writer.Write (8u);
		writer.Write ((ushort) 10);
		WriteTiffEntry (writer, 256, 4, 1, (uint) composite.Width);
		WriteTiffEntry (writer, 257, 4, 1, (uint) composite.Height);
		WriteTiffEntry (writer, 258, 3, 4, (uint) (8 + 2 + 10 * 12 + 4));
		WriteTiffEntry (writer, 259, 3, 1, 1);
		WriteTiffEntry (writer, 262, 3, 1, 2);
		WriteTiffEntry (writer, 273, 4, 1, (uint) pixelOffset);
		WriteTiffEntry (writer, 277, 3, 1, 4);
		WriteTiffEntry (writer, 278, 4, 1, (uint) composite.Height);
		WriteTiffEntry (writer, 279, 4, 1, (uint) (composite.Width * composite.Height * 4));
		WriteTiffEntry (writer, 284, 3, 1, 1);
		writer.Write (0u);
		writer.Write ((ushort) 8); writer.Write ((ushort) 8); writer.Write ((ushort) 8); writer.Write ((ushort) 8);
		foreach (ColorBgra pixel in composite.GetReadOnlyPixelData ()) {
			writer.Write (pixel.R); writer.Write (pixel.G); writer.Write (pixel.B); writer.Write (pixel.A);
		}
	}

	private static void WriteTiffEntry (BinaryWriter writer, ushort tag, ushort type, uint count, uint value)
	{
		writer.Write (tag); writer.Write (type); writer.Write (count);
		if (type == 3 && count == 1) { writer.Write ((ushort) value); writer.Write ((ushort) 0); } else writer.Write (value);
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
			xml.Append ($"<layer name=\"{System.Security.SecurityElement.Escape (layer.Name)}\" src=\"{source}\" opacity=\"{layer.Opacity.ToString (System.Globalization.CultureInfo.InvariantCulture)}\" visibility=\"{(layer.IsVisible ? "visible" : "hidden")}\" locked=\"{(layer.IsLocked ? "true" : "false")}\" />");
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
		BrushTool activeBrush = ActiveTool == CanvasTool.Eraser ? eraser_tool : brush_tool;
		if (IsActiveLayerLocked || !activeBrush.DrawPoint (Surface, canvasPoint, color, BrushSize, BrushOpacity,
			BrushHardness, activeBrush.IsEraser))
			return;
		bitmap = null;
		InvalidateVisual ();
	}

	private static ColorBgra Blend (ColorBgra destination, ColorBgra source)
	{
		if (source.A == 255)
			return source;
		if (source.A == 0)
			return destination;
		int sourceAlpha = source.A;
		int destinationAlpha = destination.A;
		int outputAlpha = sourceAlpha + destinationAlpha * (255 - sourceAlpha) / 255;
		if (outputAlpha == 0)
			return ColorBgra.Zero;
		return ColorBgra.FromBgra (
			(byte) ((source.B * sourceAlpha + destination.B * destinationAlpha * (255 - sourceAlpha) / 255) / outputAlpha),
			(byte) ((source.G * sourceAlpha + destination.G * destinationAlpha * (255 - sourceAlpha) / 255) / outputAlpha),
			(byte) ((source.R * sourceAlpha + destination.R * destinationAlpha * (255 - sourceAlpha) / 255) / outputAlpha),
			(byte) outputAlpha);
	}

	public void PaintCanvasLine (PointD from, PointD to, ColorBgra color)
	{
		BrushTool activeBrush = ActiveTool == CanvasTool.Eraser ? eraser_tool : brush_tool;
		if (IsActiveLayerLocked || !activeBrush.DrawLine (Surface, from, to, color, BrushSize, BrushOpacity,
			BrushHardness, activeBrush.IsEraser))
			return;
		bitmap = null;
		InvalidateVisual ();
	}

	public void PaintPencilLine (PointD from, PointD to, ColorBgra color)
	{
		if (IsActiveLayerLocked || !pencil_tool.DrawPencilLine (Surface, from, to, color, BrushOpacity))
			return;
		bitmap = null;
		InvalidateVisual ();
	}

	public bool CanUndoStroke => (undo_stack.Count > 0 && Layers.Contains (undo_stack.Peek ().Layer)) || CanUndoLayerOperation;
	public bool CanRedoStroke => (redo_stack.Count > 0 && Layers.Contains (redo_stack.Peek ().Layer)) || CanRedoLayerOperation;
	public IReadOnlyList<string> History => history_stack.ToArray ();
	public event EventHandler? HistoryChanged;

	private void ClearHistory ()
	{
		undo_stack.Clear ();
		redo_stack.Clear ();
		history_stack.Clear ();
		redo_history_stack.Clear ();
		layer_undo_stack.Clear ();
		layer_redo_stack.Clear ();
		has_current_stroke = false;
		HistoryChanged?.Invoke (this, EventArgs.Empty);
	}

	public void BeginStroke ()
	{
		if (Surface is null)
			return;

		undo_stack.Push (new RasterSnapshot (session.ActiveLayer!, Surface.GetReadOnlyPixelData ().ToArray (), shape_tool.CaptureState ()));
		redo_stack.Clear ();
		redo_history_stack.Clear ();
		history_stack.Push ("Raster operation");
		has_current_stroke = true;
		HistoryChanged?.Invoke (this, EventArgs.Empty);
	}

	public void CancelCurrentStroke ()
	{
		tool_set.CancelPointerActions ();
		pointer_action_tool = null;
		is_painting = false;
		last_canvas_point = null;
		if (!has_current_stroke || undo_stack.Count == 0) {
			InvalidateVisual ();
			return;
		}
		RasterSnapshot snapshot = undo_stack.Pop ();
		history_stack.Pop ();
		snapshot.Pixels.AsSpan ().CopyTo (snapshot.Layer.Surface.GetPixelData ());
		shape_tool.RestoreState (snapshot.Shape);
		has_current_stroke = false;
		bitmap = null;
		InvalidateVisual ();
		HistoryChanged?.Invoke (this, EventArgs.Empty);
	}

	public void CommitStroke ()
	{
		has_current_stroke = false;
	}

	public bool ClearActiveLayer ()
	{
		if (Surface is null || IsActiveLayerLocked)
			return false;
		ReadOnlySpan<ColorBgra> pixels = Surface.GetReadOnlyPixelData ();
		for (int i = 0; i < pixels.Length; i++)
			if (pixels[i] != ColorBgra.Zero)
				goto has_pixels;
		return false;

	has_pixels:
		BeginStroke ();
		Surface.GetPixelData ().Fill (ColorBgra.Zero);
		bitmap = null;
		InvalidateVisual ();
		CommitStroke ();
		return true;
	}

	public void UndoLastStroke ()
	{
		if (undo_stack.Count == 0 || !Layers.Contains (undo_stack.Peek ().Layer)) {
			UndoLayerOperation ();
			return;
		}
		if (!CanUndoStroke)
			return;

		RasterSnapshot snapshot = undo_stack.Pop ();
		AvaloniaLayer layer = snapshot.Layer;
		redo_stack.Push (new RasterSnapshot (layer, layer.Surface.GetReadOnlyPixelData ().ToArray (), shape_tool.CaptureState ()));
		snapshot.Pixels.AsSpan ().CopyTo (layer.Surface.GetPixelData ());
		shape_tool.RestoreState (snapshot.Shape);
		redo_history_stack.Push (history_stack.Pop ());
		bitmap = null;
		InvalidateVisual ();
		HistoryChanged?.Invoke (this, EventArgs.Empty);
	}

	public void RedoLastStroke ()
	{
		if (redo_stack.Count == 0 || !Layers.Contains (redo_stack.Peek ().Layer)) {
			RedoLayerOperation ();
			return;
		}
		if (!CanRedoStroke)
			return;

		RasterSnapshot snapshot = redo_stack.Pop ();
		AvaloniaLayer layer = snapshot.Layer;
		undo_stack.Push (new RasterSnapshot (layer, layer.Surface.GetReadOnlyPixelData ().ToArray (), shape_tool.CaptureState ()));
		snapshot.Pixels.AsSpan ().CopyTo (layer.Surface.GetPixelData ());
		shape_tool.RestoreState (snapshot.Shape);
		history_stack.Push (redo_history_stack.Pop ());
		bitmap = null;
		InvalidateVisual ();
		HistoryChanged?.Invoke (this, EventArgs.Empty);
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

		if (Surface is not null) {
			MemoryImageSurface? shapePreview = null;
			try {
				if (ActiveTool == CanvasTool.Shapes && !IsActiveLayerLocked && shape_tool.IsEditing) {
					shapePreview = new MemoryImageSurface (ImageSize.Width, ImageSize.Height);
					shape_tool.DrawPreview (shapePreview);
				} else if (ActiveTool == CanvasTool.Shapes && !IsActiveLayerLocked && shape_tool.IsCreating) {
					shapePreview = new MemoryImageSurface (ImageSize.Width, ImageSize.Height);
					shape_tool.DrawCreationPreview (shapePreview);
				}
				using MemoryImageSurface composite = CreateCompositeSurface (shapePreview);
				DrawSurface (context, origin, composite);
			} finally {
				shapePreview?.Dispose ();
			}
		} else
			context.FillRectangle (Brushes.White, new Rect (origin, new global::Avalonia.Size (viewSize.Width, viewSize.Height)));
		context.DrawRectangle (new Pen (Brushes.Gray, 1), new Rect (origin, new global::Avalonia.Size (viewSize.Width, viewSize.Height)));
		if (ActiveTool == CanvasTool.Gradient && is_painting)
			gradient_tool.DrawGuide (context, origin, Scale, Math.Max (1, BrushSize * Scale));
		if (Selection is RectangleI selected) {
			var selection_pen = new Pen (Brushes.DodgerBlue, 1, new DashStyle (new[] { 4d, 4d }, 0));
			context.DrawRectangle (selection_pen, new Rect (origin.X + selected.X * Scale, origin.Y + selected.Y * Scale, selected.Width * Scale, selected.Height * Scale));
		}
		if (ActiveTool == CanvasTool.Shapes && shape_tool.SelectedShape is ShapeTool.ShapeState selectedShape && !shape_tool.IsEditing)
			shape_tool.DrawHandles (context, origin, Scale, selectedShape.Kind, selectedShape.From, selectedShape.To, selectedShape.Rotation);

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
			for (int x = 0; x < columns; x++) {
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

	private MemoryImageSurface CreateCompositeSurface (IImageSurface? activeLayerPreview = null)
	{
		var composite = new MemoryImageSurface (ImageSize.Width, ImageSize.Height);
		Span<ColorBgra> destination = composite.GetPixelData ();
		foreach (AvaloniaLayer layer in Layers) {
			if (!layer.IsVisible || layer.Opacity <= 0)
				continue;
			ReadOnlySpan<ColorBgra> source = ReferenceEquals (layer, session.ActiveLayer) && activeLayerPreview is not null
				? activeLayerPreview.GetReadOnlyPixelData ()
				: layer.Surface.GetReadOnlyPixelData ();
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
		ZoomAt (e.GetPosition (this), factor);
		e.Handled = true;
	}

	private void OnPointerPressed (object? sender, PointerPressedEventArgs e)
	{
		var properties = e.GetCurrentPoint (this).Properties;
		if (properties.IsMiddleButtonPressed) {
			pan_start_view_point = e.GetPosition (this);
			pan_start_offset = viewport_offset;
			e.Pointer.Capture (this);
			e.Handled = true;
			return;
		}
		if (!properties.IsLeftButtonPressed && !properties.IsRightButtonPressed)
			return;

		stroke_color = ActiveTool == CanvasTool.Eraser
			? ColorBgra.Zero
			: properties.IsRightButtonPressed ? SecondaryBrushColor : BrushColor;
		e.Pointer.Capture (this);
		Point position = e.GetPosition (this);
		Point origin = GetCanvasOrigin ();
		last_canvas_point = session.Viewport.ViewPointToCanvas (new PointD (position.X - origin.X, position.Y - origin.Y));
		CanvasToolPointerContext context = new (this, position, last_canvas_point.Value, last_canvas_point.Value,
			stroke_color, properties.IsRightButtonPressed, false);
		bool handled = tool_set.Get<ICanvasTool> (ActiveTool).PointerPressed (context);
		if (ActiveTool == CanvasTool.Zoom) {
			e.Pointer.Capture (null);
			e.Handled = handled;
			return;
		}
		if (ActiveTool is CanvasTool.Selection or CanvasTool.Shapes or CanvasTool.Gradient
			or CanvasTool.Brush or CanvasTool.Pencil or CanvasTool.Eraser) {
			is_painting = handled;
			pointer_action_tool = handled ? ActiveTool : null;
		}
		if (!handled)
			e.Pointer.Capture (null);
		e.Handled = handled;
	}

	public void PickColorAt (PointD point)
	{
		if (color_picker_tool.TryPick (Surface, point, out ColorBgra picked))
			BrushColor = picked;
	}

	public bool FillAt (PointD point, ColorBgra color)
	{
		if (IsActiveLayerLocked || !fill_tool.Fill (Surface, point, color, FillTolerance, BeginStroke, CommitStroke))
			return false;
		bitmap = null;
		InvalidateVisual ();
		return true;
	}

	private void OnPointerReleased (object? sender, PointerReleasedEventArgs e)
	{
		if (pan_start_view_point is not null) {
			pan_start_view_point = null;
			e.Pointer.Capture (null);
			e.Handled = true;
			return;
		}
		if (is_painting) {
			Point position = e.GetPosition (this);
			Point origin = GetCanvasOrigin ();
			PointD point = session.Viewport.ViewPointToCanvas (new PointD (position.X - origin.X, position.Y - origin.Y));
			PointD previous = last_canvas_point ?? point;
			tool_set.Get<ICanvasTool> (pointer_action_tool ?? ActiveTool).PointerReleased (
				new CanvasToolPointerContext (this, position, point, previous, stroke_color, false, false));
		}
		is_painting = false;
		pointer_action_tool = null;
		last_canvas_point = null;
		has_current_stroke = false;
		InvalidateVisual ();
		e.Pointer.Capture (null);
	}

	private void OnPointerMoved (object? sender, PointerEventArgs e)
	{
		if (pan_start_view_point is Point panStart) {
			PanBy (e.GetPosition (this) - panStart - (viewport_offset - pan_start_offset));
			pan_start_view_point = e.GetPosition (this);
			pan_start_offset = viewport_offset;
			e.Handled = true;
			return;
		}
		if (!is_painting)
			return;

		Point position = e.GetPosition (this);
		Point origin = GetCanvasOrigin ();
		PointD current = session.Viewport.ViewPointToCanvas (new PointD (position.X - origin.X, position.Y - origin.Y));
		PointD previous = last_canvas_point ?? current;
		bool handled = tool_set.Get<ICanvasTool> (pointer_action_tool ?? ActiveTool).PointerMoved (
			new CanvasToolPointerContext (this, position, current, previous, stroke_color, false,
				(e.KeyModifiers & KeyModifiers.Shift) != 0));
		last_canvas_point = current;
		e.Handled = handled;
	}

	private void OnKeyDown (object? sender, KeyEventArgs e)
	{
		if (e.Key == Key.Escape) {
			selection_tool.CancelSelection ();
			if (shape_tool.IsEditing)
				shape_tool.CancelEdit (CancelCurrentStroke);
			else
				CancelCurrentStroke ();
			e.Handled = true;
		} else if (ActiveTool == CanvasTool.Shapes && shape_tool.SelectedShape is not null && e.Key is Key.Left or Key.Right or Key.Up or Key.Down) {
			int step = (e.KeyModifiers & KeyModifiers.Shift) != 0 ? 10 : 1;
			PointD delta = e.Key switch {
				Key.Left => new PointD (-step, 0),
				Key.Right => new PointD (step, 0),
				Key.Up => new PointD (0, -step),
				_ => new PointD (0, step)
			};
			if (shape_tool.MoveSelected (delta, Surface as MemoryImageSurface, IsActiveLayerLocked, BeginStroke, CommitStroke)) {
				bitmap = null;
				InvalidateVisual ();
			}
			e.Handled = true;
		} else if (Selection is not null && e.Key is Key.Left or Key.Right or Key.Up or Key.Down) {
			PointI delta = e.Key switch {
				Key.Left => new PointI (-1, 0),
				Key.Right => new PointI (1, 0),
				Key.Up => new PointI (0, -1),
				_ => new PointI (0, 1)
			};
			MoveSelection (delta);
			e.Handled = true;
		} else if (keyboard.Matches (e, PintoraKeyCommands.ClearLayer)) {
			ClearActiveLayer ();
			e.Handled = true;
		} else if (keyboard.Matches (e, PintoraKeyCommands.Copy)) {
			CopySelection ();
			e.Handled = true;
		} else if (keyboard.Matches (e, PintoraKeyCommands.Paste)) {
			PasteSelection ();
			e.Handled = true;
		} else if (keyboard.Matches (e, PintoraKeyCommands.SelectAll)) {
			SelectAll ();
			e.Handled = true;
		} else if (keyboard.Matches (e, PintoraKeyCommands.Deselect)) {
			ClearSelection ();
			e.Handled = true;
		} else if (keyboard.Matches (e, PintoraKeyCommands.DuplicateLayer)) {
			DuplicateActiveLayer ();
			e.Handled = true;
		} else if (keyboard.Matches (e, PintoraKeyCommands.SwapColors)) {
			SwapColors ();
			e.Handled = true;
		} else if (keyboard.Matches (e, PintoraKeyCommands.ActualSize)) {
			SetActualSize ();
			e.Handled = true;
		} else if (keyboard.Matches (e, PintoraKeyCommands.FitCanvas)) {
			FitToWindow ();
			e.Handled = true;
		} else if (keyboard.Matches (e, PintoraKeyCommands.Undo)) {
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
		} else if (keyboard.Matches (e, PintoraKeyCommands.Brush)) {
			ActiveTool = CanvasTool.Brush; e.Handled = true;
		} else if (keyboard.Matches (e, PintoraKeyCommands.Pencil)) {
			ActiveTool = CanvasTool.Pencil; e.Handled = true;
		} else if (keyboard.Matches (e, PintoraKeyCommands.Line)) {
			SelectedShape = ShapeKind.Line; ActiveTool = CanvasTool.Shapes; e.Handled = true;
		} else if (keyboard.Matches (e, PintoraKeyCommands.Rectangle)) {
			SelectedShape = ShapeKind.Rectangle; ActiveTool = CanvasTool.Shapes; e.Handled = true;
		} else if (keyboard.Matches (e, PintoraKeyCommands.Shapes)) {
			ActiveTool = CanvasTool.Shapes; e.Handled = true;
		} else if (keyboard.Matches (e, PintoraKeyCommands.Fill)) {
			ActiveTool = CanvasTool.Fill; e.Handled = true;
		} else if (keyboard.Matches (e, PintoraKeyCommands.ColorPicker)) {
			ActiveTool = CanvasTool.ColorPicker; e.Handled = true;
		} else if (keyboard.Matches (e, PintoraKeyCommands.Zoom)) {
			ActiveTool = CanvasTool.Zoom; e.Handled = true;
		}
	}

	private Rect GetCanvasRect ()
	{
		if (rendered_canvas_rect.Width > 0 && rendered_canvas_rect.Height > 0)
			return rendered_canvas_rect;

		Pinta.Core.Size viewSize = ViewSize;
		return new Rect (GetCanvasOrigin (), new global::Avalonia.Size (viewSize.Width, viewSize.Height));
	}

	public void PaintCanvasShape (ShapeKind shape, PointD from, PointD to, ColorBgra color)
		=> PaintCanvasShape (shape, from, to, color, SecondaryBrushColor, FillStyle,
			ShapeOutlineWidth, DashStyle, ShapeCornerRadius, ShapeAntialias);

	private void PaintCanvasShape (ShapeKind shape, PointD from, PointD to, ColorBgra outline,
		ColorBgra fill, ShapeFillStyle style, int width, ShapeDashStyle dash, int cornerRadius, bool antialias)
	{
		if (Surface is not MemoryImageSurface surface || IsActiveLayerLocked)
			return;
		ColorBgra[] basePixels = surface.GetReadOnlyPixelData ().ToArray ();
		ShapeRasterizer.Draw (surface, shape, from, to, outline, fill, style, width, dash, cornerRadius, antialias);
		shape_tool.RegisterPaintedShape (ActiveLayer, basePixels, shape, from, to, outline, fill,
			style, width, dash, cornerRadius, antialias);
		bitmap = null;
		InvalidateVisual ();
	}

	public void PaintCanvasRectangle (PointD from, PointD to, ColorBgra color)
		=> PaintCanvasShape (ShapeKind.Rectangle, from, to, color);

	public void PaintCanvasEllipse (PointD from, PointD to, ColorBgra color)
		=> PaintCanvasShape (ShapeKind.Ellipse, from, to, color);

	public void PaintGradient (PointD from, PointD to)
	{
		if (Surface is null)
			return;
		BeginStroke ();
		GradientTool.Apply (Surface, from, to, BrushColor, SecondaryBrushColor, GradientReversed);
		bitmap = null;
		InvalidateVisual ();
	}

	private Point GetCanvasOrigin ()
	{
		Point centered = GetCenteredCanvasOrigin ();
		return centered + viewport_offset;
	}

	public Point CanvasOrigin => GetCanvasOrigin ();

	private Point GetCenteredCanvasOrigin ()
	{
		Pinta.Core.Size viewSize = ViewSize;
		double hostWidth = Bounds.Width > 0 ? Bounds.Width : Width;
		double hostHeight = Bounds.Height > 0 ? Bounds.Height : Height;
		return new Point (
			Math.Max ((hostWidth - viewSize.Width) / 2, 0),
			Math.Max ((hostHeight - viewSize.Height) / 2, 0));
	}
}
