using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Pinta.Core;
using Pintora.Avalonia.Canvas;
using Pintora.Avalonia.Imaging;
using SkiaSharp;

namespace Pintora.Avalonia.Editor.Annotations;

/// <summary>Rasterizes persistent annotations without making the editor depend on their storage.</summary>
public static class AnnotationRenderer
{
	public static global::Avalonia.Size MeasureText (string text, double fontSize, string fontFamily, FontWeight weight)
	{
		using var font = CreateFont (fontSize, fontFamily, weight);
		string[] lines = text.Replace ("\r\n", "\n", StringComparison.Ordinal).Split ('\n');
		double width = 1;
		foreach (string line in lines)
			width = Math.Max (width, font.MeasureText (line));
		return new global::Avalonia.Size (width, Math.Max (1, lines.Length) * font.Size * 1.25);
	}

	public static bool TryRender (MemoryImageSurface surface, Annotation annotation)
	{
		switch (annotation) {
			case ShapeAnnotation shape:
				Render (surface, shape);
				return true;
			case TextAnnotation text:
				Render (surface, text);
				return true;
			case RegionEffectAnnotation effect:
				Render (surface, effect);
				return true;
			default:
				return false;
		}
	}

	public static bool TryRender (MemoryImageSurface surface, Annotation annotation, IImageSurface? source)
	{
		if (source is null)
			return TryRender (surface, annotation);
		switch (annotation) {
			case ImageAnnotation image:
				Render (surface, image, source);
				return true;
			case CursorAnnotation cursor:
				Render (surface, cursor, source);
				return true;
			default:
				return TryRender (surface, annotation);
		}
	}

	public static void Render (MemoryImageSurface surface, ImageAnnotation annotation, IImageSurface source)
	{
		RasterBlitter.CopyScaled (source, surface, annotation.Bounds, annotation.Opacity, annotation.PreserveAspectRatio, rotation: annotation.Rotation);
	}

	public static void Render (MemoryImageSurface surface, CursorAnnotation annotation, IImageSurface source)
	{
		double scale = Math.Max (0.01, annotation.Scale);
		Rect bounds = annotation.Bounds;
		if (Math.Abs (scale - 1) > double.Epsilon) {
			double width = bounds.Width * scale;
			double height = bounds.Height * scale;
			bounds = new Rect (bounds.Center.X - width / 2, bounds.Center.Y - height / 2, width, height);
		}
		bounds = new Rect (bounds.X - annotation.HotspotX * scale, bounds.Y - annotation.HotspotY * scale, bounds.Width, bounds.Height);
		RasterBlitter.CopyScaled (source, surface, bounds, 1, true, annotation.Tint, annotation.Rotation);
	}

	public static void Render (MemoryImageSurface surface, ShapeAnnotation annotation)
	{
		ShapeRasterizer.DrawRotated (surface, annotation.Kind, annotation.From, annotation.To,
			annotation.Outline, annotation.Fill, annotation.FillStyle, annotation.OutlineWidth,
			annotation.Dash, annotation.CornerRadius, annotation.Antialias, annotation.Rotation);
	}

	public static void Render (MemoryImageSurface surface, TextAnnotation annotation)
	{
		using var overlay = new SKBitmap (surface.Width, surface.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
		using var canvas = new SKCanvas (overlay);
		using var paint = new SKPaint {
			Color = new SKColor (annotation.Color.R, annotation.Color.G, annotation.Color.B, annotation.Color.A),
				IsAntialias = true
		};
		using var font = CreateFont (annotation.FontSize, annotation.FontFamily, annotation.FontWeight);
		float x = (float) annotation.Bounds.X;
		SKPoint center = new ((float) annotation.Bounds.Center.X, (float) annotation.Bounds.Center.Y);
		canvas.Save ();
		canvas.RotateDegrees ((float) (annotation.Rotation * 180 / Math.PI), center.X, center.Y);
		string[] lines = annotation.Text.Replace ("\r\n", "\n", StringComparison.Ordinal).Split ('\n');
		for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++) {
			float y = (float) annotation.Bounds.Y + font.Size + lineIndex * font.Size * 1.25f;
			canvas.DrawText (lines[lineIndex], x, y, SKTextAlign.Left, font, paint);
			if (annotation.Underline)
				canvas.DrawLine (x, y + 2, x + font.MeasureText (lines[lineIndex]), y + 2, paint);
		}
		canvas.Restore ();
		canvas.Flush ();
		CopyOverlay (surface, overlay);
	}

	private static SKFont CreateFont (double size, string family, FontWeight weight)
	{
		SKTypeface? typeface = SKTypeface.FromFamilyName (family,
			weight == FontWeight.Bold || weight == FontWeight.SemiBold ? SKFontStyle.Bold : SKFontStyle.Normal)
			?? SKTypeface.Default;
		return new SKFont (typeface, (float) size);
	}

	public static void Render (MemoryImageSurface surface, RegionEffectAnnotation annotation)
	{
		int left = Math.Max (0, (int) Math.Floor (annotation.Bounds.X));
		int top = Math.Max (0, (int) Math.Floor (annotation.Bounds.Y));
		int right = Math.Min (surface.Width, (int) Math.Ceiling (annotation.Bounds.Right));
		int bottom = Math.Min (surface.Height, (int) Math.Ceiling (annotation.Bounds.Bottom));
		if (left >= right || top >= bottom) return;
		switch (annotation.Effect) {
			case RegionEffectKind.Highlight:
				Highlight (surface, left, top, right, bottom, annotation.HighlightColor, annotation.Strength);
				break;
			case RegionEffectKind.Pixelate:
				Pixelate (surface, left, top, right, bottom, Math.Max (2, annotation.PixelSize));
				break;
			case RegionEffectKind.Blur:
				Blur (surface, left, top, right, bottom, Math.Clamp (annotation.Strength, 0, 1));
				break;
			case RegionEffectKind.Magnify:
				Magnify (surface, left, top, right, bottom, annotation.Strength);
				break;
			case RegionEffectKind.Spotlight:
				Spotlight (surface, left, top, right, bottom, annotation.Strength);
				break;
		}
	}

	private static void Magnify (MemoryImageSurface surface, int left, int top, int right, int bottom, double strength)
	{
		double factor = 1 + Math.Clamp (strength, 0, 4);
		ColorBgra[] source = surface.GetReadOnlyPixelData ().ToArray ();
		double centerX = (left + right - 1) / 2.0;
		double centerY = (top + bottom - 1) / 2.0;
		Span<ColorBgra> pixels = surface.GetPixelData ();
		for (int y = top; y < bottom; y++) for (int x = left; x < right; x++) {
			int sampleX = Math.Clamp ((int) Math.Round (centerX + (x - centerX) / factor), left, right - 1);
			int sampleY = Math.Clamp ((int) Math.Round (centerY + (y - centerY) / factor), top, bottom - 1);
			pixels[y * surface.Width + x] = source[sampleY * surface.Width + sampleX];
		}
	}

	private static void Spotlight (MemoryImageSurface surface, int left, int top, int right, int bottom, double strength)
	{
		byte alpha = (byte) Math.Round (Math.Clamp (strength, 0, 1) * 180);
		if (alpha == 0) return;
		Span<ColorBgra> pixels = surface.GetPixelData ();
		for (int y = 0; y < surface.Height; y++) for (int x = 0; x < surface.Width; x++)
			if (x < left || x >= right || y < top || y >= bottom)
				pixels[y * surface.Width + x] = Blend (pixels[y * surface.Width + x], ColorBgra.FromBgra (0, 0, 0, alpha));
	}

	private static void Highlight (MemoryImageSurface surface, int left, int top, int right, int bottom, ColorBgra color, double strength)
	{
		byte alpha = (byte) Math.Round (color.A * Math.Clamp (strength, 0, 1));
		Span<ColorBgra> pixels = surface.GetPixelData ();
		for (int y = top; y < bottom; y++) for (int x = left; x < right; x++) {
			int i = y * surface.Width + x;
			pixels[i] = Blend (pixels[i], ColorBgra.FromBgra (color.B, color.G, color.R, alpha));
		}
	}

	private static void Pixelate (MemoryImageSurface surface, int left, int top, int right, int bottom, int block)
	{
		Span<ColorBgra> pixels = surface.GetPixelData ();
		for (int by = top; by < bottom; by += block) for (int bx = left; bx < right; bx += block) {
			int endX = Math.Min (right, bx + block), endY = Math.Min (bottom, by + block);
			long b = 0, g = 0, r = 0, a = 0, count = 0;
			for (int y = by; y < endY; y++) for (int x = bx; x < endX; x++) { ColorBgra c = pixels[y * surface.Width + x]; b += c.B; g += c.G; r += c.R; a += c.A; count++; }
			ColorBgra average = ColorBgra.FromBgra ((byte) (b / count), (byte) (g / count), (byte) (r / count), (byte) (a / count));
			for (int y = by; y < endY; y++) for (int x = bx; x < endX; x++) pixels[y * surface.Width + x] = average;
		}
	}

	private static void Blur (MemoryImageSurface surface, int left, int top, int right, int bottom, double strength)
	{
		int radius = Math.Max (1, (int) Math.Round (1 + strength * 5));
		ColorBgra[] source = surface.GetReadOnlyPixelData ().ToArray ();
		ColorBgra[] horizontal = new ColorBgra[source.Length];
		Span<ColorBgra> pixels = surface.GetPixelData ();
		// A box blur is separable. Two one-dimensional passes produce the same
		// rectangular kernel as the old nested neighbourhood loop, but reduce
		// work from O(area * radius²) to O(area * radius).
		for (int y = top; y < bottom; y++) for (int x = left; x < right; x++) {
			long b = 0, g = 0, r = 0, a = 0, count = 0;
			for (int sx = Math.Max (left, x - radius); sx <= Math.Min (right - 1, x + radius); sx++) {
				ColorBgra c = source[y * surface.Width + sx];
				b += c.B; g += c.G; r += c.R; a += c.A; count++;
			}
			horizontal[y * surface.Width + x] = ColorBgra.FromBgra ((byte) (b / count), (byte) (g / count), (byte) (r / count), (byte) (a / count));
		}
		for (int y = top; y < bottom; y++) for (int x = left; x < right; x++) {
			long b = 0, g = 0, r = 0, a = 0, count = 0;
			for (int sy = Math.Max (top, y - radius); sy <= Math.Min (bottom - 1, y + radius); sy++) {
				ColorBgra c = horizontal[sy * surface.Width + x];
				b += c.B; g += c.G; r += c.R; a += c.A; count++;
			}
			pixels[y * surface.Width + x] = ColorBgra.FromBgra ((byte) (b / count), (byte) (g / count), (byte) (r / count), (byte) (a / count));
		}
	}

	private static ColorBgra Blend (ColorBgra destination, ColorBgra source)
	{
		int alpha = source.A + destination.A * (255 - source.A) / 255;
		if (alpha == 0) return ColorBgra.Zero;
		return ColorBgra.FromBgra ((byte) ((source.B * source.A + destination.B * destination.A * (255 - source.A) / 255) / alpha), (byte) ((source.G * source.A + destination.G * destination.A * (255 - source.A) / 255) / alpha), (byte) ((source.R * source.A + destination.R * destination.A * (255 - source.A) / 255) / alpha), (byte) alpha);
	}

	private static void CopyOverlay (MemoryImageSurface surface, SKBitmap overlay)
	{
		Span<ColorBgra> pixels = surface.GetPixelData ();
		ReadOnlySpan<SKColor> overlayPixels = MemoryMarshal.Cast<byte, SKColor> (overlay.GetPixelSpan ());
		for (int y = 0; y < surface.Height; y++)
			for (int x = 0; x < surface.Width; x++) {
				SKColor color = overlayPixels[y * surface.Width + x];
				if (color.Alpha == 0) continue;
				int index = y * surface.Width + x;
				pixels[index] = Blend (pixels[index], ColorBgra.FromBgra (color.Blue, color.Green, color.Red, color.Alpha));
			}
	}
}
