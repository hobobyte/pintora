using System;
using Avalonia.Media;

namespace Pintora.Avalonia;

public static class ColorSpace
{
	public static (double H, double S, double V) ToHsv (Color color)
	{
		double r = color.R / 255d, g = color.G / 255d, b = color.B / 255d;
		double max = Math.Max (r, Math.Max (g, b)), min = Math.Min (r, Math.Min (g, b)), delta = max - min;
		double h = delta == 0 ? 0 : max == r ? 60 * (((g - b) / delta) % 6) : max == g ? 60 * (((b - r) / delta) + 2) : 60 * (((r - g) / delta) + 4);
		if (h < 0) h += 360;
		return (h, max == 0 ? 0 : delta / max, max);
	}

	public static Color FromHsv (double h, double s, double v, byte alpha = 255)
	{
		h = ((h % 360) + 360) % 360;
		s = Math.Clamp (s, 0, 1); v = Math.Clamp (v, 0, 1);
		double c = v * s, x = c * (1 - Math.Abs ((h / 60 % 2) - 1)), m = v - c;
		(double r, double g, double b) = h < 60 ? (c, x, 0d) : h < 120 ? (x, c, 0d) : h < 180 ? (0d, c, x) : h < 240 ? (0d, x, c) : h < 300 ? (x, 0d, c) : (c, 0d, x);
		return Color.FromArgb (alpha, (byte) Math.Round ((r + m) * 255), (byte) Math.Round ((g + m) * 255), (byte) Math.Round ((b + m) * 255));
	}
}
