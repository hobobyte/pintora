using System;
using Avalonia;
using Avalonia.Media;
using Pinta.Core;
using Pintora.Avalonia.Document;
using Pintora.Avalonia.Imaging;

namespace Pintora.Avalonia.Canvas;

internal sealed class ShapeTool : ICanvasTool
{
	public CanvasHost.CanvasTool Kind => CanvasHost.CanvasTool.Shapes;
	internal sealed class ShapeState
	{
		public required AvaloniaLayer Layer { get; init; }
		public required ColorBgra[] BasePixels { get; init; }
		public CanvasHost.ShapeKind Kind { get; init; }
		public PointD From { get; set; }
		public PointD To { get; set; }
		public ColorBgra Outline { get; init; }
		public ColorBgra Fill { get; init; }
		public CanvasHost.ShapeFillStyle FillStyle { get; init; }
		public int Width { get; init; }
		public CanvasHost.ShapeDashStyle Dash { get; init; }
		public int CornerRadius { get; init; }
		public bool Antialias { get; init; }
		public double Rotation { get; set; }
	}

	internal enum HitKind { None, Move, Resize, Rotate }
	internal readonly record struct HitTarget (HitKind Kind, int HandleIndex = -1);
	public ShapeState? SelectedShape { get; set; }
	public bool IsEditing { get; private set; }
	public bool IsCreating { get; private set; }
	private bool moving;
	private bool rotating;
	private int resizeHandle = -1;
	private PointD pointerStart;
	private PointD initialFrom;
	private PointD initialTo;
	private PointD currentFrom;
	private PointD currentTo;
	private ColorBgra[]? editBasePixels;
	private double currentRotation;
	private double aspectRatio = 1;
	private PointD creationStart;
	private PointD creationEnd;
	private ColorBgra[]? creationBasePixels;
	private CanvasHost.ShapeKind creationKind;
	private ColorBgra creationOutline;
	private ColorBgra creationFill;
	private CanvasHost.ShapeFillStyle creationFillStyle;
	private CanvasHost.ShapeDashStyle creationDash;
	private int creationWidth;
	private int creationCornerRadius;
	private bool creationAntialias;

	public bool PointerPressed (CanvasToolPointerContext context)
	{
		CanvasHost host = context.Host;
		if (host.IsActiveLayerLocked)
			return false;
		AvaloniaLayer? layer = host.ActiveLayerIndex >= 0 && host.ActiveLayerIndex < host.Layers.Count
			? host.Layers[host.ActiveLayerIndex] : null;
		return BeginPointerAction (context.Point, layer, host.Surface as MemoryImageSurface,
			host.SelectedShape, context.Color,
			context.IsRightButton ? host.BrushColor : host.SecondaryBrushColor,
			host.FillStyle, host.ShapeOutlineWidth, host.DashStyle,
			host.ShapeCornerRadius, host.ShapeAntialias, host.BeginStroke);
	}

	public bool PointerMoved (CanvasToolPointerContext context)
	{
		if (IsEditing)
			UpdateEdit (context.Point, context.ShiftPressed);
		else if (IsCreating)
			UpdateCreation (context.Point);
		else
			return false;
		context.Host.InvalidateToolVisual ();
		return true;
	}

	public bool PointerReleased (CanvasToolPointerContext context)
	{
		CanvasHost host = context.Host;
		bool completed = IsEditing
			? CommitEdit (host.Surface as MemoryImageSurface, host.CommitStroke)
			: CompleteCreate (host.Surface as MemoryImageSurface,
				host.ActiveLayerIndex >= 0 && host.ActiveLayerIndex < host.Layers.Count ? host.Layers[host.ActiveLayerIndex] : null,
				host.CommitStroke);
		if (completed)
			host.MarkCanvasChanged ();
		return completed;
	}

	public void CancelPointerAction () => CancelCreate ();

	public bool BeginCreate (PointD point, MemoryImageSurface? surface, CanvasHost.ShapeKind kind,
		ColorBgra outline, ColorBgra fill, CanvasHost.ShapeFillStyle fillStyle, int width,
		CanvasHost.ShapeDashStyle dash, int cornerRadius, bool antialias)
	{
		if (surface is null)
			return false;
		creationStart = creationEnd = point;
		creationBasePixels = surface.GetReadOnlyPixelData ().ToArray ();
		creationKind = kind;
		creationOutline = outline;
		creationFill = fill;
		creationFillStyle = fillStyle;
		creationWidth = width;
		creationDash = dash;
		creationCornerRadius = cornerRadius;
		creationAntialias = antialias;
		IsCreating = true;
		SelectedShape = null;
		return true;
	}

	public bool BeginPointerAction (PointD point, AvaloniaLayer? activeLayer, MemoryImageSurface? surface,
		CanvasHost.ShapeKind kind, ColorBgra outline, ColorBgra fill, CanvasHost.ShapeFillStyle fillStyle,
		int width, CanvasHost.ShapeDashStyle dash, int cornerRadius, bool antialias, Action beginStroke)
	{
		if (surface is null)
			return false;
		if (BeginEdit (point, activeLayer, beginStroke))
			return true;
		beginStroke ();
		return BeginCreate (point, surface, kind, outline, fill, fillStyle, width, dash, cornerRadius, antialias);
	}

	public void UpdateCreation (PointD point)
	{
		if (IsCreating)
			creationEnd = point;
	}

	public void DrawCreationPreview (MemoryImageSurface preview)
	{
		if (!IsCreating || creationBasePixels is null)
			return;
		creationBasePixels.AsSpan ().CopyTo (preview.GetPixelData ());
		ShapeRasterizer.Draw (preview, creationKind, creationStart, creationEnd, creationOutline, creationFill,
			creationFillStyle, creationWidth, creationDash, creationCornerRadius, creationAntialias);
	}

	public bool CompleteCreate (MemoryImageSurface? surface, AvaloniaLayer? layer, Action commitStroke)
	{
		if (!IsCreating || surface is null || layer is null || creationBasePixels is null)
			return false;
		ShapeRasterizer.Draw (surface, creationKind, creationStart, creationEnd, creationOutline, creationFill,
			creationFillStyle, creationWidth, creationDash, creationCornerRadius, creationAntialias);
		SelectedShape = new ShapeState {
			Layer = layer, BasePixels = creationBasePixels, Kind = creationKind,
			From = creationStart, To = creationEnd, Outline = creationOutline, Fill = creationFill,
			FillStyle = creationFillStyle, Width = creationWidth, Dash = creationDash,
			CornerRadius = creationCornerRadius, Antialias = creationAntialias
		};
		ResetCreation ();
		commitStroke ();
		return true;
	}

	public void CancelCreate () => ResetCreation ();

	private void ResetCreation ()
	{
		IsCreating = false;
		creationBasePixels = null;
	}

	public void DrawPreview (MemoryImageSurface preview)
	{
		if (!IsEditing || SelectedShape is not ShapeState shape || editBasePixels is null)
			return;
		editBasePixels.AsSpan ().CopyTo (preview.GetPixelData ());
		ShapeRasterizer.DrawRotated (preview, shape.Kind, currentFrom, currentTo, shape.Outline, shape.Fill,
			shape.FillStyle, shape.Width, shape.Dash, shape.CornerRadius, shape.Antialias, currentRotation);
	}

	public bool BeginEdit (PointD point, AvaloniaLayer? activeLayer, Action beginStroke)
	{
		if (SelectedShape is not ShapeState shape || !ReferenceEquals (shape.Layer, activeLayer))
			return false;
		HitTarget hit = HitTest (point, shape.Kind, shape.From, shape.To, shape.Rotation);
		Rect bounds = new (Math.Min (shape.From.X, shape.To.X), Math.Min (shape.From.Y, shape.To.Y),
			Math.Abs (shape.To.X - shape.From.X), Math.Abs (shape.To.Y - shape.From.Y));
		if (hit.Kind == HitKind.None && (point.X < bounds.X || point.Y < bounds.Y || point.X > bounds.Right || point.Y > bounds.Bottom))
			return false;
		beginStroke ();
		IsEditing = true;
		moving = hit.Kind is HitKind.Move or HitKind.None;
		rotating = hit.Kind == HitKind.Rotate;
		resizeHandle = hit.Kind == HitKind.Resize ? hit.HandleIndex : -1;
		pointerStart = point;
		currentFrom = initialFrom = shape.From;
		currentTo = initialTo = shape.To;
		currentRotation = shape.Rotation;
		double height = Math.Abs (shape.To.Y - shape.From.Y);
		aspectRatio = height > 0 ? Math.Abs (shape.To.X - shape.From.X) / height : 1;
		editBasePixels = shape.BasePixels;
		return true;
	}

	public void UpdateEdit (PointD point, bool constrainAspect)
	{
		if (!IsEditing || SelectedShape is not ShapeState shape)
			return;
		if (rotating) {
			PointD center = Center (currentFrom, currentTo);
			currentRotation = Math.Atan2 (point.Y - center.Y, point.X - center.X) * 180 / Math.PI + 90;
		} else if (moving) {
			double dx = point.X - pointerStart.X, dy = point.Y - pointerStart.Y;
			currentFrom = new PointD (initialFrom.X + dx, initialFrom.Y + dy);
			currentTo = new PointD (initialTo.X + dx, initialTo.Y + dy);
		} else if (resizeHandle >= 0) {
			if (shape.Kind == CanvasHost.ShapeKind.Line) {
				if (resizeHandle == 0) currentFrom = point; else currentTo = point;
			} else {
				PointD fixedPoint = GetHandles (shape.Kind, shape.From, shape.To, shape.Rotation)[(resizeHandle + 2) % 4];
				if (constrainAspect && aspectRatio > 0) {
					double width = Math.Abs (point.X - fixedPoint.X), height = Math.Abs (point.Y - fixedPoint.Y);
					if (width / Math.Max (height, 0.001) > aspectRatio) height = width / aspectRatio;
					else width = height * aspectRatio;
					point = new PointD (point.X >= fixedPoint.X ? fixedPoint.X + width : fixedPoint.X - width,
						point.Y >= fixedPoint.Y ? fixedPoint.Y + height : fixedPoint.Y - height);
				}
				currentFrom = new PointD (resizeHandle is 0 or 3 ? point.X : fixedPoint.X, resizeHandle is 0 or 1 ? point.Y : fixedPoint.Y);
				currentTo = new PointD (resizeHandle is 0 or 3 ? fixedPoint.X : point.X, resizeHandle is 0 or 1 ? fixedPoint.Y : point.Y);
			}
		}
	}

	public bool CommitEdit (MemoryImageSurface? surface, Action commitStroke)
	{
		if (!IsEditing || SelectedShape is not ShapeState shape || surface is null || editBasePixels is null)
			return false;
		editBasePixels.AsSpan ().CopyTo (surface.GetPixelData ());
		ShapeRasterizer.DrawRotated (surface, shape.Kind, currentFrom, currentTo, shape.Outline, shape.Fill,
			shape.FillStyle, shape.Width, shape.Dash, shape.CornerRadius, shape.Antialias, currentRotation);
		shape.From = currentFrom;
		shape.To = currentTo;
		shape.Rotation = currentRotation;
		ResetEdit ();
		commitStroke ();
		return true;
	}

	public void CancelEdit (Action cancelStroke)
	{
		if (!IsEditing)
			return;
		ResetEdit ();
		cancelStroke ();
	}

	public bool MoveSelected (PointD delta, MemoryImageSurface? surface, bool layerLocked, Action beginStroke, Action commitStroke)
	{
		if (SelectedShape is not ShapeState shape || surface is null || layerLocked)
			return false;
		beginStroke ();
		shape.BasePixels.AsSpan ().CopyTo (surface.GetPixelData ());
		PointD from = new (shape.From.X + delta.X, shape.From.Y + delta.Y);
		PointD to = new (shape.To.X + delta.X, shape.To.Y + delta.Y);
		ShapeRasterizer.DrawRotated (surface, shape.Kind, from, to, shape.Outline, shape.Fill, shape.FillStyle,
			shape.Width, shape.Dash, shape.CornerRadius, shape.Antialias, shape.Rotation);
		shape.From = from;
		shape.To = to;
		commitStroke ();
		return true;
	}

	private void ResetEdit ()
	{
		IsEditing = moving = rotating = false;
		resizeHandle = -1;
		editBasePixels = null;
	}

	public void DrawHandles (DrawingContext context, Point origin, double scale, CanvasHost.ShapeKind kind,
		PointD from, PointD to, double rotation)
	{
		foreach (PointD point in GetHandles (kind, from, to, rotation)) {
			var rect = new Rect (origin.X + point.X * scale - 5, origin.Y + point.Y * scale - 5, 10, 10);
			context.FillRectangle (Brushes.White, rect);
			context.DrawRectangle (new Pen (Brushes.DodgerBlue, 1), rect);
		}
		if (kind == CanvasHost.ShapeKind.Line)
			return;
		PointD rotationHandle = GetRotationHandle (from, to, rotation);
		var handle = new Rect (origin.X + rotationHandle.X * scale - 5, origin.Y + rotationHandle.Y * scale - 5, 10, 10);
		context.FillRectangle (Brushes.White, handle);
		context.DrawRectangle (new Pen (Brushes.Orange, 1), handle);
	}

	public HitTarget HitTest (PointD point, CanvasHost.ShapeKind kind, PointD from, PointD to, double rotation)
	{
		if (kind != CanvasHost.ShapeKind.Line && DistanceSquared (point, GetRotationHandle (from, to, rotation)) <= 100)
			return new HitTarget (HitKind.Rotate);
		PointD[] handles = GetHandles (kind, from, to, rotation);
		for (int i = 0; i < handles.Length; i++)
			if (DistanceSquared (point, handles[i]) <= 64)
				return new HitTarget (HitKind.Resize, i);
		return new HitTarget (HitKind.None);
	}

	public PointD[] GetHandles (CanvasHost.ShapeKind kind, PointD from, PointD to, double rotation)
	{
		if (kind == CanvasHost.ShapeKind.Line)
			return [from, to];
		PointD[] points = [
			new (Math.Min (from.X, to.X), Math.Min (from.Y, to.Y)),
			new (Math.Max (from.X, to.X), Math.Min (from.Y, to.Y)),
			new (Math.Max (from.X, to.X), Math.Max (from.Y, to.Y)),
			new (Math.Min (from.X, to.X), Math.Max (from.Y, to.Y))
		];
		PointD center = Center (from, to);
		for (int i = 0; i < points.Length; i++)
			points[i] = RotatePoint (points[i], center, rotation);
		return points;
	}

	private static PointD GetRotationHandle (PointD from, PointD to, double rotation)
	{
		PointD center = Center (from, to);
		PointD top = new (center.X, Math.Min (from.Y, to.Y) - 24);
		return RotatePoint (top, center, rotation);
	}

	private static PointD Center (PointD from, PointD to)
		=> new ((from.X + to.X) / 2, (from.Y + to.Y) / 2);

	private static double DistanceSquared (PointD a, PointD b)
	{
		double dx = a.X - b.X, dy = a.Y - b.Y;
		return dx * dx + dy * dy;
	}

	public static PointD RotatePoint (PointD point, PointD center, double degrees)
	{
		double radians = degrees * Math.PI / 180;
		double x = point.X - center.X, y = point.Y - center.Y;
		return new PointD (center.X + x * Math.Cos (radians) - y * Math.Sin (radians),
			center.Y + x * Math.Sin (radians) + y * Math.Cos (radians));
	}
}
