using System;
using System.Collections.Generic;

namespace Pinta.Core;

/// <summary>Platform-neutral raster operations shared by GTK and Avalonia adapters.</summary>
public static class RasterAlgorithms
{
	public static ColorBgra ApplyOpacity (ColorBgra color, double opacity)
	{
		if (opacity >= 1) return color;
		if (opacity <= 0) return ColorBgra.FromBgra (color.B, color.G, color.R, 0);
		return ColorBgra.FromBgra (color.B, color.G, color.R, (byte) Math.Round (color.A * opacity));
	}

	public static ColorBgra CompositeOver (ColorBgra destination, ColorBgra source)
	{
		if (source.A == 255) return source;
		if (source.A == 0) return destination;
		int sourceAlpha = source.A;
		int destinationAlpha = destination.A;
		int outputAlpha = sourceAlpha + destinationAlpha * (255 - sourceAlpha) / 255;
		if (outputAlpha == 0) return ColorBgra.Zero;
		return ColorBgra.FromBgra (
			(byte) ((source.B * sourceAlpha + destination.B * destinationAlpha * (255 - sourceAlpha) / 255) / outputAlpha),
			(byte) ((source.G * sourceAlpha + destination.G * destinationAlpha * (255 - sourceAlpha) / 255) / outputAlpha),
			(byte) ((source.R * sourceAlpha + destination.R * destinationAlpha * (255 - sourceAlpha) / 255) / outputAlpha),
			(byte) outputAlpha);
	}

	public static void CompositeOver (Span<ColorBgra> destination, ReadOnlySpan<ColorBgra> source)
	{
		if (destination.Length != source.Length)
			throw new ArgumentException ("Source and destination lengths must match.");
		for (int i = 0; i < destination.Length; i++)
			destination[i] = CompositeOver (destination[i], source[i]);
	}

	public static bool TrySampleNearest (IImageSurface? surface, PointD point, out ColorBgra color)
	{
		color = default;
		if (surface is null)
			return false;
		int x = (int) Math.Round (point.X);
		int y = (int) Math.Round (point.Y);
		if ((uint) x >= (uint) surface.Width || (uint) y >= (uint) surface.Height)
			return false;
		color = surface.GetReadOnlyPixelData ()[y * surface.Width + x];
		return true;
	}

	public static bool FloodFill (IImageSurface? surface, PointD point, ColorBgra color, int tolerance)
	{
		if (surface is null)
			return false;
		int startX = (int) Math.Round (point.X);
		int startY = (int) Math.Round (point.Y);
		if ((uint) startX >= (uint) surface.Width || (uint) startY >= (uint) surface.Height)
			return false;

		Span<ColorBgra> pixels = surface.GetPixelData ();
		ColorBgra target = pixels[startY * surface.Width + startX];
		if (target == color)
			return false;

		int threshold = Math.Clamp (tolerance, 0, 255);
		var queue = new Queue<(int X, int Y)> (16);
		var visited = new bool[pixels.Length];
		queue.Enqueue ((startX, startY));
		while (queue.Count > 0) {
			(int x, int y) = queue.Dequeue ();
			if ((uint) x >= (uint) surface.Width || (uint) y >= (uint) surface.Height)
				continue;
			int index = y * surface.Width + x;
			if (visited[index])
				continue;
			visited[index] = true;
			ColorBgra actual = pixels[index];
			if (Math.Abs (actual.R - target.R) > threshold
				|| Math.Abs (actual.G - target.G) > threshold
				|| Math.Abs (actual.B - target.B) > threshold
				|| Math.Abs (actual.A - target.A) > threshold)
				continue;
			pixels[index] = color;
			queue.Enqueue ((x + 1, y));
			queue.Enqueue ((x - 1, y));
			queue.Enqueue ((x, y + 1));
			queue.Enqueue ((x, y - 1));
		}
		return true;
	}

	public static void ApplyLinearGradient (IImageSurface surface, PointD from, PointD to,
		ColorBgra primary, ColorBgra secondary, bool reverse)
	{
		ArgumentNullException.ThrowIfNull (surface);
		double dx = to.X - from.X, dy = to.Y - from.Y;
		double lengthSquared = Math.Max (dx * dx + dy * dy, 1);
		Span<ColorBgra> pixels = surface.GetPixelData ();
		for (int y = 0; y < surface.Height; y++)
			for (int x = 0; x < surface.Width; x++) {
				double t = Math.Clamp (((x - from.X) * dx + (y - from.Y) * dy) / lengthSquared, 0, 1);
				if (reverse) t = 1 - t;
				pixels[y * surface.Width + x] = ColorBgra.FromBgra (
					(byte) Math.Round (primary.B + (secondary.B - primary.B) * t),
					(byte) Math.Round (primary.G + (secondary.G - primary.G) * t),
					(byte) Math.Round (primary.R + (secondary.R - primary.R) * t),
					(byte) Math.Round (primary.A + (secondary.A - primary.A) * t));
			}
	}
}
