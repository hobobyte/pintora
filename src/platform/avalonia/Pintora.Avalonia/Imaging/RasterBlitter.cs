using System;
using Avalonia;
using Pinta.Core;

namespace Pintora.Avalonia.Imaging;

public static class RasterBlitter
{
	public static void CopyScaled (IImageSurface source, IImageSurface destination, Rect bounds, double opacity = 1, bool preserveAspectRatio = true, ColorBgra? tint = null, double rotation = 0)
	{
		if (source.Width <= 0 || source.Height <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
			return;
		double scaleX = bounds.Width / source.Width;
		double scaleY = bounds.Height / source.Height;
		double scale = preserveAspectRatio ? Math.Min (scaleX, scaleY) : 1;
		double width = preserveAspectRatio ? source.Width * scale : bounds.Width;
		double height = preserveAspectRatio ? source.Height * scale : bounds.Height;
		double left = bounds.X + (bounds.Width - width) / 2;
		double top = bounds.Y + (bounds.Height - height) / 2;
		int firstX = Math.Max (0, (int) Math.Floor (left));
		int firstY = Math.Max (0, (int) Math.Floor (top));
		int lastX = Math.Min (destination.Width, (int) Math.Ceiling (left + width));
		int lastY = Math.Min (destination.Height, (int) Math.Ceiling (top + height));
		if (firstX >= lastX || firstY >= lastY)
			return;

		opacity = Math.Clamp (opacity, 0, 1);
		double actualScaleX = width / source.Width;
		double actualScaleY = height / source.Height;
		ReadOnlySpan<ColorBgra> sourcePixels = source.GetReadOnlyPixelData ();
		Span<ColorBgra> destinationPixels = destination.GetPixelData ();
		for (int y = firstY; y < lastY; y++) {
			for (int x = firstX; x < lastX; x++) {
				double localPositionX = (x + 0.5 - (left + width / 2)) / actualScaleX;
				double localPositionY = (y + 0.5 - (top + height / 2)) / actualScaleY;
				double cosine = Math.Cos (-rotation), sine = Math.Sin (-rotation);
				double rotatedX = localPositionX * cosine - localPositionY * sine + source.Width / 2.0;
				double rotatedY = localPositionX * sine + localPositionY * cosine + source.Height / 2.0;
				if (rotatedX < 0 || rotatedY < 0 || rotatedX >= source.Width || rotatedY >= source.Height)
					continue;
				int sourceX = Math.Clamp ((int) rotatedX, 0, source.Width - 1);
				int sourceSampleY = Math.Clamp ((int) rotatedY, 0, source.Height - 1);
				ColorBgra color = sourcePixels[sourceSampleY * source.Width + sourceX];
				if (tint is ColorBgra tintColor)
					color = ColorBgra.FromBgra ((byte) (color.B * tintColor.B / 255), (byte) (color.G * tintColor.G / 255), (byte) (color.R * tintColor.R / 255), (byte) (color.A * tintColor.A / 255));
				if (opacity < 1)
					color = ColorBgra.FromBgra (color.B, color.G, color.R, (byte) Math.Round (color.A * opacity));
				destinationPixels[y * destination.Width + x] = Blend (destinationPixels[y * destination.Width + x], color);
			}
		}
	}

	public static void CopyRegion (IImageSurface source, RectangleI area, IImageSurface destination)
	{
		int left = Math.Max (0, area.X);
		int top = Math.Max (0, area.Y);
		int right = Math.Min (source.Width, area.X + area.Width);
		int bottom = Math.Min (source.Height, area.Y + area.Height);
		if (right <= left || bottom <= top)
			return;
		int copyWidth = Math.Min (right - left, destination.Width);
		int copyHeight = Math.Min (bottom - top, destination.Height);
		ReadOnlySpan<ColorBgra> sourcePixels = source.GetReadOnlyPixelData ();
		Span<ColorBgra> destinationPixels = destination.GetPixelData ();
		for (int y = 0; y < copyHeight; y++)
			sourcePixels.Slice ((top + y) * source.Width + left, copyWidth)
				.CopyTo (destinationPixels.Slice (y * destination.Width, copyWidth));
	}

	public static void CopyCentered (IImageSurface source, IImageSurface destination)
	{
		int offsetX = (destination.Width - source.Width) / 2;
		int offsetY = (destination.Height - source.Height) / 2;
		int sourceLeft = Math.Max (0, -offsetX);
		int sourceTop = Math.Max (0, -offsetY);
		int destinationLeft = Math.Max (0, offsetX);
		int destinationTop = Math.Max (0, offsetY);
		int copyWidth = Math.Min (source.Width - sourceLeft, destination.Width - destinationLeft);
		int copyHeight = Math.Min (source.Height - sourceTop, destination.Height - destinationTop);
		if (copyWidth <= 0 || copyHeight <= 0)
			return;

		ReadOnlySpan<ColorBgra> sourcePixels = source.GetReadOnlyPixelData ();
		Span<ColorBgra> destinationPixels = destination.GetPixelData ();
		for (int y = 0; y < copyHeight; y++)
			sourcePixels.Slice ((sourceTop + y) * source.Width + sourceLeft, copyWidth)
				.CopyTo (destinationPixels.Slice ((destinationTop + y) * destination.Width + destinationLeft, copyWidth));
	}

	private static ColorBgra Blend (ColorBgra destination, ColorBgra source)
	{
		int alpha = source.A + destination.A * (255 - source.A) / 255;
		if (alpha == 0)
			return ColorBgra.Zero;
		return ColorBgra.FromBgra (
			(byte) ((source.B * source.A + destination.B * destination.A * (255 - source.A) / 255) / alpha),
			(byte) ((source.G * source.A + destination.G * destination.A * (255 - source.A) / 255) / alpha),
			(byte) ((source.R * source.A + destination.R * destination.A * (255 - source.A) / 255) / alpha),
			(byte) alpha);
	}
}
