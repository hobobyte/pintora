using System;
using System.Collections.Generic;
using Pinta.Core;
using Pintora.Avalonia.Imaging;
using SkiaSharp;

namespace Pintora.Avalonia.Canvas;

internal static class ShapeRasterizer
{
	public static void Draw (
		MemoryImageSurface surface, CanvasHost.ShapeKind kind, PointD from, PointD to,
		ColorBgra outline, ColorBgra fill, CanvasHost.ShapeFillStyle style,
		int width, CanvasHost.ShapeDashStyle dash, int cornerRadius, bool antialias)
	{
		if (antialias) {
			DrawSmooth (surface, kind, from, to, outline, fill, style, width, dash, cornerRadius, 0);
			return;
		}
		double left = Math.Min (from.X, to.X);
		double top = Math.Min (from.Y, to.Y);
		double right = Math.Max (from.X, to.X);
		double bottom = Math.Max (from.Y, to.Y);
		if (kind != CanvasHost.ShapeKind.Line && style != CanvasHost.ShapeFillStyle.Outline)
			Fill (surface, kind, left, top, right, bottom,
				style == CanvasHost.ShapeFillStyle.Fill ? outline : fill, cornerRadius);
		if (kind == CanvasHost.ShapeKind.Line || style != CanvasHost.ShapeFillStyle.Fill)
			Outline (surface, BuildPath (kind, from, to, cornerRadius), outline, width, dash);
	}

	public static void DrawRotated (MemoryImageSurface surface, CanvasHost.ShapeKind kind, PointD from, PointD to,
		ColorBgra outline, ColorBgra fill, CanvasHost.ShapeFillStyle style, int width, CanvasHost.ShapeDashStyle dash,
		int cornerRadius, bool antialias, double angle)
	{
		if (!antialias) { Draw (surface, kind, from, to, outline, fill, style, width, dash, cornerRadius, false); return; }
		DrawSmooth (surface, kind, from, to, outline, fill, style, width, dash, cornerRadius, angle);
	}

	private static void DrawSmooth (MemoryImageSurface surface, CanvasHost.ShapeKind kind, PointD from, PointD to,
		ColorBgra outline, ColorBgra fill, CanvasHost.ShapeFillStyle style, int width, CanvasHost.ShapeDashStyle dash, int cornerRadius, double angle)
	{
		using var overlay = new SKBitmap (surface.Width, surface.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
		using var canvas = new SKCanvas (overlay);
		double centerX = (from.X + to.X) / 2, centerY = (from.Y + to.Y) / 2;
		if (Math.Abs (angle) > 0.001) canvas.RotateDegrees ((float) angle, (float) centerX, (float) centerY);
		using var path = new SKPath ();
		float left = (float) Math.Min (from.X, to.X);
		float top = (float) Math.Min (from.Y, to.Y);
		float right = (float) Math.Max (from.X, to.X);
		float bottom = (float) Math.Max (from.Y, to.Y);
		var rect = new SKRect (left, top, right, bottom);
		switch (kind) {
			case CanvasHost.ShapeKind.Line:
				path.MoveTo ((float) from.X, (float) from.Y);
				path.LineTo ((float) to.X, (float) to.Y);
				break;
			case CanvasHost.ShapeKind.Rectangle:
				path.AddRect (rect);
				break;
			case CanvasHost.ShapeKind.Ellipse:
				path.AddOval (rect);
				break;
			case CanvasHost.ShapeKind.RoundedRectangle:
				float radius = Math.Min (cornerRadius, Math.Min ((right - left) / 2, (bottom - top) / 2));
				path.AddRoundRect (rect, radius, radius);
				break;
			default:
				throw new ArgumentOutOfRangeException (nameof (kind));
		}
		if (kind != CanvasHost.ShapeKind.Line && style != CanvasHost.ShapeFillStyle.Outline) {
			ColorBgra fillColor = style == CanvasHost.ShapeFillStyle.Fill ? outline : fill;
			using var paint = new SKPaint {
				IsAntialias = true,
				Style = SKPaintStyle.Fill,
				Color = new SKColor (fillColor.R, fillColor.G, fillColor.B, fillColor.A)
			};
			canvas.DrawPath (path, paint);
		}
		if (kind == CanvasHost.ShapeKind.Line || style != CanvasHost.ShapeFillStyle.Fill) {
			using var paint = new SKPaint {
				IsAntialias = true,
				Style = SKPaintStyle.Stroke,
				StrokeWidth = width,
				StrokeCap = SKStrokeCap.Square,
				StrokeJoin = SKStrokeJoin.Round,
				Color = new SKColor (outline.R, outline.G, outline.B, outline.A)
			};
			float unit = Math.Max (1, width);
			float[]? pattern = dash switch {
				CanvasHost.ShapeDashStyle.Solid => null,
				CanvasHost.ShapeDashStyle.Dash => [6 * unit, 4 * unit],
				CanvasHost.ShapeDashStyle.Dot => [unit, 3 * unit],
				CanvasHost.ShapeDashStyle.DashDot => [6 * unit, 3 * unit, unit, 3 * unit],
				_ => throw new ArgumentOutOfRangeException (nameof (dash))
			};
			if (pattern is not null)
				paint.PathEffect = SKPathEffect.CreateDash (pattern, 0);
			canvas.DrawPath (path, paint);
		}
		canvas.Flush ();
		Span<ColorBgra> pixels = surface.GetPixelData ();
		int margin = width + 2;
		double radians = angle * Math.PI / 180;
		double cos = Math.Abs (Math.Cos (radians));
		double sin = Math.Abs (Math.Sin (radians));
		double rotatedWidth = (right - left) * cos + (bottom - top) * sin;
		double rotatedHeight = (right - left) * sin + (bottom - top) * cos;
		double rotatedLeft = centerX - rotatedWidth / 2;
		double rotatedTop = centerY - rotatedHeight / 2;
		double rotatedRight = centerX + rotatedWidth / 2;
		double rotatedBottom = centerY + rotatedHeight / 2;
		int minX = Math.Max (0, (int) Math.Floor (rotatedLeft) - margin);
		int minY = Math.Max (0, (int) Math.Floor (rotatedTop) - margin);
		int maxX = Math.Min (surface.Width - 1, (int) Math.Ceiling (rotatedRight) + margin);
		int maxY = Math.Min (surface.Height - 1, (int) Math.Ceiling (rotatedBottom) + margin);
		for (int y = minY; y <= maxY; y++)
			for (int x = minX; x <= maxX; x++) {
				SKColor color = overlay.GetPixel (x, y);
				if (color.Alpha == 0) continue;
				int index = y * surface.Width + x;
				pixels[index] = Blend (pixels[index], ColorBgra.FromBgra (color.Blue, color.Green, color.Red, color.Alpha));
			}
	}

	private static void Fill (MemoryImageSurface surface, CanvasHost.ShapeKind kind,
		double left, double top, double right, double bottom, ColorBgra color, int cornerRadius)
	{
		Span<ColorBgra> pixels = surface.GetPixelData ();
		double cx = (left + right) / 2;
		double cy = (top + bottom) / 2;
		double rx = (right - left) / 2;
		double ry = (bottom - top) / 2;
		double radius = Math.Min (cornerRadius, Math.Min (rx, ry));
		for (int y = Math.Max (0, (int) Math.Ceiling (top)); y <= Math.Min (surface.Height - 1, (int) Math.Floor (bottom)); y++)
			for (int x = Math.Max (0, (int) Math.Ceiling (left)); x <= Math.Min (surface.Width - 1, (int) Math.Floor (right)); x++) {
				bool inside = kind switch {
					CanvasHost.ShapeKind.Ellipse => rx == 0 || ry == 0 ||
						Math.Pow ((x - cx) / rx, 2) + Math.Pow ((y - cy) / ry, 2) <= 1,
					CanvasHost.ShapeKind.RoundedRectangle when radius > 0 =>
						Math.Pow (Math.Max (Math.Abs (x - cx) - (rx - radius), 0), 2)
						+ Math.Pow (Math.Max (Math.Abs (y - cy) - (ry - radius), 0), 2) <= radius * radius,
					_ => true
				};
				if (inside) {
					int index = y * surface.Width + x;
					pixels[index] = Blend (pixels[index], color);
				}
			}
	}

	private static List<PointD> BuildPath (CanvasHost.ShapeKind kind, PointD from, PointD to, int cornerRadius)
	{
		if (kind == CanvasHost.ShapeKind.Line)
			return [from, to];
		double left = Math.Min (from.X, to.X);
		double top = Math.Min (from.Y, to.Y);
		double right = Math.Max (from.X, to.X);
		double bottom = Math.Max (from.Y, to.Y);
		if (kind == CanvasHost.ShapeKind.Rectangle)
			return [new (left, top), new (right, top), new (right, bottom), new (left, bottom), new (left, top)];
		var points = new List<PointD> ();
		if (kind == CanvasHost.ShapeKind.Ellipse) {
			double rx = (right - left) / 2;
			double ry = (bottom - top) / 2;
			int steps = Math.Max (64, (int) Math.Ceiling (2 * Math.PI * Math.Max (rx, ry)));
			for (int i = 0; i <= steps; i++) {
				double angle = 2 * Math.PI * i / steps;
				points.Add (new PointD ((left + right) / 2 + rx * Math.Cos (angle), (top + bottom) / 2 + ry * Math.Sin (angle)));
			}
			return points;
		}
		if (kind != CanvasHost.ShapeKind.RoundedRectangle)
			throw new ArgumentOutOfRangeException (nameof (kind));
		double radius = Math.Min (cornerRadius, Math.Min ((right - left) / 2, (bottom - top) / 2));
		if (radius <= 0)
			return [new (left, top), new (right, top), new (right, bottom), new (left, bottom), new (left, top)];
		AddArc (points, right - radius, top + radius, radius, -Math.PI / 2, 0);
		AddArc (points, right - radius, bottom - radius, radius, 0, Math.PI / 2);
		AddArc (points, left + radius, bottom - radius, radius, Math.PI / 2, Math.PI);
		AddArc (points, left + radius, top + radius, radius, Math.PI, 3 * Math.PI / 2);
		points.Add (points[0]);
		return points;
	}

	private static void AddArc (List<PointD> points, double cx, double cy, double radius, double start, double end)
	{
		int steps = Math.Max (8, (int) Math.Ceiling (radius * Math.PI / 2));
		for (int i = 0; i <= steps; i++) {
			double angle = start + (end - start) * i / steps;
			points.Add (new PointD (cx + radius * Math.Cos (angle), cy + radius * Math.Sin (angle)));
		}
	}

	private static void Outline (MemoryImageSurface surface, List<PointD> path, ColorBgra color, int width, CanvasHost.ShapeDashStyle dash)
	{
		var mask = new bool[surface.Width * surface.Height];
		double distance = 0;
		for (int i = 1; i < path.Count; i++) {
			PointD a = path[i - 1];
			PointD b = path[i];
			double length = Math.Sqrt (Math.Pow (b.X - a.X, 2) + Math.Pow (b.Y - a.Y, 2));
			int steps = Math.Max (1, (int) Math.Ceiling (length * 2));
			for (int step = 0; step <= steps; step++) {
				double t = step / (double) steps;
				if (IsOn (distance + length * t, dash, width))
					Stamp (mask, surface.Width, surface.Height,
						(int) Math.Round (a.X + (b.X - a.X) * t),
						(int) Math.Round (a.Y + (b.Y - a.Y) * t), width);
			}
			distance += length;
		}
		Span<ColorBgra> pixels = surface.GetPixelData ();
		for (int i = 0; i < mask.Length; i++)
			if (mask[i]) pixels[i] = Blend (pixels[i], color);
	}

	private static bool IsOn (double distance, CanvasHost.ShapeDashStyle dash, int width)
	{
		int unit = Math.Max (1, width);
		int[] pattern = dash switch {
			CanvasHost.ShapeDashStyle.Solid => [1],
			CanvasHost.ShapeDashStyle.Dash => [6, 4],
			CanvasHost.ShapeDashStyle.Dot => [1, 3],
			CanvasHost.ShapeDashStyle.DashDot => [6, 3, 1, 3],
			_ => throw new ArgumentOutOfRangeException (nameof (dash))
		};
		if (pattern.Length == 1) return true;
		int cycle = 0;
		foreach (int part in pattern) cycle += part * unit;
		double position = distance % cycle;
		for (int i = 0; i < pattern.Length; i++) {
			position -= pattern[i] * unit;
			if (position < 0) return i % 2 == 0;
		}
		return true;
	}

	private static void Stamp (bool[] mask, int width, int height, int x, int y, int size)
	{
		int firstX = x - (size - 1) / 2;
		int firstY = y - (size - 1) / 2;
		for (int py = Math.Max (0, firstY); py <= Math.Min (height - 1, firstY + size - 1); py++)
			for (int px = Math.Max (0, firstX); px <= Math.Min (width - 1, firstX + size - 1); px++)
				mask[py * width + px] = true;
	}

	private static ColorBgra Blend (ColorBgra destination, ColorBgra source)
	{
		if (source.A == 255) return source;
		if (source.A == 0) return destination;
		int alpha = source.A + destination.A * (255 - source.A) / 255;
		return ColorBgra.FromBgra (
			(byte) ((source.B * source.A + destination.B * destination.A * (255 - source.A) / 255) / alpha),
			(byte) ((source.G * source.A + destination.G * destination.A * (255 - source.A) / 255) / alpha),
			(byte) ((source.R * source.A + destination.R * destination.A * (255 - source.A) / 255) / alpha),
			(byte) alpha);
	}
}
