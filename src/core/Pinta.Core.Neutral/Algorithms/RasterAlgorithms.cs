using System;
using System.Collections.Generic;

namespace Pinta.Core;

/// <summary>Platform-neutral raster operations shared by GTK and Avalonia adapters.</summary>
public static class RasterAlgorithms
{
	public static byte[,] CreateRadialFalloffLut (int resolution)
	{
		if (resolution <= 0) throw new ArgumentOutOfRangeException (nameof (resolution));
		var result = new byte[resolution + 1, resolution + 1];
		for (int y = 0; y <= resolution; y++)
			for (int x = 0; x <= resolution; x++) {
				double distance = Math.Sqrt (x * x + y * y) / resolution;
				result[y, x] = distance > 1
					? (byte) 255
					: (byte) (255 - Math.Cos (Math.Sqrt (distance) * Math.PI / 2) * 255);
			}
		return result;
	}

	public static bool TryRecolorPixel (ColorBgra basis, ColorBgra search, ColorBgra oldColor,
		ColorBgra newColor, int tolerance, out ColorBgra result)
	{
		if (!ColorBgra.ColorsWithinTolerance (search, basis, Math.Clamp (tolerance, 0, 256))) {
			result = basis;
			return false;
		}
		result = AdjustColorDifference (search, oldColor, basis);
		return true;
	}

	public static ColorBgra AdjustColorDifference (ColorBgra oldColor, ColorBgra newColor, ColorBgra basisColor)
		=> ColorBgra.FromBgra (
			AdjustColorByte (oldColor.B, newColor.B, basisColor.B),
			AdjustColorByte (oldColor.G, newColor.G, basisColor.G),
			AdjustColorByte (oldColor.R, newColor.R, basisColor.R),
			basisColor.A);

	private static byte AdjustColorByte (byte oldByte, byte newByte, byte basisByte)
	{
		int delta = newByte - oldByte;
		return (byte) Math.Clamp (basisByte + delta, 0, 255);
	}

	public static ColorBgra ApplyOpacity (ColorBgra color, double opacity)
	{
		if (opacity >= 1) return color;
		if (opacity <= 0) return ColorBgra.FromBgra (color.B, color.G, color.R, 0);
		return ColorBgra.FromBgra (color.B, color.G, color.R, (byte) Math.Round (color.A * opacity));
	}

	public static void ApplyOpacity (Span<ColorBgra> pixels, double opacity)
	{
		for (int i = 0; i < pixels.Length; i++)
			pixels[i] = ApplyOpacity (pixels[i], opacity);
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

	/// <summary>Samples a center-cropped raster into a fixed thumbnail size.</summary>
	public static (int Width, int Height, ColorBgra[] Pixels) SampleThumbnail (IImageSurface surface, int width, int height)
	{
		ArgumentNullException.ThrowIfNull (surface);
		if (width <= 0 || height <= 0)
			throw new ArgumentOutOfRangeException (nameof (width));
		if (surface.Width <= 0 || surface.Height <= 0)
			return (width, height, new ColorBgra[width * height]);

		double targetAspect = width / (double) height;
		double sourceAspect = surface.Width / (double) surface.Height;
		int cropWidth = surface.Width;
		int cropHeight = surface.Height;
		if (sourceAspect > targetAspect)
			cropWidth = Math.Max (1, (int) Math.Round (surface.Height * targetAspect));
		else
			cropHeight = Math.Max (1, (int) Math.Round (surface.Width / targetAspect));
		int cropLeft = (surface.Width - cropWidth) / 2;
		int cropTop = (surface.Height - cropHeight) / 2;
		ReadOnlySpan<ColorBgra> source = surface.GetReadOnlyPixelData ();
		var pixels = new ColorBgra[width * height];
		for (int y = 0; y < height; y++) {
			int sourceY = cropTop + Math.Min (cropHeight - 1, y * cropHeight / height);
			for (int x = 0; x < width; x++) {
				int sourceX = cropLeft + Math.Min (cropWidth - 1, x * cropWidth / width);
				pixels[y * width + x] = source[sourceY * surface.Width + sourceX];
			}
		}
		return (width, height, pixels);
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
