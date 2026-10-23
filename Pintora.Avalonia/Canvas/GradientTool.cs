using System;
using Avalonia;
using Avalonia.Media;
using Pinta.Core;
using Pintora.Avalonia.Imaging;

namespace Pintora.Avalonia.Canvas;

internal sealed class GradientTool : ICanvasTool
{
	public CanvasHost.CanvasTool Kind => CanvasHost.CanvasTool.Gradient;
	private PointD start;
	private PointD end;
	private ColorBgra primary;
	private ColorBgra secondary;
	private bool reversed;
	public bool IsActive { get; private set; }

	public bool PointerPressed (CanvasToolPointerContext context)
	{
		CanvasHost host = context.Host;
		if (host.IsActiveLayerLocked || host.Surface is null)
			return false;
		Begin (context.Point, host.BrushColor, host.SecondaryBrushColor, host.GradientReversed);
		return true;
	}

	public bool PointerMoved (CanvasToolPointerContext context)
	{
		if (!IsActive)
			return false;
		Update (context.Point);
		context.Host.InvalidateToolVisual ();
		return true;
	}

	public bool PointerReleased (CanvasToolPointerContext context)
	{
		if (!IsActive)
			return false;
		Complete (context.Host.Surface, context.Host.BeginStroke, context.Host.CommitStroke);
		context.Host.MarkCanvasChanged ();
		return true;
	}

	public void CancelPointerAction () => Cancel ();

	public void Begin (PointD point, ColorBgra primaryColor, ColorBgra secondaryColor, bool reverse)
	{
		start = end = point;
		primary = primaryColor;
		secondary = secondaryColor;
		reversed = reverse;
		IsActive = true;
	}

	public void Update (PointD point)
	{
		if (IsActive)
			end = point;
	}

	public void DrawGuide (DrawingContext context, Point origin, double scale, double thickness)
	{
		if (!IsActive)
			return;
		var pen = new Pen (new SolidColorBrush (global::Avalonia.Media.Color.FromArgb (primary.A, primary.R, primary.G, primary.B)), thickness);
		Point first = origin + new Vector (start.X * scale, start.Y * scale);
		Point last = origin + new Vector (end.X * scale, end.Y * scale);
		context.DrawLine (pen, first, last);
	}

	public bool Complete (IImageSurface? surface, Action beginStroke, Action commitStroke)
	{
		if (!IsActive || surface is null)
			return false;
		beginStroke ();
		Apply (surface, start, end, primary, secondary, reversed);
		IsActive = false;
		commitStroke ();
		return true;
	}

	public void Cancel () => IsActive = false;

	public static void Apply (IImageSurface surface, PointD from, PointD to,
		ColorBgra primaryColor, ColorBgra secondaryColor, bool reverse)
	{
		double dx = to.X - from.X, dy = to.Y - from.Y;
		double lengthSquared = Math.Max (dx * dx + dy * dy, 1);
		Span<ColorBgra> pixels = surface.GetPixelData ();
		for (int y = 0; y < surface.Height; y++)
			for (int x = 0; x < surface.Width; x++) {
				double t = Math.Clamp (((x - from.X) * dx + (y - from.Y) * dy) / lengthSquared, 0, 1);
				if (reverse) t = 1 - t;
				pixels[y * surface.Width + x] = ColorBgra.FromBgra (
					(byte) Math.Round (primaryColor.B + (secondaryColor.B - primaryColor.B) * t),
					(byte) Math.Round (primaryColor.G + (secondaryColor.G - primaryColor.G) * t),
					(byte) Math.Round (primaryColor.R + (secondaryColor.R - primaryColor.R) * t),
					(byte) Math.Round (primaryColor.A + (secondaryColor.A - primaryColor.A) * t));
			}
	}
}
