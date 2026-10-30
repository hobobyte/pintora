using System;
using Pinta.Core;

namespace Pintora.Avalonia.Canvas;

internal sealed class BrushTool : ICanvasTool
{
	public BrushTool (CanvasHost.CanvasTool kind) => Kind = kind;
	public CanvasHost.CanvasTool Kind { get; }
	public bool IsPencil => Kind == CanvasHost.CanvasTool.Pencil;
	public bool IsEraser => Kind == CanvasHost.CanvasTool.Eraser;
	private bool pointer_active;
	private PointD previous_point;

	public bool PointerPressed (CanvasToolPointerContext context)
	{
		CanvasHost host = context.Host;
		if (!BeginStroke (host.Surface, context.Point, context.Color, host.BrushSize,
			host.BrushOpacity, host.BrushHardness, host.IsActiveLayerLocked, host.BeginStroke))
			return false;
		pointer_active = true;
		previous_point = context.Point;
		host.MarkCanvasChanged (true);
		return true;
	}

	public bool PointerMoved (CanvasToolPointerContext context)
	{
		if (!pointer_active)
			return false;
		CanvasHost host = context.Host;
		bool changed = ContinueStroke (host.Surface, previous_point, context.Point, context.Color,
			host.BrushSize, host.BrushOpacity, host.BrushHardness, host.IsActiveLayerLocked);
		previous_point = context.Point;
		if (changed)
			host.MarkCanvasChanged (true);
		return true;
	}

	public bool PointerReleased (CanvasToolPointerContext context)
	{
		bool wasActive = pointer_active;
		pointer_active = false;
		return wasActive;
	}

	public void CancelPointerAction () => pointer_active = false;

	public bool BeginStroke (IImageSurface? surface, PointD point, ColorBgra color, int size,
		double opacity, double hardness, bool layerLocked, Action beginHistory)
	{
		if (layerLocked || surface is null)
			return false;
		beginHistory ();
		return IsPencil
			? DrawPencilLine (surface, point, point, color, opacity)
			: DrawPoint (surface, point, color, size, opacity, hardness, IsEraser);
	}

	public bool ContinueStroke (IImageSurface? surface, PointD from, PointD to, ColorBgra color,
		int size, double opacity, double hardness, bool layerLocked)
	{
		if (layerLocked || surface is null)
			return false;
		return IsPencil
			? DrawPencilLine (surface, from, to, color, opacity)
			: DrawLine (surface, from, to, color, size, opacity, hardness, IsEraser);
	}
	public bool DrawPoint (IImageSurface? surface, PointD point, ColorBgra color, int size,
		double opacity, double hardness, bool erase)
	{
		if (surface is null)
			return false;
		int radius = Math.Max (size - 1, 0) / 2;
		Span<ColorBgra> pixels = surface.GetPixelData ();
		int centerX = (int) Math.Round (point.X), centerY = (int) Math.Round (point.Y);
		if (color.A > 0 && opacity < 1)
			color = ColorBgra.FromBgra (color.B, color.G, color.R, (byte) Math.Round (color.A * Math.Clamp (opacity, 0, 1)));
		for (int y = centerY - radius; y <= centerY + radius; y++)
			for (int x = centerX - radius; x <= centerX + radius; x++)
				if ((uint) x < surface.Width && (uint) y < surface.Height) {
					ColorBgra painted = erase ? ColorBgra.Zero : color;
					if (color.A > 0 && hardness < 1 && radius > 0) {
						double edge = Math.Max (Math.Abs (x - centerX), Math.Abs (y - centerY)) / (double) radius;
						double softness = BrushAlgorithms.HardnessCoverage (edge, hardness);
						painted = softness <= 0 ? ColorBgra.Zero : ColorBgra.FromBgra (color.B, color.G, color.R, (byte) Math.Round (color.A * softness));
					}
					int index = y * surface.Width + x;
					pixels[index] = erase ? painted : RasterAlgorithms.CompositeOver (pixels[index], painted);
				}
		return true;
	}

	public bool DrawLine (IImageSurface? surface, PointD from, PointD to, ColorBgra color,
		int size, double opacity, double hardness, bool erase)
	{
		if (surface is null)
			return false;
		if (from == to)
			return DrawPoint (surface, from, color, size, opacity, hardness, erase);
		foreach (PointD point in BrushAlgorithms.InterpolateLine (from, to))
			DrawPoint (surface, point, color, size, opacity, hardness, erase);
		return true;
	}

	public bool DrawPencilLine (IImageSurface? surface, PointD from, PointD to, ColorBgra color, double opacity)
	{
		if (surface is null)
			return false;
		if (color.A > 0 && opacity < 1)
			color = ColorBgra.FromBgra (color.B, color.G, color.R, (byte) Math.Round (color.A * Math.Clamp (opacity, 0, 1)));
		Span<ColorBgra> pixels = surface.GetPixelData ();
		if (from == to) {
			int x = (int) Math.Round (from.X), y = (int) Math.Round (from.Y);
			if ((uint) x < surface.Width && (uint) y < surface.Height) {
				int index = y * surface.Width + x;
				pixels[index] = RasterAlgorithms.CompositeOver (pixels[index], color);
			}
			return true;
		}
		foreach (PointD point in BrushAlgorithms.InterpolateLine (from, to)) {
			int x = (int) Math.Round (point.X);
			int y = (int) Math.Round (point.Y);
			if ((uint) x < surface.Width && (uint) y < surface.Height) {
				int index = y * surface.Width + x;
				pixels[index] = RasterAlgorithms.CompositeOver (pixels[index], color);
			}
		}
		return true;
	}

}
