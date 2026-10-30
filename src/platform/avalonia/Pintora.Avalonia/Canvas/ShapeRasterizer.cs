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
		RectangleD bounds = ShapeAlgorithms.Bounds (from, to);
		double left = bounds.X, top = bounds.Y, right = bounds.X + bounds.Width, bottom = bounds.Y + bounds.Height;
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
		RectangleD bounds = ShapeAlgorithms.Bounds (from, to);
		float left = (float) bounds.X, top = (float) bounds.Y;
		float right = (float) (bounds.X + bounds.Width), bottom = (float) (bounds.Y + bounds.Height);
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
				AppendGtkEllipsePath (path, rect);
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
		RectangleD rotated = ShapeAlgorithms.RotatedBounds (bounds, angle);
		double rotatedLeft = rotated.X, rotatedTop = rotated.Y;
		double rotatedRight = rotated.X + rotated.Width, rotatedBottom = rotated.Y + rotated.Height;
		int minX = Math.Max (0, (int) Math.Floor (rotatedLeft) - margin);
		int minY = Math.Max (0, (int) Math.Floor (rotatedTop) - margin);
		int maxX = Math.Min (surface.Width - 1, (int) Math.Ceiling (rotatedRight) + margin);
		int maxY = Math.Min (surface.Height - 1, (int) Math.Ceiling (rotatedBottom) + margin);
		for (int y = minY; y <= maxY; y++)
			for (int x = minX; x <= maxX; x++) {
				SKColor color = overlay.GetPixel (x, y);
				if (color.Alpha == 0) continue;
				int index = y * surface.Width + x;
				pixels[index] = RasterAlgorithms.CompositeOver (pixels[index], ColorBgra.FromBgra (color.Blue, color.Green, color.Red, color.Alpha));
			}
	}

	private static void AppendGtkEllipsePath (SKPath path, SKRect rect)
	{
		const float c = 0.552285f;
		float rx = rect.Width / 2, ry = rect.Height / 2;
		float cx = rect.MidX, cy = rect.MidY;
		path.MoveTo (cx + rx, cy);
		path.CubicTo (cx + rx, cy - c * ry, cx + c * rx, cy - ry, cx, cy - ry);
		path.CubicTo (cx - c * rx, cy - ry, cx - rx, cy - c * ry, cx - rx, cy);
		path.CubicTo (cx - rx, cy + c * ry, cx - c * rx, cy + ry, cx, cy + ry);
		path.CubicTo (cx + c * rx, cy + ry, cx + rx, cy + c * ry, cx + rx, cy);
		path.Close ();
	}

	private static void Fill (MemoryImageSurface surface, CanvasHost.ShapeKind kind,
		double left, double top, double right, double bottom, ColorBgra color, int cornerRadius)
		=> ShapeAlgorithms.Fill (surface, (ShapePathKind) kind, new RectangleD (left, top, right - left, bottom - top), color, cornerRadius);

	private static List<PointD> BuildPath (CanvasHost.ShapeKind kind, PointD from, PointD to, int cornerRadius)
		=> [.. ShapeAlgorithms.BuildPath ((ShapePathKind) kind, from, to, cornerRadius)];

	private static void Outline (MemoryImageSurface surface, List<PointD> path, ColorBgra color, int width, CanvasHost.ShapeDashStyle dash)
		=> ShapeAlgorithms.Outline (surface, path, color, width, ToDashPattern (dash));

	private static ShapeDashPattern ToDashPattern (CanvasHost.ShapeDashStyle dash)
		=> dash switch {
			CanvasHost.ShapeDashStyle.Solid => ShapeDashPattern.Solid,
			CanvasHost.ShapeDashStyle.Dash => ShapeDashPattern.Dash,
			CanvasHost.ShapeDashStyle.Dot => ShapeDashPattern.Dot,
			CanvasHost.ShapeDashStyle.DashDot => ShapeDashPattern.DashDot,
			_ => throw new ArgumentOutOfRangeException (nameof (dash))
		};

}
