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
		host.MarkCanvasChanged ();
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
			host.MarkCanvasChanged ();
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
						double softness = Math.Clamp ((1 - edge) / Math.Max (1 - hardness, 0.001), 0, 1);
						painted = softness <= 0 ? ColorBgra.Zero : ColorBgra.FromBgra (color.B, color.G, color.R, (byte) Math.Round (color.A * softness));
					}
					int index = y * surface.Width + x;
					pixels[index] = erase ? painted : Blend (pixels[index], painted);
				}
		return true;
	}

	public bool DrawLine (IImageSurface? surface, PointD from, PointD to, ColorBgra color,
		int size, double opacity, double hardness, bool erase)
	{
		if (surface is null)
			return false;
		double distance = Math.Sqrt (Math.Pow (to.X - from.X, 2) + Math.Pow (to.Y - from.Y, 2));
		if (distance == 0)
			return DrawPoint (surface, from, color, size, opacity, hardness, erase);
		int steps = Math.Max (1, (int) Math.Ceiling (distance));
		for (int i = 0; i <= steps; i++) {
			double progress = i / (double) steps;
			DrawPoint (surface, new PointD (from.X + (to.X - from.X) * progress,
				from.Y + (to.Y - from.Y) * progress), color, size, opacity, hardness, erase);
		}
		return true;
	}

	public bool DrawPencilLine (IImageSurface? surface, PointD from, PointD to, ColorBgra color, double opacity)
	{
		if (surface is null)
			return false;
		if (color.A > 0 && opacity < 1)
			color = ColorBgra.FromBgra (color.B, color.G, color.R, (byte) Math.Round (color.A * Math.Clamp (opacity, 0, 1)));
		double distance = Math.Sqrt (Math.Pow (to.X - from.X, 2) + Math.Pow (to.Y - from.Y, 2));
		Span<ColorBgra> pixels = surface.GetPixelData ();
		if (distance == 0) {
			int x = (int) Math.Round (from.X), y = (int) Math.Round (from.Y);
			if ((uint) x < surface.Width && (uint) y < surface.Height) {
				int index = y * surface.Width + x;
				pixels[index] = Blend (pixels[index], color);
			}
			return true;
		}
		int steps = Math.Max (1, (int) Math.Ceiling (distance));
		for (int i = 0; i <= steps; i++) {
			double progress = i / (double) steps;
			int x = (int) Math.Round (from.X + (to.X - from.X) * progress);
			int y = (int) Math.Round (from.Y + (to.Y - from.Y) * progress);
			if ((uint) x < surface.Width && (uint) y < surface.Height) {
				int index = y * surface.Width + x;
				pixels[index] = Blend (pixels[index], color);
			}
		}
		return true;
	}

	private static ColorBgra Blend (ColorBgra destination, ColorBgra source)
	{
		if (source.A == 255) return source;
		if (source.A == 0) return destination;
		int sourceAlpha = source.A, destinationAlpha = destination.A;
		int outputAlpha = sourceAlpha + destinationAlpha * (255 - sourceAlpha) / 255;
		if (outputAlpha == 0) return ColorBgra.Zero;
		return ColorBgra.FromBgra (
			(byte) ((source.B * sourceAlpha + destination.B * destinationAlpha * (255 - sourceAlpha) / 255) / outputAlpha),
			(byte) ((source.G * sourceAlpha + destination.G * destinationAlpha * (255 - sourceAlpha) / 255) / outputAlpha),
			(byte) ((source.R * sourceAlpha + destination.R * destinationAlpha * (255 - sourceAlpha) / 255) / outputAlpha),
			(byte) outputAlpha);
	}
}
