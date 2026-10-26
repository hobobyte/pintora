using System;
using System.Collections.Generic;
using Pinta.Core;

namespace Pintora.Avalonia.Canvas;

internal sealed class FillTool : ICanvasTool
{
	public CanvasHost.CanvasTool Kind => CanvasHost.CanvasTool.Fill;
	public bool PointerPressed (CanvasToolPointerContext context)
		=> context.Host.FillAt (context.Point, context.Color);

	public bool Fill (IImageSurface? surface, PointD point, ColorBgra color, int tolerance,
		Action beginStroke, Action commitStroke)
	{
		if (surface is null)
			return false;
		int startX = (int) Math.Round (point.X), startY = (int) Math.Round (point.Y);
		if ((uint) startX >= surface.Width || (uint) startY >= surface.Height)
			return false;
		Span<ColorBgra> pixels = surface.GetPixelData ();
		ColorBgra target = pixels[startY * surface.Width + startX];
		if (target == color)
			return false;
		beginStroke ();
		var queue = new Queue<(int X, int Y)> ();
		var visited = new bool[pixels.Length];
		queue.Enqueue ((startX, startY));
		while (queue.Count > 0) {
			(int x, int y) = queue.Dequeue ();
			if ((uint) x >= surface.Width || (uint) y >= surface.Height)
				continue;
			int index = y * surface.Width + x;
			if (visited[index])
				continue;
			visited[index] = true;
			if (!Matches (pixels[index], target, tolerance))
				continue;
			pixels[index] = color;
			queue.Enqueue ((x + 1, y));
			queue.Enqueue ((x - 1, y));
			queue.Enqueue ((x, y + 1));
			queue.Enqueue ((x, y - 1));
		}
		commitStroke ();
		return true;
	}

	private static bool Matches (ColorBgra actual, ColorBgra target, int tolerance)
	{
		int threshold = Math.Clamp (tolerance, 0, 255);
		return Math.Abs (actual.R - target.R) <= threshold
			&& Math.Abs (actual.G - target.G) <= threshold
			&& Math.Abs (actual.B - target.B) <= threshold
			&& Math.Abs (actual.A - target.A) <= threshold;
	}
}
