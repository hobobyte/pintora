using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Pinta.Core;
using Pintora.Avalonia.Document;
using Pintora.Avalonia.Editor.History;
using Pintora.Avalonia.Editor.Hosting;
using Pintora.Avalonia.Editor.Annotations;
using Pintora.Avalonia.Editor.Viewport;
using Pintora.Avalonia.Imaging;
using Pintora.Avalonia.Input;
using SkiaSharp;

namespace Pintora.Avalonia.Canvas;

/// <summary>
/// Avalonia viewport for a document. Rendering is intentionally minimal until the shared
/// image renderer is separated from the GTK canvas.
/// </summary>
public sealed class CanvasHost : Control, IDisposable
{
	private bool disposed;
	public enum CanvasTool
	{
		Selection, Text,
		Brush,
		Pencil,
		Shapes,
		Gradient,
		Fill,
		ColorPicker,
		Zoom,
		Eraser,
		Blur, Pixelate, Highlight, Magnify, Spotlight
	}

	public enum ShapeKind { Line, Rectangle, Ellipse, RoundedRectangle }
	public enum ShapeFillStyle { Outline, Fill, FillAndOutline }
	public enum ShapeDashStyle { Solid, Dash, Dot, DashDot }

	private AvaloniaDocumentSession session;
	private DocumentSession document_session;
	private readonly HashSet<AvaloniaLayer> subscribed_layers = new ();
	private readonly NotifyCollectionChangedEventHandler session_layers_changed;
	private readonly Dictionary<string, IImageSurface> annotation_sources = new (StringComparer.Ordinal);
	private readonly HashSet<IImageSurface> owned_annotation_sources = new ();
	private Annotation? selected_annotation;
	private Annotation? dragged_annotation;
	private PointD drag_start_point;
	private Rect drag_start_bounds;
	private LayerStackSnapshot? drag_undo_snapshot;
	private bool drag_changed;
	private readonly CanvasToolSet tool_set = CanvasToolFactory.CreateTools ();
	private ShapeTool shape_tool => tool_set.Get<ShapeTool> (CanvasTool.Shapes);
	private TextTool text_tool => tool_set.Get<TextTool> (CanvasTool.Text);
	public string TextToolText { get => text_tool.Text; set { text_tool.Text = value; } }
	public string TextToolFontFamily { get => text_tool.FontFamily; set { text_tool.FontFamily = value; ToolOptionsChanged?.Invoke (this, EventArgs.Empty); } }
	public double TextToolFontSize { get => text_tool.FontSize; set { text_tool.FontSize = value; ToolOptionsChanged?.Invoke (this, EventArgs.Empty); } }
	public bool TextToolBold { get => text_tool.Bold; set { text_tool.Bold = value; ToolOptionsChanged?.Invoke (this, EventArgs.Empty); } }
	public bool TextToolUnderline { get => text_tool.Underline; set { text_tool.Underline = value; ToolOptionsChanged?.Invoke (this, EventArgs.Empty); } }
	private GradientTool gradient_tool => tool_set.Get<GradientTool> (CanvasTool.Gradient);
	public GradientToolOptions GetGradientToolOptions () => gradient_tool.Options;
	private FillTool fill_tool => tool_set.Get<FillTool> (CanvasTool.Fill);
	private SelectionTool selection_tool => tool_set.Get<SelectionTool> (CanvasTool.Selection);
	private ColorPickerTool color_picker_tool => tool_set.Get<ColorPickerTool> (CanvasTool.ColorPicker);
	private ZoomTool zoom_tool => tool_set.Get<ZoomTool> (CanvasTool.Zoom);
	private EffectTool effect_tool => tool_set.Get<EffectTool> (ActiveTool);
	private BrushTool brush_tool => tool_set.Brush (CanvasTool.Brush);
	private BrushTool pencil_tool => tool_set.Brush (CanvasTool.Pencil);
	private BrushTool eraser_tool => tool_set.Brush (CanvasTool.Eraser);
	public EffectToolOptions GetEffectOptions (CanvasTool tool)
		=> tool_set.Get<EffectTool> (tool).Options;
	public TextToolOptions GetTextToolOptions () => text_tool.Options;
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
	private readonly ShapeToolOptions shape_options = new ();
	private Rect rendered_canvas_rect;
	private Vector viewport_offset;
	private readonly Stack<RasterSnapshot> undo_stack = new ();
	private readonly Stack<RasterSnapshot> redo_stack = new ();
	private readonly Stack<LayerStackSnapshot> layer_undo_stack = new ();
	private readonly Stack<LayerStackSnapshot> layer_redo_stack = new ();
	private enum HistoryKind { Raster, Layer }
	private readonly EditorHistory<HistoryKind> history_kinds = new ();
	private readonly EditorHistory<HistoryKind> redo_history_kinds = new ();
	private sealed record RasterSnapshot (AvaloniaLayer Layer, ColorBgra[] Pixels, ShapeTool.ShapeSnapshot? Shape, List<Annotation> Annotations);
	private sealed record LayerSnapshot (AvaloniaLayer Layer, string Name, bool IsVisible, bool IsLocked, double Opacity, ColorBgra[] Pixels, List<Annotation> Annotations);
	private sealed record LayerStackSnapshot (List<LayerSnapshot> Layers, int ActiveIndex);
	private static RasterSnapshot CaptureRasterSnapshot (AvaloniaLayer layer, ShapeTool.ShapeSnapshot? shape)
		=> new (layer, layer.Surface.GetReadOnlyPixelData ().ToArray (), shape, layer.Annotations.Items.ToList ());

	private static void RestoreRasterSnapshot (RasterSnapshot snapshot)
	{
		snapshot.Pixels.AsSpan ().CopyTo (snapshot.Layer.Surface.GetPixelData ());
		snapshot.Layer.Annotations.Clear ();
		snapshot.Layer.Annotations.AddRange (snapshot.Annotations);
	}

	private void ReconcileAnnotationSelection (AvaloniaLayer layer)
	{
		if (selected_annotation is not null && !layer.Annotations.Items.Contains (selected_annotation))
			ClearAnnotationSelection ();
	}

	private void RecordLayerUndo ()
	{
		layer_undo_stack.Push (CaptureLayerStack ());
		layer_redo_stack.Clear ();
		history_kinds.Push (HistoryKind.Layer);
		redo_history_kinds.Clear ();
		HistoryChanged?.Invoke (this, EventArgs.Empty);
	}

	public CanvasHost () : this (new Pinta.Core.Size (800, 600))
	{
	}

	public CanvasHost (Pinta.Core.Size imageSize) : this (imageSize, null)
	{
	}

	public CanvasHost (Pinta.Core.Size imageSize, IImageSurface? surface, PintoraKeyboardSettings? keyboard = null)
	{
		session_layers_changed = OnSessionLayersChanged;
		session = new AvaloniaDocumentSession (imageSize, surface);
		document_session = new DocumentSession (session);
		document_session.StateChanged += OnDocumentSessionStateChanged;
		AttachSession (session);
		this.keyboard = keyboard ?? new PintoraKeyboardSettings ();
		ClipToBounds = true;
		Focusable = true;
		PointerWheelChanged += OnPointerWheelChanged;
		PointerPressed += OnPointerPressed;
		PointerReleased += OnPointerReleased;
		PointerMoved += OnPointerMoved;
		KeyDown += OnKeyDown;
	}

	private void SubscribeToLayer (AvaloniaLayer layer)
	{
		if (!subscribed_layers.Add (layer))
			return;
		layer.Changed += OnLayerChanged;
		layer.MetadataChanged += OnLayerMetadataChanged;
	}

	private void UnsubscribeFromLayer (AvaloniaLayer layer)
	{
		if (!subscribed_layers.Remove (layer))
			return;
		layer.Changed -= OnLayerChanged;
		layer.MetadataChanged -= OnLayerMetadataChanged;
	}

	private void SyncLayerSubscriptions ()
	{
		foreach (AvaloniaLayer layer in subscribed_layers.ToArray ()) {
			if (!session.Layers.Contains (layer))
				UnsubscribeFromLayer (layer);
		}
		foreach (AvaloniaLayer layer in session.Layers)
			SubscribeToLayer (layer);
	}

	private void OnSessionLayersChanged (object? sender, NotifyCollectionChangedEventArgs e)
	{
		SyncLayerSubscriptions ();
		LayersChanged?.Invoke (this, EventArgs.Empty);
	}

	private void AttachSession (AvaloniaDocumentSession nextSession)
	{
		nextSession.Layers.CollectionChanged += session_layers_changed;
		SyncLayerSubscriptions ();
	}

	private void DetachSession ()
	{
		session.Layers.CollectionChanged -= session_layers_changed;
		foreach (AvaloniaLayer layer in subscribed_layers.ToArray ())
			UnsubscribeFromLayer (layer);
	}

	private void InitializeSession (AvaloniaDocumentSession nextSession)
	{
		DetachSession ();
		document_session.StateChanged -= OnDocumentSessionStateChanged;
		session = nextSession;
		document_session = new DocumentSession (session);
		document_session.StateChanged += OnDocumentSessionStateChanged;
		AttachSession (session);
		shape_tool.ClearSelectedShape ();
	}

	private void OnDocumentSessionStateChanged (object? sender, EventArgs e)
		=> DocumentStateChanged?.Invoke (this, EventArgs.Empty);

	private void OnLayerChanged (object? sender, EventArgs e)
	{
		// AvaloniaLayer.Changed already implies a rendered content change, so
		// raise LayerContentChanged here and use the non-raising overload to
		// avoid a duplicate notification.
		MarkCanvasChanged ();
		if (sender is AvaloniaLayer layer)
			RaiseLayerContentChanged (layer);
	}

	/// <summary>
	/// Raised when the rendered content of a layer changes, either from a raster
	/// edit or from a layer-stack operation (add/remove/reorder/undo/redo).
	/// Name and lock changes are metadata only and do not raise this event.
	/// </summary>
	public event EventHandler<LayerContentChangedEventArgs>? LayerContentChanged;

	private void RaiseLayerContentChanged (AvaloniaLayer layer)
		=> LayerContentChanged?.Invoke (this, new LayerContentChangedEventArgs (layer));

	private void OnLayerMetadataChanged (object? sender, EventArgs e)
	{
		// Name and lock changes are metadata only: they refresh the layer list
		// but must not raise LayerContentChanged or invalidate the composite.
		// Visibility and opacity change the rendered result, so they redraw.
		LayersChanged?.Invoke (this, e);
		if (e is LayerMetadataChangedEventArgs { AffectsRenderedAppearance: true })
			MarkCanvasChanged ();
	}

	public double Scale => session.Scale;

	/// <summary>Registers a decoded source used by image and cursor annotations during compositing.</summary>
	public void RegisterAnnotationSource (string sourceId, IImageSurface source)
	{
		if (string.IsNullOrWhiteSpace (sourceId))
			throw new ArgumentException ("A source id is required.", nameof (sourceId));
		if (annotation_sources.TryGetValue (sourceId, out IImageSurface? previous) && previous != source && owned_annotation_sources.Remove (previous) && previous is IDisposable disposable)
			disposable.Dispose ();
		annotation_sources[sourceId] = source ?? throw new ArgumentNullException (nameof (source));
		MarkCanvasChanged ();
	}

	public void RegisterAnnotationSource (string sourceId, Bitmap source)
	{
		if (string.IsNullOrWhiteSpace (sourceId))
			throw new ArgumentException ("A source id is required.", nameof (sourceId));
		if (source is null)
			throw new ArgumentNullException (nameof (source));
		MemoryImageSurface decoded = DecodeSurface (source);
		RegisterAnnotationSource (sourceId, decoded);
		owned_annotation_sources.Add (decoded);
	}

	public bool UnregisterAnnotationSource (string sourceId)
	{
		bool removed = annotation_sources.Remove (sourceId, out IImageSurface? source);
		if (source is not null && owned_annotation_sources.Remove (source) && source is IDisposable disposable)
			disposable.Dispose ();
		if (removed)
			MarkCanvasChanged ();
		return removed;
	}

	private void ClearAnnotationSources ()
	{
		foreach (IImageSurface source in owned_annotation_sources)
			if (source is IDisposable disposable)
				disposable.Dispose ();
		owned_annotation_sources.Clear ();
		annotation_sources.Clear ();
	}

	public void Dispose ()
	{
		if (disposed)
			return;
		disposed = true;
		DetachSession ();
		document_session.StateChanged -= OnDocumentSessionStateChanged;
		ClearAnnotationSources ();
		ClearHistory ();
		bitmap = null;
		selected_annotation = null;
		dragged_annotation = null;
		drag_undo_snapshot = null;
	}

	public ImageAnnotation AddImageAnnotation (string sourceId, IImageSurface source, Rect bounds, double opacity = 1, bool preserveAspectRatio = true)
	{
		if (session.ActiveLayer is null || session.ActiveLayer.IsLocked)
			throw new InvalidOperationException ("The active layer is locked or unavailable.");
		RegisterAnnotationSource (sourceId, source);
		RecordLayerUndo ();
		AvaloniaLayer activeLayer = session.ActiveLayer ?? throw new InvalidOperationException ("The document has no active layer.");
		var annotation = new ImageAnnotation {
			SourceId = sourceId,
			Bounds = bounds,
			Opacity = opacity,
			PreserveAspectRatio = preserveAspectRatio
		};
		activeLayer.Annotations.Add (annotation);
		return annotation;
	}

	public async Task<ImageAnnotation?> AddImageAnnotationFromFileAsync (IImageSourceProvider provider, string path, Rect bounds, double opacity = 1, bool preserveAspectRatio = true, CancellationToken cancellationToken = default)
	{
		if (session.ActiveLayer is null || session.ActiveLayer.IsLocked)
			throw new InvalidOperationException ("The active layer is locked or unavailable.");
		ImageSourceData? loaded = await provider.LoadAsync (path, cancellationToken);
		if (loaded is null)
			return null;
		MemoryImageSurface source = DecodeSurface (loaded.Bitmap);
		RegisterAnnotationSource (loaded.SourceId, source);
		owned_annotation_sources.Add (source);
		return AddImageAnnotationFromRegisteredSource (loaded.SourceId, bounds, opacity, preserveAspectRatio);
	}

	public async Task<ImageAnnotation?> AddImageAnnotationFromClipboardAsync (IImageSourceProvider provider, double opacity = 1, bool preserveAspectRatio = true, CancellationToken cancellationToken = default)
	{
		if (session.ActiveLayer is null || session.ActiveLayer.IsLocked)
			throw new InvalidOperationException ("The active layer is locked or unavailable.");
		ImageSourceData? loaded = await provider.GetFromClipboardAsync (cancellationToken);
		if (loaded is null)
			return null;
		MemoryImageSurface source = DecodeSurface (loaded.Bitmap);
		RegisterAnnotationSource (loaded.SourceId, source);
		owned_annotation_sources.Add (source);
		Pinta.Core.Size size = ImageSize;
		return AddImageAnnotationFromRegisteredSource (loaded.SourceId, new Rect (size.Width * 0.25, size.Height * 0.25, size.Width * 0.5, size.Height * 0.5), opacity, preserveAspectRatio);
	}

	private ImageAnnotation AddImageAnnotationFromRegisteredSource (string sourceId, Rect bounds, double opacity, bool preserveAspectRatio)
	{
		if (session.ActiveLayer is null || session.ActiveLayer.IsLocked)
			throw new InvalidOperationException ("The active layer is locked or unavailable.");
		RecordLayerUndo ();
		AvaloniaLayer activeLayer = session.ActiveLayer ?? throw new InvalidOperationException ("The document has no active layer.");
		var annotation = new ImageAnnotation { SourceId = sourceId, Bounds = bounds, Opacity = opacity, PreserveAspectRatio = preserveAspectRatio };
		activeLayer.Annotations.Add (annotation);
		return annotation;
	}

	public bool MoveAnnotation (Annotation annotation, Vector delta)
	{
		if (annotation is ShapeAnnotation shape && shape_tool.SelectedShape?.Annotation is { } selectedShape
			&& ReferenceEquals (selectedShape, shape) && shape_tool.MoveSelected (new PointD (delta.X, delta.Y), Surface as MemoryImageSurface,
				IsActiveLayerLocked, BeginStroke, CommitStroke)) {
			selected_annotation = shape_tool.SelectedShape.Annotation;
			AnnotationSelectionChanged?.Invoke (this, EventArgs.Empty);
			bitmap = null;
			InvalidateVisual ();
			return true;
		}
		return TransformAnnotation (annotation, bounds => bounds.Translate (delta));
	}

	public bool ResizeAnnotation (Annotation annotation, global::Avalonia.Size size)
	{
		if (size.Width <= 0 || size.Height <= 0)
			return false;
		return TransformAnnotation (annotation, bounds => new Rect (bounds.Position, size));
	}

	public bool RotateAnnotation (Annotation annotation, double deltaRadians)
	{
		if (!double.IsFinite (deltaRadians))
			return false;
		// Region effects are raster operations; rotating their metadata without
		// rotating the effect kernel produces a misleading selection frame.
		if (annotation is RegionEffectAnnotation)
			return false;
		if (annotation is ShapeAnnotation shape && shape_tool.SelectedShape?.Annotation is { } selectedShape
			&& ReferenceEquals (selectedShape, shape) && shape_tool.RotateSelected (deltaRadians, Surface as MemoryImageSurface,
				IsActiveLayerLocked, BeginStroke, CommitStroke)) {
			selected_annotation = shape_tool.SelectedShape.Annotation;
			AnnotationSelectionChanged?.Invoke (this, EventArgs.Empty);
			bitmap = null;
			InvalidateVisual ();
			return true;
		}
		foreach (AvaloniaLayer layer in Layers) {
			if (layer.IsLocked || !layer.Annotations.Items.Contains (annotation))
				continue;
			RecordLayerUndo ();
			Annotation updated = annotation with { Rotation = annotation.Rotation + deltaRadians };
			layer.Annotations.Replace (annotation, updated);
			if (ReferenceEquals (selected_annotation, annotation)) {
				selected_annotation = updated;
				AnnotationSelectionChanged?.Invoke (this, EventArgs.Empty);
			}
			return true;
		}
		return false;
	}

	public bool RemoveAnnotation (Annotation annotation)
	{
		foreach (AvaloniaLayer layer in Layers) {
			if (layer.IsLocked || !layer.Annotations.Items.Contains (annotation))
				continue;
			RecordLayerUndo ();
			bool removed = layer.Annotations.Remove (annotation);
			if (removed && shape_tool.SelectedShape?.Annotation is { } selectedShape && ReferenceEquals (selectedShape, annotation))
				shape_tool.ClearSelectedShape ();
			if (removed && ReferenceEquals (selected_annotation, annotation))
				ClearAnnotationSelection ();
			return removed;
		}
		return false;
	}

	public Annotation? DuplicateSelectedAnnotation (Vector? offset = null)
	{
		if (selected_annotation is not { } annotation || session.ActiveLayer is not { } layer || layer.IsLocked || !layer.Annotations.Items.Contains (annotation))
			return null;
		RecordLayerUndo ();
		Annotation duplicate = annotation with { Bounds = annotation.Bounds.Translate (offset ?? new Vector (10, 10)) };
		layer.Annotations.Add (duplicate);
		selected_annotation = duplicate;
		AnnotationSelectionChanged?.Invoke (this, EventArgs.Empty);
		MarkCanvasChanged ();
		InvalidateVisual ();
		return duplicate;
	}

	public Annotation? HitTestAnnotation (PointD canvasPoint)
	{
		for (int layerIndex = Layers.Count - 1; layerIndex >= 0; layerIndex--) {
			AvaloniaLayer layer = Layers[layerIndex];
			if (!layer.IsVisible)
				continue;
			for (int annotationIndex = layer.Annotations.Items.Count - 1; annotationIndex >= 0; annotationIndex--) {
				Annotation annotation = layer.Annotations.Items[annotationIndex];
				// SelectionAnnotation is a transient overlay; all other annotation kinds
				// participate in the same hit-test and can be edited uniformly.
				if (annotation is not SelectionAnnotation && ContainsAnnotationPoint (annotation, canvasPoint))
					return annotation;
			}
		}
		return null;
	}

	public Annotation? SelectedAnnotation => selected_annotation;
	public event EventHandler? AnnotationSelectionChanged;

	public bool SelectAnnotationAt (PointD canvasPoint)
	{
		Annotation? next = HitTestAnnotation (canvasPoint);
		if (ReferenceEquals (selected_annotation, next))
			return next is not null;
		selected_annotation = next;
		AnnotationSelectionChanged?.Invoke (this, EventArgs.Empty);
		InvalidateVisual ();
		return next is not null;
	}

	public void ClearAnnotationSelection ()
	{
		if (selected_annotation is null)
			return;
		selected_annotation = null;
		AnnotationSelectionChanged?.Invoke (this, EventArgs.Empty);
		InvalidateVisual ();
	}

	private static bool ContainsAnnotationPoint (Annotation annotation, PointD point)
	{
		Rect bounds = annotation.Bounds;
		if (annotation is CursorAnnotation cursor) {
			double scale = Math.Max (0.01, cursor.Scale);
			bounds = new Rect (bounds.X - cursor.HotspotX * scale, bounds.Y - cursor.HotspotY * scale, bounds.Width * scale, bounds.Height * scale);
		}
		Point center = bounds.Center;
		double cosine = Math.Cos (-annotation.Rotation), sine = Math.Sin (-annotation.Rotation);
		double dx = point.X - center.X, dy = point.Y - center.Y;
		Point local = new (center.X + dx * cosine - dy * sine, center.Y + dx * sine + dy * cosine);
		return bounds.Contains (local);
	}

	private bool TransformAnnotation (Annotation annotation, Func<Rect, Rect> transform)
	{
		foreach (AvaloniaLayer layer in Layers) {
			if (layer.IsLocked || !layer.Annotations.Items.Contains (annotation))
				continue;
			RecordLayerUndo ();
			Annotation updated = annotation with { Bounds = transform (annotation.Bounds) };
			layer.Annotations.Replace (annotation, updated);
			if (ReferenceEquals (selected_annotation, annotation)) {
				selected_annotation = updated;
				AnnotationSelectionChanged?.Invoke (this, EventArgs.Empty);
			}
			return true;
		}
		return false;
	}

	private bool UpdateDraggedAnnotation (Annotation annotation, Vector delta)
	{
		foreach (AvaloniaLayer layer in Layers) {
			if (layer.IsLocked || !layer.Annotations.Items.Contains (annotation))
				continue;
			Annotation updated = annotation with { Bounds = drag_start_bounds.Translate (delta) };
			layer.Annotations.Replace (annotation, updated);
			selected_annotation = dragged_annotation = updated;
			drag_changed = delta.X != 0 || delta.Y != 0;
			InvalidateVisual ();
			return true;
		}
		return false;
	}

	private void CommitDraggedAnnotationUndo ()
	{
		if (!drag_changed || drag_undo_snapshot is null)
			return;
		layer_undo_stack.Push (drag_undo_snapshot);
		layer_redo_stack.Clear ();
		history_kinds.Push (HistoryKind.Layer);
		redo_history_kinds.Clear ();
		HistoryChanged?.Invoke (this, EventArgs.Empty);
	}

	public CursorAnnotation AddCursorAnnotation (string cursorId, IImageSurface source, Rect bounds, double scale = 1, ColorBgra? tint = null, int hotspotX = 0, int hotspotY = 0)
	{
		if (session.ActiveLayer is null || session.ActiveLayer.IsLocked)
			throw new InvalidOperationException ("The active layer is locked or unavailable.");
		RegisterAnnotationSource (cursorId, source);
		RecordLayerUndo ();
		AvaloniaLayer activeLayer = session.ActiveLayer ?? throw new InvalidOperationException ("The document has no active layer.");
		var annotation = new CursorAnnotation {
			CursorId = cursorId,
			Bounds = bounds,
			Scale = scale,
			Tint = tint ?? ColorBgra.White,
			HotspotX = hotspotX,
			HotspotY = hotspotY
		};
		activeLayer.Annotations.Add (annotation);
		return annotation;
	}

	/// <summary>Creates editable text metadata for the active layer.</summary>
	public TextAnnotation AddTextAnnotation (string text, PointD position, double fontSize = 16,
		ColorBgra? color = null, string fontFamily = "Inter", FontWeight fontWeight = FontWeight.Normal,
		bool underline = false)
	{
		if (string.IsNullOrWhiteSpace (text))
			throw new ArgumentException ("Text cannot be empty.", nameof (text));
		if (fontSize <= 0 || !double.IsFinite (fontSize))
			throw new ArgumentOutOfRangeException (nameof (fontSize));
		if (session.ActiveLayer is not { } activeLayer || activeLayer.IsLocked || Surface is not MemoryImageSurface surface)
			throw new InvalidOperationException ("The active layer is locked or unavailable.");

		RecordLayerUndo ();
		global::Avalonia.Size textSize = AnnotationRenderer.MeasureText (text, fontSize, fontFamily, fontWeight);
		var annotation = new TextAnnotation {
			Bounds = new Rect (position.X, position.Y, textSize.Width, textSize.Height),
			Text = text,
			FontFamily = string.IsNullOrWhiteSpace (fontFamily) ? "Inter" : fontFamily,
			FontSize = fontSize,
			FontWeight = fontWeight,
			Color = color ?? ColorBgra.Black,
			Underline = underline
		};
		activeLayer.Annotations.Add (annotation);
		selected_annotation = annotation;
		AnnotationSelectionChanged?.Invoke (this, EventArgs.Empty);
		MarkCanvasChanged ();
		return annotation;
	}

	public bool UpdateTextAnnotation (TextAnnotation annotation, string text, double? fontSize = null,
		ColorBgra? color = null, FontWeight? fontWeight = null, bool? underline = null, string? fontFamily = null)
	{
		if (string.IsNullOrWhiteSpace (text) || fontSize is <= 0 || (fontSize is double size && !double.IsFinite (size)))
			return false;
		foreach (AvaloniaLayer layer in Layers) {
			if (layer.IsLocked || !layer.Annotations.Items.Contains (annotation))
				continue;
			RecordLayerUndo ();
			double nextSize = fontSize ?? annotation.FontSize;
			TextAnnotation updated = annotation with {
				Text = text,
				FontSize = nextSize,
				Color = color ?? annotation.Color,
				FontWeight = fontWeight ?? annotation.FontWeight,
				Underline = underline ?? annotation.Underline,
				FontFamily = string.IsNullOrWhiteSpace (fontFamily) ? annotation.FontFamily : fontFamily,
				Bounds = new Rect (annotation.Bounds.Position,
					AnnotationRenderer.MeasureText (text, nextSize, string.IsNullOrWhiteSpace (fontFamily) ? annotation.FontFamily : fontFamily,
						fontWeight ?? annotation.FontWeight))
			};
			layer.Annotations.Replace (annotation, updated);
			if (ReferenceEquals (selected_annotation, annotation)) {
				selected_annotation = updated;
				AnnotationSelectionChanged?.Invoke (this, EventArgs.Empty);
			}
			MarkCanvasChanged ();
			return true;
		}
		return false;
	}

	/// <summary>The per-document aggregate owning dirty state, file path and title.</summary>
	public DocumentSession DocumentSession => document_session;

	public bool IsDirty => document_session.IsDirty;
	public event EventHandler? DocumentStateChanged;
	internal void InvalidateToolVisual () => InvalidateVisual ();
	internal void MarkCanvasChanged ()
		=> MarkCanvasChanged (false);

	/// <summary>
	/// Marks the canvas dirty and optionally raises <see cref="LayerContentChanged"/>
	/// for the active layer. Callers that mutate a layer surface directly (rather
	/// than through <see cref="AvaloniaLayer.Changed"/>) must pass
	/// <paramref name="raiseLayerContentChanged"/> so observers see the edit.
	/// </summary>
	internal void MarkCanvasChanged (bool raiseLayerContentChanged)
	{
		if (disposed)
			return;
		document_session.MarkDirty ();
		bitmap = null;
		InvalidateVisual ();
		if (raiseLayerContentChanged && session.ActiveLayer is { } layer)
			RaiseLayerContentChanged (layer);
	}
	public event EventHandler? ScaleChanged;
	private readonly BrushToolOptions brush_options = new ();
	public int BrushSize {
		get => brush_options.Size;
		set {
			int next = Math.Clamp (value, 1, 256);
			if (brush_options.Size == next)
				return;
			brush_options.Size = next;
			BrushSizeChanged?.Invoke (this, EventArgs.Empty);
		}
	}
	public BrushToolOptions GetBrushToolOptions () => brush_options;
	public event EventHandler? BrushSizeChanged;
	public event EventHandler? ToolOptionsChanged;
	public event EventHandler? ColorsChanged;
	public double BrushOpacity {
		get => brush_options.Opacity;
		set {
			double next = Math.Clamp (value, 0, 1);
			if (brush_options.Opacity == next) return;
			brush_options.Opacity = next;
			ToolOptionsChanged?.Invoke (this, EventArgs.Empty);
		}
	}
	public double BrushHardness {
		get => brush_options.Hardness;
		set {
			double next = Math.Clamp (value, 0, 1);
			if (brush_options.Hardness == next) return;
			brush_options.Hardness = next;
			ToolOptionsChanged?.Invoke (this, EventArgs.Empty);
		}
	}
	public ColorBgra BrushColor { get; set; } = ColorBgra.Black;
	public ColorBgra SecondaryBrushColor { get; set; } = ColorBgra.White;
	private readonly FillToolOptions fill_options = new ();
	public int FillTolerance {
		get => fill_options.Tolerance;
		set {
			int next = Math.Clamp (value, 0, 255);
			if (fill_options.Tolerance == next) return;
			fill_options.Tolerance = next;
			ToolOptionsChanged?.Invoke (this, EventArgs.Empty);
		}
	}
	public FillToolOptions GetFillToolOptions () => fill_options;
	public ShapeFillStyle FillStyle {
		get => shape_options.FillStyle;
		set {
			if (!Enum.IsDefined (value)) throw new ArgumentOutOfRangeException (nameof (value));
			if (shape_options.FillStyle == value) return;
			shape_options.FillStyle = value;
			UpdateSelectedShapeOptions ();
			ToolOptionsChanged?.Invoke (this, EventArgs.Empty);
			InvalidateVisual ();
		}
	}
	public bool ShapeFilled {
		get => FillStyle != ShapeFillStyle.Outline;
		set => FillStyle = value ? ShapeFillStyle.Fill : ShapeFillStyle.Outline;
	}
	public int ShapeOutlineWidth {
		get => shape_options.OutlineWidth;
		set {
			int next = Math.Clamp (value, 1, 256);
			if (shape_options.OutlineWidth == next) return;
			shape_options.OutlineWidth = next;
			UpdateSelectedShapeOptions ();
			ToolOptionsChanged?.Invoke (this, EventArgs.Empty);
			InvalidateVisual ();
		}
	}
	public ShapeDashStyle DashStyle {
		get => shape_options.Dash;
		set {
			if (!Enum.IsDefined (value)) throw new ArgumentOutOfRangeException (nameof (value));
			if (shape_options.Dash == value) return;
			shape_options.Dash = value;
			UpdateSelectedShapeOptions ();
			ToolOptionsChanged?.Invoke (this, EventArgs.Empty);
			InvalidateVisual ();
		}
	}
	public int ShapeCornerRadius {
		get => shape_options.CornerRadius;
		set {
			int next = Math.Clamp (value, 1, 256);
			if (shape_options.CornerRadius == next) return;
			shape_options.CornerRadius = next;
			UpdateSelectedShapeOptions ();
			ToolOptionsChanged?.Invoke (this, EventArgs.Empty);
			InvalidateVisual ();
		}
	}
	public bool ShapeAntialias {
		get => shape_options.Antialias;
		set {
			if (shape_options.Antialias == value) return;
			shape_options.Antialias = value;
			UpdateSelectedShapeOptions ();
			ToolOptionsChanged?.Invoke (this, EventArgs.Empty);
			InvalidateVisual ();
		}
	}
	public ShapeKind SelectedShape {
		get => shape_options.Kind;
		set {
			if (!Enum.IsDefined (value))
				throw new ArgumentOutOfRangeException (nameof (value));
			if (shape_options.Kind == value)
				return;
			shape_options.Kind = value;
			UpdateSelectedShapeOptions ();
			ToolOptionsChanged?.Invoke (this, EventArgs.Empty);
			SelectedShapeChanged?.Invoke (this, EventArgs.Empty);
			InvalidateVisual ();
		}
	}
	public event EventHandler? SelectedShapeChanged;
	public ShapeToolOptions GetShapeToolOptions () => shape_options;
	private void UpdateSelectedShapeOptions ()
	{
		if (shape_tool.UpdateSelectedOptions (session.ActiveLayer, Surface as MemoryImageSurface,
			shape_options.Kind, shape_options.FillStyle, shape_options.OutlineWidth, shape_options.Dash, shape_options.CornerRadius,
			shape_options.Antialias, BeginStroke, CommitStroke))
			bitmap = null;
	}

	public bool UpdateSelectedEffectOptions (double? strength = null, int? pixelSize = null)
	{
		if (selected_annotation is not RegionEffectAnnotation effect || session.ActiveLayer is not { } layer || layer.IsLocked || !layer.Annotations.Items.Contains (effect))
			return false;
		RegionEffectAnnotation updated = effect with {
			Strength = strength is double value ? Math.Clamp (value, 0, 1) : effect.Strength,
			PixelSize = pixelSize is int size ? Math.Clamp (size, 2, 64) : effect.PixelSize
		};
		if (updated == effect)
			return false;
		RecordLayerUndo ();
		layer.Annotations.Replace (effect, updated);
		selected_annotation = updated;
		MarkCanvasChanged ();
		InvalidateVisual ();
		return true;
	}
	public bool GradientReversed {
		get => gradient_tool.Options.Reversed;
		set {
			if (gradient_tool.Options.Reversed == value) return;
			gradient_tool.Options.Reversed = value;
			ToolOptionsChanged?.Invoke (this, EventArgs.Empty);
		}
	}
	private PointD? pointer_position;
	public PointD? PointerPosition => pointer_position;
	public event EventHandler? PointerPositionChanged;
	public CanvasTool ActiveTool {
		get => active_tool;
		set {
			if (active_tool == value)
				return;
			if (active_tool == CanvasTool.Shapes && value != CanvasTool.Shapes && !shape_tool.IsEditing)
				shape_tool.ClearSelectedShape ();
			active_tool = value;
			ActiveToolChanged?.Invoke (this, EventArgs.Empty);
			ToolOptionsChanged?.Invoke (this, EventArgs.Empty);
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
	public event EventHandler? LayersChanged;
	public int ActiveLayerIndex => session.ActiveLayerIndex;
	public event EventHandler? ActiveLayerChanged;
	public RectangleI? Selection => selection_tool.Selection;

	public bool SelectRectangle (PointI start, PointI end)
	{
		bool selected = selection_tool.SelectRectangle (start, end, ImageSize);
		SyncSelectionAnnotation ();
		InvalidateVisual ();
		return selected;
	}

	public bool SelectAll ()
		=> SelectRectangle (new PointI (0, 0), new PointI (ImageSize.Width, ImageSize.Height));

	public void ClearSelection ()
	{
		selection_tool.CancelSelection ();
		selection_tool.Clear ();
		RemoveSelectionAnnotations ();
		InvalidateVisual ();
	}

	private void RemoveSelectionAnnotations ()
	{
		if (ActiveLayer is not { } layer) return;
		foreach (SelectionAnnotation annotation in layer.Annotations.Items.OfType<SelectionAnnotation> ().ToArray ())
			layer.Annotations.Remove (annotation);
	}

	private void SyncSelectionAnnotation ()
	{
		RemoveSelectionAnnotations ();
		if (ActiveLayer is { } layer && selection_tool.Annotation is SelectionAnnotation annotation)
			layer.Annotations.Add (annotation);
	}

	public bool CopySelection ()
	{
		return selection_tool.Copy (Surface);
	}

	public async Task<bool> CopyImageToClipboardAsync (IClipboardService clipboard, CancellationToken cancellationToken = default)
	{
		using MemoryImageSurface composite = CreateCompositeSurface ();
		using WriteableBitmap image = CreateBitmap (composite);
		await clipboard.SetImageAsync (image, cancellationToken);
		return true;
	}

	public async Task<bool> CopySelectionToClipboardAsync (IClipboardService clipboard, CancellationToken cancellationToken = default)
	{
		if (Surface is null || Selection is not RectangleI area || area.Width <= 0 || area.Height <= 0)
			return false;
		using var selection = new MemoryImageSurface (area.Width, area.Height);
		RasterBlitter.CopyRegion (Surface, area, selection);
		using WriteableBitmap image = CreateBitmap (selection);
		await clipboard.SetImageAsync (image, cancellationToken);
		return true;
	}

	public bool PasteSelection (PointI destination)
	{
		if (!selection_tool.Paste (destination, name => session.AddLayer ($"{name} {Layers.Count}")))
			return false;
		MarkCanvasChanged (true);
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
		MarkCanvasChanged (true);
		return true;
	}

	public bool FlipSelectionHorizontal ()
	{
		if (IsActiveLayerLocked || !selection_tool.FlipHorizontal (Surface, BeginStroke, CommitStroke))
			return false;
		MarkCanvasChanged (true);
		return true;
	}

	public bool FlipSelectionVertical ()
	{
		if (IsActiveLayerLocked || !selection_tool.FlipVertical (Surface, BeginStroke, CommitStroke))
			return false;
		MarkCanvasChanged (true);
		return true;
	}

	public bool RotateSelectionClockwise ()
	{
		if (IsActiveLayerLocked || !selection_tool.RotateClockwise (Surface, BeginStroke, CommitStroke))
			return false;
		MarkCanvasChanged (true);
		return true;
	}

	public void SetActiveLayerOpacity (double opacity)
	{
		if (session.ActiveLayer is null)
			return;
		double clamped = Math.Clamp (opacity, 0, 1);
		if (session.ActiveLayer.Opacity == clamped) return;
		RecordLayerUndo ();
		session.ActiveLayer.Opacity = clamped;
		MarkCanvasChanged ();
	}

	public void SwapColors ()
	{
		(BrushColor, SecondaryBrushColor) = (SecondaryBrushColor, BrushColor);
		ColorsChanged?.Invoke (this, EventArgs.Empty);
	}

	public void ResetColors ()
	{
		BrushColor = ColorBgra.Black;
		SecondaryBrushColor = ColorBgra.White;
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
		RecordLayerUndo ();
		Layers[index].IsVisible = isVisible;
		MarkCanvasChanged ();
	}

	public void SetLayerLocked (int index, bool isLocked)
	{
		if ((uint) index >= Layers.Count || Layers[index].IsLocked == isLocked)
			return;
		RecordLayerUndo ();
		Layers[index].IsLocked = isLocked;
		MarkCanvasChanged ();
	}

	public void AddLayer ()
	{
		RecordLayerUndo ();
		SubscribeToLayer (session.AddLayer ($"Layer {session.Layers.Count}"));
		MarkCanvasChanged ();
	}

	public bool DuplicateActiveLayer ()
	{
		AvaloniaLayer source = Layers[ActiveLayerIndex];
		RecordLayerUndo ();
		AvaloniaLayer duplicate = session.AddLayer ($"{source.Name} copy");
		SubscribeToLayer (duplicate);
		source.Surface.GetReadOnlyPixelData ().CopyTo (duplicate.Surface.GetPixelData ());
		duplicate.IsVisible = source.IsVisible;
		duplicate.IsLocked = source.IsLocked;
		duplicate.Opacity = source.Opacity;
		duplicate.Annotations.AddRange (source.Annotations.Items);
		shape_tool.ClearSelectedShape ();
		MarkCanvasChanged ();
		return true;
	}

	public void SelectLayer (int index)
	{
		if (index == session.ActiveLayerIndex)
			return;
		session.SelectLayer (index);
		ClearAnnotationSelection ();
		shape_tool.ClearSelectedShape ();
		ActiveLayerChanged?.Invoke (this, EventArgs.Empty);
		bitmap = null;
		InvalidateVisual ();
	}

	public void RemoveLayer ()
	{
		if (Layers.Count <= 1) return;
		AvaloniaLayer removed = session.ActiveLayer!;
		RecordLayerUndo ();
		if (session.RemoveActiveLayer ()) {
			// Unsubscribe immediately so a detached layer can no longer push
			// Changed/MetadataChanged notifications into this canvas.
			UnsubscribeFromLayer (removed);
			if (selected_annotation is not null && removed.Annotations.Items.Contains (selected_annotation))
				ClearAnnotationSelection ();
			MarkCanvasChanged ();
		}
	}

	public void MoveLayer (int direction)
	{
		int target = ActiveLayerIndex + Math.Sign (direction);
		if ((uint) target >= (uint) Layers.Count) return;
		RecordLayerUndo ();
		if (session.MoveActiveLayer (direction)) {
			bitmap = null;
			InvalidateVisual ();
		}
	}

	public bool MoveLayerTo (int index)
	{
		if ((uint) index >= (uint) Layers.Count || index == ActiveLayerIndex) return false;
		RecordLayerUndo ();
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
		RecordLayerUndo ();
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
		RecordLayerUndo ();
		AvaloniaLayer upper = Layers[ActiveLayerIndex];
		AvaloniaLayer lower = Layers[ActiveLayerIndex - 1];
		Span<ColorBgra> destination = lower.Surface.GetPixelData ();
		using var renderedUpper = new MemoryImageSurface (ImageSize.Width, ImageSize.Height);
		upper.Surface.GetReadOnlyPixelData ().CopyTo (renderedUpper.GetPixelData ());
		RenderNonRasterAnnotations (renderedUpper, upper.Annotations.Items);
		ReadOnlySpan<ColorBgra> source = renderedUpper.GetReadOnlyPixelData ();
		for (int i = 0; i < destination.Length; i++) {
			ColorBgra color = source[i];
			if (upper.Opacity < 1)
				color = ColorBgra.FromBgra (color.B, color.G, color.R, (byte) Math.Round (color.A * upper.Opacity));
			if (upper.IsVisible)
				destination[i] = Blend (destination[i], color);
		}
		// The merged layer now contains the complete visible result. Keeping
		// the lower layer's old opacity would attenuate the upper layer again.
		lower.Opacity = 1;
		lower.IsVisible = lower.IsVisible || upper.IsVisible;
		lower.Name = $"{lower.Name} + {upper.Name}";
		int lowerIndex = ActiveLayerIndex - 1;
		session.RemoveLayerAt (ActiveLayerIndex);
		// The upper layer is gone from the stack; stop listening to it right
		// away instead of waiting for the collection-changed handler.
		UnsubscribeFromLayer (upper);
		ClearAnnotationSelection ();
		shape_tool.ClearSelectedShape ();
		SelectLayer (lowerIndex);
		bitmap = null;
		InvalidateVisual ();
		return true;
	}

	public bool CanUndoLayerOperation => layer_undo_stack.Count > 0;
	public bool CanRedoLayerOperation => layer_redo_stack.Count > 0;

	public void UndoLayerOperation ()
		=> UndoLayerOperationCore (false);

	private void UndoLayerOperationCore (bool fromUnifiedHistory)
	{
		if (!CanUndoLayerOperation) return;
		layer_redo_stack.Push (CaptureLayerStack ());
		RestoreLayerStack (layer_undo_stack.Pop ());
		if (fromUnifiedHistory && history_kinds.Count > 0 && history_kinds.Peek () == HistoryKind.Layer)
			history_kinds.Pop ();
		if (fromUnifiedHistory)
			redo_history_kinds.Push (HistoryKind.Layer);
		HistoryChanged?.Invoke (this, EventArgs.Empty);
	}

	public void RedoLayerOperation ()
		=> RedoLayerOperationCore (false);

	private void RedoLayerOperationCore (bool fromUnifiedHistory)
	{
		if (!CanRedoLayerOperation) return;
		layer_undo_stack.Push (CaptureLayerStack ());
		RestoreLayerStack (layer_redo_stack.Pop ());
		if (fromUnifiedHistory && redo_history_kinds.Count > 0 && redo_history_kinds.Peek () == HistoryKind.Layer)
			redo_history_kinds.Pop ();
		if (fromUnifiedHistory)
			history_kinds.Push (HistoryKind.Layer);
		HistoryChanged?.Invoke (this, EventArgs.Empty);
	}

	private LayerStackSnapshot CaptureLayerStack ()
		=> new (Layers.Select (layer => new LayerSnapshot (layer, layer.Name, layer.IsVisible, layer.IsLocked, layer.Opacity, layer.Surface.GetReadOnlyPixelData ().ToArray (), layer.Annotations.Items.ToList ())).ToList (), ActiveLayerIndex);

	private void RestoreLayerStack (LayerStackSnapshot snapshot)
	{
		if (snapshot.Layers.Count == 0) return;
		ClearAnnotationSelection ();
		DetachSession ();
		session.Layers.Clear ();
		foreach (LayerSnapshot item in snapshot.Layers) {
			AvaloniaLayer layer = item.Layer;
			SubscribeToLayer (layer);
			item.Pixels.CopyTo (layer.Surface.GetPixelData ());
			layer.Name = item.Name;
			layer.IsVisible = item.IsVisible;
			layer.IsLocked = item.IsLocked;
			layer.Opacity = item.Opacity;
			layer.Annotations.Clear ();
			layer.Annotations.AddRange (item.Annotations);
			session.Layers.Add (layer);
		}
		AttachSession (session);
		session.SelectLayer (Math.Min (snapshot.ActiveIndex, Layers.Count - 1));
		bitmap = null; InvalidateVisual ();
		foreach (AvaloniaLayer layer in Layers)
			RaiseLayerContentChanged (layer);
	}

	public void RenameLayer (string name)
	{
		if (string.Equals (Layers[ActiveLayerIndex].Name, name, StringComparison.Ordinal))
			return;
		RecordLayerUndo ();
		session.RenameActiveLayer (name);
		MarkCanvasChanged ();
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
		ClearAnnotationSelection ();
		ClearAnnotationSources ();
		InitializeSession (new AvaloniaDocumentSession (new Pinta.Core.Size (width, height), surface));
		ClearHistory ();
		SetClean ();
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
		MarkCanvasChanged (true);
		viewport_offset = Vector.Zero;
		bitmap = null;
		InvalidateVisual ();
	}

	public void CropImage (int left, int top, int width, int height)
	{
		session.Crop (left, top, width, height);
		ClearHistory ();
		MarkCanvasChanged (true);
		viewport_offset = Vector.Zero;
		bitmap = null;
		InvalidateVisual ();
	}

	public void OpenPng (string path)
	{
		using Bitmap image = new (path);
		MemoryImageSurface surface = DecodeSurface (image);
		ClearAnnotationSelection ();
		ClearAnnotationSources ();
		InitializeSession (new AvaloniaDocumentSession (new Pinta.Core.Size (surface.Width, surface.Height), surface));
		document_session.MarkSaved (path);
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
		SubscribeToLayer (layer);
		source.GetReadOnlyPixelData ().CopyTo (layer.Surface.GetPixelData ());
		MarkCanvasChanged (true);
	}

	public bool PasteImageAsLayer (Bitmap image, string name = "Clipboard")
	{
		MemoryImageSurface source = DecodeSurface (image);
		return PasteSurfaceAsLayer (source, name);
	}

	private bool PasteSurfaceAsLayer (MemoryImageSurface source, string name)
	{
		RecordLayerUndo ();
		AvaloniaLayer layer = session.AddLayer (string.IsNullOrWhiteSpace (name) ? "Clipboard" : name.Trim ());
		SubscribeToLayer (layer);
		RasterBlitter.CopyCentered (source, layer.Surface);
		MarkCanvasChanged (true);
		return true;
	}

	public async Task<bool> PasteImageFromClipboardAsync (IImageSourceProvider provider, CancellationToken cancellationToken = default)
	{
		ImageSourceData? source = await provider.GetFromClipboardAsync (cancellationToken);
		if (source is null)
			return false;
		MemoryImageSurface decoded = DecodeSurface (source.Bitmap);
		RegisterAnnotationSource (source.SourceId, decoded);
		owned_annotation_sources.Add (decoded);
		return PasteSurfaceAsLayer (decoded, "Clipboard");
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
		SubscribeToLayer (layer);
		var bytes = new byte[ImageSize.Width * ImageSize.Height * ColorBgra.SizeOf];
		Marshal.Copy (image.GetPixels (), bytes, 0, bytes.Length);
		MemoryMarshal.Cast<byte, ColorBgra> (bytes).CopyTo (layer.Surface.GetPixelData ());
		MarkCanvasChanged (true);
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

		ClearAnnotationSelection ();
		shape_tool.ClearSelectedShape ();
		ClearAnnotationSources ();
		MemoryImageSurface firstSurface = DecodeSurface (new Bitmap (archive.GetEntry (layerNodes[0].Attribute ("src")!.Value)!.Open ()));
		InitializeSession (new AvaloniaDocumentSession (new Pinta.Core.Size (firstSurface.Width, firstSurface.Height), firstSurface));
		ApplyLayerMetadata (session.ActiveLayer!, layerNodes[0]);
		for (int i = 1; i < layerNodes.Count; i++) {
			XElement node = layerNodes[i];
			MemoryImageSurface source = DecodeSurface (new Bitmap (archive.GetEntry (node.Attribute ("src")!.Value)!.Open ()));
			AvaloniaLayer layer = session.AddLayer (node.Attribute ("name")?.Value ?? $"Layer {i}");
			SubscribeToLayer (layer);
			source.GetReadOnlyPixelData ().CopyTo (layer.Surface.GetPixelData ());
			ApplyLayerMetadata (layer, node);
		}
		LoadRasterAnnotations (archive);
		document_session.MarkSaved (path);
		ClearHistory ();
		viewport_offset = Vector.Zero;
		bitmap = null;
		InvalidateVisual ();
	}

	private void LoadRasterAnnotations (ZipArchive archive)
	{
		ZipArchiveEntry? metadata = archive.GetEntry ("pintora/annotations.xml");
		if (metadata is null)
			return;
		XElement root = XElement.Parse (new StreamReader (metadata.Open ()).ReadToEnd ());
		foreach (XElement node in root.Elements ("annotation")) {
			if (!int.TryParse (node.Attribute ("layer")?.Value, out int layerIndex) || (uint) layerIndex >= (uint) Layers.Count)
				continue;
			// Shape metadata was written by an earlier Avalonia version, but
			// shapes are raster content in the GTK/Pinta contract. Ignore the
			// legacy duplicate and use the pixels from the layer PNG instead.
			if (node.Attribute ("type")?.Value == "shape")
				continue;
			if (node.Attribute ("type")?.Value == "effect" && TryReadAnnotationRect (node, out Rect effectBounds)
				&& Enum.TryParse (node.Attribute ("effect")?.Value, out RegionEffectKind effectKind)) {
				double.TryParse (node.Attribute ("strength")?.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double strength);
				int.TryParse (node.Attribute ("pixel-size")?.Value, out int pixelSize);
				double.TryParse (node.Attribute ("rotation")?.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double rotation);
				Layers[layerIndex].Annotations.Add (new RegionEffectAnnotation {
					Bounds = effectBounds,
					Rotation = rotation,
					Effect = effectKind,
					Strength = Math.Clamp (strength, 0, 1),
					PixelSize = Math.Clamp (pixelSize, 2, 64)
				});
				continue;
			}
			if (node.Attribute ("type")?.Value == "text" && TryReadAnnotationRect (node, out Rect textBounds)) {
				double.TryParse (node.Attribute ("font-size")?.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double fontSize);
				double.TryParse (node.Attribute ("rotation")?.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double rotation);
				Layers[layerIndex].Annotations.Add (new TextAnnotation {
					Bounds = textBounds,
					Rotation = rotation,
					Text = node.Attribute ("text")?.Value ?? string.Empty,
					FontFamily = node.Attribute ("font")?.Value ?? "Inter",
					FontSize = fontSize > 0 ? fontSize : 16,
					Color = ParseAnnotationColor (node.Attribute ("color")?.Value),
					Underline = node.Attribute ("underline")?.Value == "true",
					FontWeight = node.Attribute ("bold")?.Value == "true" ? FontWeight.Bold : FontWeight.Normal
				});
				continue;
			}
			if (node.Attribute ("type")?.Value == "shape" && TryReadAnnotationRect (node, out Rect shapeBounds)
				&& Enum.TryParse (node.Attribute ("kind")?.Value, out ShapeKind shapeKind)
				&& TryReadAnnotationPoint (node, "from", out PointD from) && TryReadAnnotationPoint (node, "to", out PointD to)) {
				double.TryParse (node.Attribute ("rotation")?.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double rotation);
				int.TryParse (node.Attribute ("outline-width")?.Value, out int outlineWidth);
				int.TryParse (node.Attribute ("corner-radius")?.Value, out int cornerRadius);
				Layers[layerIndex].Annotations.Add (new ShapeAnnotation {
					Bounds = shapeBounds,
					Rotation = rotation,
					Kind = shapeKind,
					From = from,
					To = to,
					Outline = ParseAnnotationColor (node.Attribute ("outline")?.Value),
					Fill = ParseAnnotationColor (node.Attribute ("fill")?.Value),
					FillStyle = Enum.TryParse (node.Attribute ("fill-style")?.Value, out ShapeFillStyle fillStyle) ? fillStyle : ShapeFillStyle.Outline,
					OutlineWidth = Math.Max (1, outlineWidth),
					Dash = Enum.TryParse (node.Attribute ("dash")?.Value, out ShapeDashStyle dash) ? dash : ShapeDashStyle.Solid,
					CornerRadius = Math.Max (0, cornerRadius),
					Antialias = node.Attribute ("antialias")?.Value != "false"
				});
				continue;
			}
			string? sourceId = node.Attribute ("source")?.Value;
			string? sourcePath = node.Attribute ("file")?.Value;
			ZipArchiveEntry? sourceEntry = sourcePath is null ? null : archive.GetEntry (sourcePath);
			if (sourceId is null || sourceEntry is null || !TryReadAnnotationRect (node, out Rect bounds))
				continue;
			MemoryImageSurface source = DecodeSurface (new Bitmap (sourceEntry.Open ()));
			RegisterAnnotationSource (sourceId, source);
			owned_annotation_sources.Add (source);
			if (node.Attribute ("type")?.Value == "image") {
				double.TryParse (node.Attribute ("opacity")?.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double opacity);
				double.TryParse (node.Attribute ("rotation")?.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double rotation);
				Layers[layerIndex].Annotations.Add (new ImageAnnotation { SourceId = sourceId, Bounds = bounds, Rotation = rotation, Opacity = opacity > 0 ? opacity : 1, PreserveAspectRatio = node.Attribute ("preserve")?.Value != "False" });
			} else if (node.Attribute ("type")?.Value == "cursor") {
				double.TryParse (node.Attribute ("scale")?.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double scale);
				double.TryParse (node.Attribute ("rotation")?.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double rotation);
				int.TryParse (node.Attribute ("hotspot-x")?.Value, out int hotspotX);
				int.TryParse (node.Attribute ("hotspot-y")?.Value, out int hotspotY);
				Layers[layerIndex].Annotations.Add (new CursorAnnotation { CursorId = sourceId, Bounds = bounds, Rotation = rotation, Scale = scale > 0 ? scale : 1, Tint = ParseAnnotationColor (node.Attribute ("tint")?.Value), HotspotX = hotspotX, HotspotY = hotspotY });
			}
		}
	}

	private static bool TryReadAnnotationRect (XElement node, out Rect bounds)
	{
		bounds = default;
		if (!double.TryParse (node.Attribute ("x")?.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double x) || !double.TryParse (node.Attribute ("y")?.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double y) || !double.TryParse (node.Attribute ("width")?.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double width) || !double.TryParse (node.Attribute ("height")?.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double height))
			return false;
		bounds = new Rect (x, y, width, height);
		return true;
	}

	private static bool TryReadAnnotationPoint (XElement node, string prefix, out PointD point)
	{
		point = default;
		if (!double.TryParse (node.Attribute ($"{prefix}-x")?.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double x)
			|| !double.TryParse (node.Attribute ($"{prefix}-y")?.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double y))
			return false;
		point = new PointD (x, y);
		return true;
	}

	private static ColorBgra ParseAnnotationColor (string? value)
	{
		if (string.IsNullOrWhiteSpace (value) || value.Length != 8 || !uint.TryParse (value, System.Globalization.NumberStyles.HexNumber, null, out uint rgba))
			return ColorBgra.White;
		return ColorBgra.FromBgra ((byte) rgba, (byte) (rgba >> 8), (byte) (rgba >> 16), (byte) (rgba >> 24));
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
			// MemoryImageSurface stores straight-alpha ColorBgra values. Keep the
			// decoded framebuffer in the same representation before copying pixels
			// into the editable layer; premultiplied RGB would produce dark halos
			// around translucent imported images.
			using var decoded = new WriteableBitmap (size, new Vector (96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
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
		document_session.MarkSaved (path);
	}

	public void SaveImage (string path)
	{
		EditorExportFormat format;
		try { format = EditorExportService.GetFormat (path); }
		catch (ArgumentException) { format = EditorExportFormat.Png; }
		switch (format) {
			case EditorExportFormat.Jpeg:
				SaveJpeg (path);
				break;
			case EditorExportFormat.WebP:
				SaveWebp (path);
				break;
			case EditorExportFormat.Tiff:
				SaveTiff (path);
				break;
			case EditorExportFormat.OpenRaster:
				SaveOra (path);
				break;
			default:
				SavePng (path);
				break;
		}
		document_session.MarkSaved (path);
	}

	private void SetClean ()
		=> document_session.MarkClean ();

	public void SaveJpeg (string path, int quality = 95)
	{
		SaveBitmap (path, new JpegBitmapEncoderOptions { Quality = Math.Clamp (quality, 1, 100) });
		document_session.MarkSaved (path);
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
		using (FileStream stream = File.Create (path))
			encoded.SaveTo (stream);
		document_session.MarkSaved (path);
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
		document_session.MarkSaved (path);
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
		SaveRasterAnnotations (archive);
		document_session.MarkSaved (path);
	}

	private void SaveRasterAnnotations (ZipArchive archive)
	{
		var xml = new StringBuilder ("<annotations>");
		int sourceIndex = 0;
		bool hasMetadata = false;
		for (int layerIndex = 0; layerIndex < Layers.Count; layerIndex++) {
			foreach (Annotation annotation in Layers[layerIndex].Annotations.Items) {
				// Shapes follow the GTK/Pinta raster contract: they are baked into the
				// layer PNG during ORA export, so do not serialize a second editable
				// copy that could diverge after reopening the document.
				string? type = annotation switch { ImageAnnotation => "image", CursorAnnotation => "cursor", RegionEffectAnnotation => "effect", TextAnnotation => "text", _ => null };
				if (type is null)
					continue;
				if (annotation is RegionEffectAnnotation effectAnnotation) {
					hasMetadata = true;
					string effectAttributes = $"type=\"effect\" layer=\"{layerIndex}\" effect=\"{effectAnnotation.Effect}\" x=\"{effectAnnotation.Bounds.X.ToString (System.Globalization.CultureInfo.InvariantCulture)}\" y=\"{effectAnnotation.Bounds.Y.ToString (System.Globalization.CultureInfo.InvariantCulture)}\" width=\"{effectAnnotation.Bounds.Width.ToString (System.Globalization.CultureInfo.InvariantCulture)}\" height=\"{effectAnnotation.Bounds.Height.ToString (System.Globalization.CultureInfo.InvariantCulture)}\" rotation=\"{effectAnnotation.Rotation.ToString (System.Globalization.CultureInfo.InvariantCulture)}\" strength=\"{effectAnnotation.Strength.ToString (System.Globalization.CultureInfo.InvariantCulture)}\" pixel-size=\"{effectAnnotation.PixelSize}\"";
					xml.Append ($"<annotation {effectAttributes} />");
					continue;
				}
				if (annotation is TextAnnotation textAnnotation) {
					hasMetadata = true;
					string textAttributes = $"type=\"text\" layer=\"{layerIndex}\" text=\"{System.Security.SecurityElement.Escape (textAnnotation.Text)}\" font=\"{System.Security.SecurityElement.Escape (textAnnotation.FontFamily)}\" font-size=\"{textAnnotation.FontSize.ToString (System.Globalization.CultureInfo.InvariantCulture)}\" color=\"{textAnnotation.Color.R:X2}{textAnnotation.Color.G:X2}{textAnnotation.Color.B:X2}{textAnnotation.Color.A:X2}\" bold=\"{(textAnnotation.FontWeight == FontWeight.Bold || textAnnotation.FontWeight == FontWeight.SemiBold ? "true" : "false")}\" underline=\"{textAnnotation.Underline.ToString ().ToLowerInvariant ()}\" x=\"{textAnnotation.Bounds.X.ToString (System.Globalization.CultureInfo.InvariantCulture)}\" y=\"{textAnnotation.Bounds.Y.ToString (System.Globalization.CultureInfo.InvariantCulture)}\" width=\"{textAnnotation.Bounds.Width.ToString (System.Globalization.CultureInfo.InvariantCulture)}\" height=\"{textAnnotation.Bounds.Height.ToString (System.Globalization.CultureInfo.InvariantCulture)}\" rotation=\"{textAnnotation.Rotation.ToString (System.Globalization.CultureInfo.InvariantCulture)}\"";
					xml.Append ($"<annotation {textAttributes} />");
					continue;
				}
				string sourceId = annotation is ImageAnnotation image ? image.SourceId : ((CursorAnnotation) annotation).CursorId;
				if (!annotation_sources.TryGetValue (sourceId, out IImageSurface? source))
					continue;
				string sourcePath = $"data/annotation{sourceIndex++}.png";
				WritePngEntry (archive, sourcePath, source);
				hasMetadata = true;
				string attributes = $"type=\"{type}\" layer=\"{layerIndex}\" source=\"{System.Security.SecurityElement.Escape (sourceId)}\" file=\"{sourcePath}\" x=\"{annotation.Bounds.X.ToString (System.Globalization.CultureInfo.InvariantCulture)}\" y=\"{annotation.Bounds.Y.ToString (System.Globalization.CultureInfo.InvariantCulture)}\" width=\"{annotation.Bounds.Width.ToString (System.Globalization.CultureInfo.InvariantCulture)}\" height=\"{annotation.Bounds.Height.ToString (System.Globalization.CultureInfo.InvariantCulture)}\" rotation=\"{annotation.Rotation.ToString (System.Globalization.CultureInfo.InvariantCulture)}\"";
				if (annotation is ImageAnnotation imageAnnotation)
					attributes += $" opacity=\"{imageAnnotation.Opacity.ToString (System.Globalization.CultureInfo.InvariantCulture)}\" preserve=\"{imageAnnotation.PreserveAspectRatio}\"";
				else if (annotation is CursorAnnotation cursorAnnotation)
					attributes += $" scale=\"{cursorAnnotation.Scale.ToString (System.Globalization.CultureInfo.InvariantCulture)}\" tint=\"{cursorAnnotation.Tint.R:X2}{cursorAnnotation.Tint.G:X2}{cursorAnnotation.Tint.B:X2}{cursorAnnotation.Tint.A:X2}\" hotspot-x=\"{cursorAnnotation.HotspotX}\" hotspot-y=\"{cursorAnnotation.HotspotY}\"";
				xml.Append ($"<annotation {attributes} />");
			}
		}
		xml.Append ("</annotations>");
		if (hasMetadata)
			WriteTextEntry (archive, "pintora/annotations.xml", xml.ToString ());
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
		MarkCanvasChanged (true);
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
		MarkCanvasChanged (true);
	}

	public void PaintPencilLine (PointD from, PointD to, ColorBgra color)
	{
		if (IsActiveLayerLocked || !pencil_tool.DrawPencilLine (Surface, from, to, color, BrushOpacity))
			return;
		MarkCanvasChanged (true);
	}

	public bool CanUndoStroke => (undo_stack.Count > 0 && Layers.Contains (undo_stack.Peek ().Layer)) || CanUndoLayerOperation;
	public bool CanRedoStroke => (redo_stack.Count > 0 && Layers.Contains (redo_stack.Peek ().Layer)) || CanRedoLayerOperation;
	public IReadOnlyList<string> History => history_kinds.UndoItems.Select (kind => kind == HistoryKind.Layer ? "Layer operation" : "Raster operation").ToArray ();
	public event EventHandler? HistoryChanged;

	public void ClearHistory ()
	{
		undo_stack.Clear ();
		redo_stack.Clear ();
		layer_undo_stack.Clear ();
		layer_redo_stack.Clear ();
		history_kinds.Clear ();
		redo_history_kinds.Clear ();
		has_current_stroke = false;
		HistoryChanged?.Invoke (this, EventArgs.Empty);
	}

	public void BeginStroke ()
	{
		if (Surface is null)
			return;

		undo_stack.Push (CaptureRasterSnapshot (session.ActiveLayer!, shape_tool.CaptureState ()));
		redo_stack.Clear ();
		history_kinds.Push (HistoryKind.Raster);
		redo_history_kinds.Clear ();
		has_current_stroke = true;
		HistoryChanged?.Invoke (this, EventArgs.Empty);
	}

	public void CancelCurrentStroke ()
	{
		CancelDraggedAnnotation ();
		tool_set.CancelPointerActions ();
		pointer_action_tool = null;
		is_painting = false;
		last_canvas_point = null;
		if (!has_current_stroke || undo_stack.Count == 0) {
			InvalidateVisual ();
			return;
		}
		RasterSnapshot snapshot = undo_stack.Pop ();
		if (history_kinds.Count > 0 && history_kinds.Peek () == HistoryKind.Raster)
			history_kinds.Pop ();
		RestoreRasterSnapshot (snapshot);
		shape_tool.RestoreState (snapshot.Shape);
		ReconcileAnnotationSelection (snapshot.Layer);
		has_current_stroke = false;
		bitmap = null;
		InvalidateVisual ();
		HistoryChanged?.Invoke (this, EventArgs.Empty);
	}

	private void CancelDraggedAnnotation ()
	{
		if (dragged_annotation is null || drag_undo_snapshot is null)
			return;

		// Annotation dragging is a layer operation, not a raster stroke. Restore
		// its pre-drag snapshot when Escape cancels the pointer action; otherwise
		// the updated bounds would remain in the layer without an undo entry.
		if (drag_changed)
			RestoreLayerStack (drag_undo_snapshot);
		dragged_annotation = null;
		drag_undo_snapshot = null;
		drag_changed = false;
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
		MarkCanvasChanged (true);
		CommitStroke ();
		return true;
	}

	public void UndoLastStroke ()
	{
		if (undo_stack.Count == 0 || !Layers.Contains (undo_stack.Peek ().Layer)) {
			if (history_kinds.Count > 0 && history_kinds.Peek () == HistoryKind.Layer) {
				UndoLayerOperationCore (true);
				return;
			}
			UndoLayerOperation ();
			return;
		}
		if (!CanUndoStroke)
			return;

		RasterSnapshot snapshot = undo_stack.Pop ();
		AvaloniaLayer layer = snapshot.Layer;
		redo_stack.Push (CaptureRasterSnapshot (layer, shape_tool.CaptureState ()));
		RestoreRasterSnapshot (snapshot);
		layer.Annotations.Clear ();
		layer.Annotations.AddRange (snapshot.Annotations);
		shape_tool.RestoreState (snapshot.Shape);
		ReconcileAnnotationSelection (layer);
		if (history_kinds.Count > 0)
			history_kinds.Pop ();
		redo_history_kinds.Push (HistoryKind.Raster);
		bitmap = null;
		InvalidateVisual ();
		RaiseLayerContentChanged (layer);
		HistoryChanged?.Invoke (this, EventArgs.Empty);
	}

	public void RedoLastStroke ()
	{
		if (redo_stack.Count == 0 || !Layers.Contains (redo_stack.Peek ().Layer)) {
			if (redo_history_kinds.Count > 0 && redo_history_kinds.Peek () == HistoryKind.Layer) {
				RedoLayerOperationCore (true);
				return;
			}
			RedoLayerOperation ();
			return;
		}
		if (!CanRedoStroke)
			return;

		RasterSnapshot snapshot = redo_stack.Pop ();
		AvaloniaLayer layer = snapshot.Layer;
		undo_stack.Push (CaptureRasterSnapshot (layer, shape_tool.CaptureState ()));
		RestoreRasterSnapshot (snapshot);
		layer.Annotations.Clear ();
		layer.Annotations.AddRange (snapshot.Annotations);
		shape_tool.RestoreState (snapshot.Shape);
		ReconcileAnnotationSelection (layer);
		if (redo_history_kinds.Count > 0)
			redo_history_kinds.Pop ();
		history_kinds.Push (HistoryKind.Raster);
		bitmap = null;
		InvalidateVisual ();
		RaiseLayerContentChanged (layer);
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

		PointD canvasPoint = GetCoordinateMapper ().ViewToCanvas (viewPoint);
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
		if (selected_annotation is not null) {
			var annotation_pen = new Pen (Brushes.DodgerBlue, 1.5, new DashStyle (new[] { 5d, 3d }, 0));
			Point[] corners = GetAnnotationDisplayCorners (selected_annotation);
			for (int i = 0; i < corners.Length; i++) {
				Point from = origin + new Vector (corners[i].X * Scale, corners[i].Y * Scale);
				Point to = origin + new Vector (corners[(i + 1) % corners.Length].X * Scale, corners[(i + 1) % corners.Length].Y * Scale);
				context.DrawLine (annotation_pen, from, to);
			}
			foreach (Point corner in corners.Select (point => origin + new Vector (point.X * Scale, point.Y * Scale))) {
				var handle = new Rect (corner.X - 4, corner.Y - 4, 8, 8);
				context.FillRectangle (Brushes.White, handle);
				context.DrawRectangle (new Pen (Brushes.DodgerBlue, 1), handle);
			}
		}
		if (ActiveTool is CanvasTool.Blur or CanvasTool.Pixelate or CanvasTool.Highlight or CanvasTool.Magnify or CanvasTool.Spotlight) {
			EffectTool effect = effect_tool;
			if (effect.IsCreating) {
				double left = Math.Min (effect.Start.X, effect.Current.X);
				double top = Math.Min (effect.Start.Y, effect.Current.Y);
				var previewBrush = new SolidColorBrush (ActiveTool == CanvasTool.Highlight
					? Color.FromArgb (55, 255, 220, 0) : Color.FromArgb (45, 80, 140, 255));
				context.FillRectangle (previewBrush, new Rect (origin.X + left * Scale, origin.Y + top * Scale,
					Math.Abs (effect.Current.X - effect.Start.X) * Scale, Math.Abs (effect.Current.Y - effect.Start.Y) * Scale));
				context.DrawRectangle (new Pen (Brushes.DodgerBlue, 1, new DashStyle (new[] { 4d, 4d }, 0)),
					new Rect (origin.X + left * Scale, origin.Y + top * Scale,
						Math.Abs (effect.Current.X - effect.Start.X) * Scale, Math.Abs (effect.Current.Y - effect.Start.Y) * Scale));
			}
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

	private static Rect GetAnnotationDisplayBounds (Annotation annotation)
	{
		if (annotation is not CursorAnnotation cursor)
			return annotation.Bounds;
		double scale = Math.Max (0.01, cursor.Scale);
		return new Rect (annotation.Bounds.X - cursor.HotspotX * scale, annotation.Bounds.Y - cursor.HotspotY * scale, annotation.Bounds.Width * scale, annotation.Bounds.Height * scale);
	}

	private static Point[] GetAnnotationDisplayCorners (Annotation annotation)
	{
		Rect bounds = GetAnnotationDisplayBounds (annotation);
		Point center = bounds.Center;
		double cosine = Math.Cos (annotation.Rotation);
		double sine = Math.Sin (annotation.Rotation);
		Point[] corners = [
			new Point (bounds.Left, bounds.Top),
			new Point (bounds.Right, bounds.Top),
			new Point (bounds.Right, bounds.Bottom),
			new Point (bounds.Left, bounds.Bottom)
		];
		for (int i = 0; i < corners.Length; i++) {
			double dx = corners[i].X - center.X;
			double dy = corners[i].Y - center.Y;
			corners[i] = new Point (center.X + dx * cosine - dy * sine, center.Y + dx * sine + dy * cosine);
		}
		return corners;
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
			IImageSurface sourceSurface = ReferenceEquals (layer, session.ActiveLayer) && activeLayerPreview is not null
				? activeLayerPreview : layer.Surface;
			using var layerComposite = new MemoryImageSurface (ImageSize.Width, ImageSize.Height);
			sourceSurface.GetReadOnlyPixelData ().CopyTo (layerComposite.GetPixelData ());
			RenderNonRasterAnnotations (layerComposite, layer.Annotations.Items);
			ReadOnlySpan<ColorBgra> source = layerComposite.GetReadOnlyPixelData ();
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

	private void RenderNonRasterAnnotations (MemoryImageSurface surface, IReadOnlyList<Annotation> annotations)
	{
		foreach (Annotation annotation in annotations) {
			switch (annotation) {
				case RegionEffectAnnotation effect:
					AnnotationRenderer.Render (surface, effect);
					break;
				// Shapes follow the GTK/Pinta raster contract and are already baked
				// into the layer surface. Text remains non-destructive so it can be
				// moved without leaving stale pixels behind.
				case TextAnnotation text:
					AnnotationRenderer.Render (surface, text);
					break;
				case ImageAnnotation image when annotation_sources.TryGetValue (image.SourceId, out IImageSurface? imageSource):
					AnnotationRenderer.Render (surface, image, imageSource);
					break;
				case CursorAnnotation cursor when annotation_sources.TryGetValue (cursor.CursorId, out IImageSurface? cursorSource):
					AnnotationRenderer.Render (surface, cursor, cursorSource);
					break;
			}
		}
	}

	private void DrawSurface (DrawingContext context, Point origin, IImageSurface displaySurface)
	{
		IImageSurface surface = displaySurface;
		if (bitmap is null || bitmap.PixelSize.Width != surface.Width || bitmap.PixelSize.Height != surface.Height)
			bitmap = CreateBitmap (surface);
		else
			UpdateBitmap (bitmap, surface);

		context.DrawImage (bitmap, new Rect (0, 0, surface.Width, surface.Height), new Rect (origin, new global::Avalonia.Size (ViewSize.Width, ViewSize.Height)));
	}

	private static WriteableBitmap CreateBitmap (IImageSurface surface)
	{
		// ColorBgra stores straight-alpha channels. Declaring this buffer as
		// premultiplied makes translucent pixels appear too bright/dark when
		// Avalonia composites the canvas over the checkerboard.
		var image = new WriteableBitmap (new PixelSize (surface.Width, surface.Height), new Vector (96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
		UpdateBitmap (image, surface);
		return image;
	}

	private static void UpdateBitmap (WriteableBitmap image, IImageSurface surface)
	{
		using ILockedFramebuffer locked = image.Lock ();
		ReadOnlySpan<byte> source = MemoryMarshal.AsBytes (surface.GetReadOnlyPixelData ());
		byte[] sourceBytes = source.ToArray ();
		int rowBytes = surface.Width * ColorBgra.SizeOf;
		for (int y = 0; y < surface.Height; y++)
			Marshal.Copy (sourceBytes, y * rowBytes, locked.Address + y * locked.RowBytes, rowBytes);
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
		last_canvas_point = GetCoordinateMapper ().ViewToCanvas (position);
		if (ActiveTool == CanvasTool.Text && SelectAnnotationAt (last_canvas_point.Value)
			&& selected_annotation is TextAnnotation) {
			e.Handled = true;
			return;
		}
		if (ActiveTool == CanvasTool.Selection && properties.IsLeftButtonPressed && SelectAnnotationAt (last_canvas_point.Value)) {
			dragged_annotation = selected_annotation;
			drag_start_point = last_canvas_point.Value;
			drag_start_bounds = dragged_annotation!.Bounds;
			drag_undo_snapshot = CaptureLayerStack ();
			drag_changed = false;
			e.Pointer.Capture (this);
			e.Handled = true;
			return;
		}
		CanvasToolPointerContext context = new (this, position, last_canvas_point.Value, last_canvas_point.Value,
			stroke_color, properties.IsRightButtonPressed, false);
		bool handled = tool_set.Get<ICanvasTool> (ActiveTool).PointerPressed (context);
		if (ActiveTool == CanvasTool.Zoom) {
			e.Pointer.Capture (null);
			e.Handled = handled;
			return;
		}
		if (ActiveTool is CanvasTool.Selection or CanvasTool.Shapes or CanvasTool.Gradient
				or CanvasTool.Brush or CanvasTool.Pencil or CanvasTool.Eraser or CanvasTool.Text or CanvasTool.Blur
			or CanvasTool.Pixelate or CanvasTool.Highlight or CanvasTool.Magnify or CanvasTool.Spotlight) {
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
		MarkCanvasChanged (true);
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
			PointD point = GetCoordinateMapper ().ViewToCanvas (position);
			PointD previous = last_canvas_point ?? point;
			tool_set.Get<ICanvasTool> (pointer_action_tool ?? ActiveTool).PointerReleased (
				new CanvasToolPointerContext (this, position, point, previous, stroke_color, false, false));
			if (pointer_action_tool == CanvasTool.Selection)
				SyncSelectionAnnotation ();
		}
		if (dragged_annotation is not null) {
			CommitDraggedAnnotationUndo ();
			dragged_annotation = null;
			drag_undo_snapshot = null;
			drag_changed = false;
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
		Point position = e.GetPosition (this);
		Point origin = GetCanvasOrigin ();
		PointD current = GetCoordinateMapper ().ViewToCanvas (position);
		PointD bounded = new (Math.Clamp (current.X, 0, ImageSize.Width), Math.Clamp (current.Y, 0, ImageSize.Height));
		if (pointer_position != bounded) {
			pointer_position = bounded;
			PointerPositionChanged?.Invoke (this, EventArgs.Empty);
		}
		if (!is_painting) {
			if (dragged_annotation is not null) {
				UpdateDraggedAnnotation (dragged_annotation, new Vector (current.X - drag_start_point.X, current.Y - drag_start_point.Y));
				e.Handled = true;
			}
			return;
		}
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
			ClearAnnotationSelection ();
			selection_tool.CancelSelection ();
			if (shape_tool.IsEditing)
				shape_tool.CancelEdit (CancelCurrentStroke);
			else
				CancelCurrentStroke ();
			e.Handled = true;
		} else if (selected_annotation is not null && (e.Key is Key.Delete or Key.Back)) {
			if (RemoveAnnotation (selected_annotation))
				ClearAnnotationSelection ();
			e.Handled = true;
		} else if (selected_annotation is not null && (e.Key is Key.Left or Key.Right or Key.Up or Key.Down)) {
			int step = (e.KeyModifiers & KeyModifiers.Shift) != 0 ? 10 : 1;
			Vector delta = e.Key switch {
				Key.Left => new Vector (-step, 0),
				Key.Right => new Vector (step, 0),
				Key.Up => new Vector (0, -step),
				_ => new Vector (0, step)
			};
			MoveAnnotation (selected_annotation, delta);
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
		} else if (keyboard.Matches (e, PintoraKeyCommands.ClearHistory)) {
			ClearHistory ();
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
		} else if (keyboard.Matches (e, PintoraKeyCommands.DuplicateAnnotation)) {
			DuplicateSelectedAnnotation ();
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
		} else if (keyboard.Matches (e, PintoraKeyCommands.Text)) {
			ActiveTool = CanvasTool.Text; e.Handled = true;
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

	private CanvasCoordinateMapper GetCoordinateMapper ()
		=> new (GetCanvasOrigin (), session.Scale);

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
		MarkCanvasChanged (true);
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
		MarkCanvasChanged (true);
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
