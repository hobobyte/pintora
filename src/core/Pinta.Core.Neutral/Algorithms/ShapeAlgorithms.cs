using System;
using System.Collections.Generic;

namespace Pinta.Core;

public enum ShapeDashPattern { Solid, Dash, Dot, DashDot }
public enum ShapePathKind { Line, Rectangle, Ellipse, RoundedRectangle }

public static class ShapeAlgorithms
{
	public static PointD RotatePoint (PointD point, PointD center, double radians)
	{
		double cosine = Math.Cos (radians), sine = Math.Sin (radians);
		double dx = point.X - center.X, dy = point.Y - center.Y;
		return new (center.X + dx * cosine - dy * sine, center.Y + dx * sine + dy * cosine);
	}

	public static IReadOnlyList<PointD> BuildPath (ShapePathKind kind, PointD from, PointD to, double cornerRadius)
	{
		if (kind == ShapePathKind.Line) return [from, to];
		RectangleD bounds = Bounds (from, to);
		if (kind == ShapePathKind.Rectangle)
			return [new (bounds.X, bounds.Y), new (bounds.X + bounds.Width, bounds.Y),
				new (bounds.X + bounds.Width, bounds.Y + bounds.Height), new (bounds.X, bounds.Y + bounds.Height), new (bounds.X, bounds.Y)];
		if (kind == ShapePathKind.Ellipse) {
			int steps = EllipseSampleCount (Math.Max (bounds.Width, bounds.Height) / 2);
			var points = new List<PointD> (steps + 1);
			for (int i = 0; i <= steps; i++) {
				double angle = 2 * Math.PI * i / steps;
				points.Add (new (bounds.GetCenter ().X + bounds.Width / 2 * Math.Cos (angle), bounds.GetCenter ().Y + bounds.Height / 2 * Math.Sin (angle)));
			}
			return points;
		}
		if (kind != ShapePathKind.RoundedRectangle) throw new ArgumentOutOfRangeException (nameof (kind));
		double radius = Math.Min (cornerRadius, Math.Min (bounds.Width / 2, bounds.Height / 2));
		if (radius <= 0) return BuildPath (ShapePathKind.Rectangle, from, to, 0);
		var rounded = new List<PointD> (ArcSampleCount (radius) * 4 + 5);
		AddArc (rounded, bounds.X + bounds.Width - radius, bounds.Y + radius, radius, -Math.PI / 2, 0);
		AddArc (rounded, bounds.X + bounds.Width - radius, bounds.Y + bounds.Height - radius, radius, 0, Math.PI / 2);
		AddArc (rounded, bounds.X + radius, bounds.Y + bounds.Height - radius, radius, Math.PI / 2, Math.PI);
		AddArc (rounded, bounds.X + radius, bounds.Y + radius, radius, Math.PI, 3 * Math.PI / 2);
		rounded.Add (rounded[0]);
		return rounded;
	}

	private static void AddArc (List<PointD> points, double cx, double cy, double radius, double start, double end)
	{
		int steps = ArcSampleCount (radius);
		for (int i = 0; i <= steps; i++) {
			double angle = start + (end - start) * i / steps;
			points.Add (new (cx + radius * Math.Cos (angle), cy + radius * Math.Sin (angle)));
		}
	}

	public static bool ContainsEllipse (PointD point, RectangleD bounds)
	{
		double rx = bounds.Width / 2, ry = bounds.Height / 2;
		if (rx == 0 || ry == 0) return true;
		PointD center = bounds.GetCenter ();
		double dx = (point.X - center.X) / rx, dy = (point.Y - center.Y) / ry;
		return dx * dx + dy * dy <= 1;
	}

	public static bool ContainsRoundedRectangle (PointD point, RectangleD bounds, double radius)
	{
		PointD center = bounds.GetCenter ();
		double rx = bounds.Width / 2, ry = bounds.Height / 2;
		radius = Math.Min (radius, Math.Min (rx, ry));
		if (radius <= 0) return true;
		double dx = Math.Max (Math.Abs (point.X - center.X) - (rx - radius), 0);
		double dy = Math.Max (Math.Abs (point.Y - center.Y) - (ry - radius), 0);
		return dx * dx + dy * dy <= radius * radius;
	}

	public static bool IsDashVisible (double distance, ShapeDashPattern pattern, int width)
	{
		int unit = Math.Max (1, width);
		int[] values = pattern switch {
			ShapeDashPattern.Solid => [1],
			ShapeDashPattern.Dash => [6, 4],
			ShapeDashPattern.Dot => [1, 3],
			ShapeDashPattern.DashDot => [6, 3, 1, 3],
			_ => throw new ArgumentOutOfRangeException (nameof (pattern))
		};
		if (values.Length == 1) return true;
		int cycle = 0;
		foreach (int value in values) cycle += value * unit;
		double position = distance % cycle;
		for (int i = 0; i < values.Length; i++) {
			position -= values[i] * unit;
			if (position < 0) return i % 2 == 0;
		}
		return true;
	}

	public static void Stamp (bool[] mask, int width, int height, int x, int y, int size)
	{
		ArgumentNullException.ThrowIfNull (mask);
		if (mask.Length != checked (width * height)) throw new ArgumentException ("Mask dimensions do not match.", nameof (mask));
		int firstX = x - (size - 1) / 2, firstY = y - (size - 1) / 2;
		for (int py = Math.Max (0, firstY); py <= Math.Min (height - 1, firstY + size - 1); py++)
			for (int px = Math.Max (0, firstX); px <= Math.Min (width - 1, firstX + size - 1); px++)
				mask[py * width + px] = true;
	}

	public static void Fill (IImageSurface surface, ShapePathKind kind, RectangleD bounds, ColorBgra color, double cornerRadius)
	{
		ArgumentNullException.ThrowIfNull (surface);
		Span<ColorBgra> pixels = surface.GetPixelData ();
		for (int y = Math.Max (0, (int) Math.Ceiling (bounds.Y)); y <= Math.Min (surface.Height - 1, (int) Math.Floor (bounds.Y + bounds.Height)); y++)
			for (int x = Math.Max (0, (int) Math.Ceiling (bounds.X)); x <= Math.Min (surface.Width - 1, (int) Math.Floor (bounds.X + bounds.Width)); x++) {
				PointD point = new (x, y);
				bool inside = kind switch {
					ShapePathKind.Ellipse => ContainsEllipse (point, bounds),
					ShapePathKind.RoundedRectangle => ContainsRoundedRectangle (point, bounds, cornerRadius),
					_ => true
				};
				if (inside) {
					int index = y * surface.Width + x;
					pixels[index] = RasterAlgorithms.CompositeOver (pixels[index], color);
				}
			}
	}

	public static void Outline (IImageSurface surface, IReadOnlyList<PointD> path, ColorBgra color, int width, ShapeDashPattern dash)
	{
		ArgumentNullException.ThrowIfNull (surface);
		ArgumentNullException.ThrowIfNull (path);
		var mask = new bool[checked (surface.Width * surface.Height)];
		double distance = 0;
		for (int i = 1; i < path.Count; i++) {
			PointD a = path[i - 1], b = path[i];
			double length = Math.Sqrt ((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
			int steps = LineSampleCount (length);
			for (int step = 0; step <= steps; step++) {
				double t = step / (double) steps;
				if (IsDashVisible (distance + length * t, dash, width))
					Stamp (mask, surface.Width, surface.Height,
						(int) Math.Round (a.X + (b.X - a.X) * t),
						(int) Math.Round (a.Y + (b.Y - a.Y) * t), width);
			}
			distance += length;
		}
		Span<ColorBgra> pixels = surface.GetPixelData ();
		for (int i = 0; i < mask.Length; i++)
			if (mask[i]) pixels[i] = RasterAlgorithms.CompositeOver (pixels[i], color);
	}

	public static RectangleD Bounds (PointD from, PointD to)
		=> RectangleD.FromPoints (from, to);

	public static RectangleD RotatedBounds (RectangleD bounds, double angleDegrees)
	{
		double radians = angleDegrees * Math.PI / 180;
		double cosine = Math.Abs (Math.Cos (radians));
		double sine = Math.Abs (Math.Sin (radians));
		double width = bounds.Width * cosine + bounds.Height * sine;
		double height = bounds.Width * sine + bounds.Height * cosine;
		PointD center = bounds.GetCenter ();
		return new (center.X - width / 2, center.Y - height / 2, width, height);
	}

	public static int EllipseSampleCount (double radius)
		=> Math.Max (64, (int) Math.Ceiling (2 * Math.PI * Math.Max (radius, 0)));

	public static int ArcSampleCount (double radius)
		=> Math.Max (8, (int) Math.Ceiling (Math.Max (radius, 0) * Math.PI / 2));

	public static int LineSampleCount (double length, double density = 2)
		=> Math.Max (1, (int) Math.Ceiling (Math.Max (length, 0) * density));
}
