using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Pintora.Avalonia;

internal sealed class ColorPickerSurface : Control
{
	public enum SurfaceMode { HueAndSaturation, SaturationAndValue }
	private SurfaceMode mode;
	private Color color = Colors.White;
	private bool showValue = true;

	public event EventHandler<Color>? ColorChanged;
	public SurfaceMode Mode { get => mode; set { mode = value; InvalidateVisual (); } }
	public Color Color { get => color; set { color = value; InvalidateVisual (); } }
	public bool ShowValue { get => showValue; set { showValue = value; InvalidateVisual (); } }

	public ColorPickerSurface ()
	{
		Width = 168;
		Height = 168;
		PointerPressed += OnPointer;
		PointerMoved += OnPointer;
	}

	public override void Render (DrawingContext context)
	{
		int size = Math.Max (1, (int) Math.Min (Bounds.Width, Bounds.Height));
		int radius = size / 2;
		var hsv = ColorSpace.ToHsv (color);
		for (int y = 0; y < size; y++) {
			for (int x = 0; x < size; x++) {
				double dx = x - radius + 0.5, dy = y - radius + 0.5;
				Color pixel;
				if (Mode == SurfaceMode.HueAndSaturation) {
					double distance = Math.Sqrt (dx * dx + dy * dy);
					if (distance > radius) continue;
					double hue = (Math.Atan2 (dy, -dx) + Math.PI) / (2 * Math.PI) * 360;
					double saturation = Math.Min (distance / radius, 1);
					pixel = ColorSpace.FromHsv (hue, saturation, ShowValue ? hsv.V : 1, 1);
				} else {
					double saturation = 1 - Math.Clamp ((double) y / Math.Max (1, size - 1), 0, 1);
					double value = Math.Clamp ((double) x / Math.Max (1, size - 1), 0, 1);
					pixel = ColorSpace.FromHsv (hsv.H, saturation, value, 1);
				}
				context.FillRectangle (new SolidColorBrush (pixel), new Rect (x, y, 1, 1));
			}
		}
		Point cursor = GetCursor (hsv, radius);
		context.DrawEllipse (null, new Pen (Brushes.Black, 2), cursor, 6, 6);
		context.DrawEllipse (null, new Pen (Brushes.White, 1), cursor, 5, 5);
	}

	private Point GetCursor ((double H, double S, double V) hsv, int radius)
	{
		if (Mode == SurfaceMode.HueAndSaturation) {
			double angle = hsv.H * Math.PI / 180;
			return new Point (radius - Math.Cos (angle) * hsv.S * radius, radius + Math.Sin (angle) * hsv.S * radius);
		}
		return new Point (hsv.V * (radius * 2 - 1), (1 - hsv.S) * (radius * 2 - 1));
	}

	private void OnPointer (object? sender, PointerEventArgs args)
	{
		if (!args.GetCurrentPoint (this).Properties.IsLeftButtonPressed) return;
		Point point = args.GetPosition (this);
		int size = Math.Max (1, (int) Math.Min (Bounds.Width, Bounds.Height));
		int radius = size / 2;
		var hsv = ColorSpace.ToHsv (color);
		if (Mode == SurfaceMode.HueAndSaturation) {
			double dx = point.X - radius, dy = point.Y - radius;
			hsv = (H: (Math.Atan2 (dy, -dx) + Math.PI) / (2 * Math.PI) * 360, S: Math.Clamp (Math.Sqrt (dx * dx + dy * dy) / radius, 0, 1), V: hsv.V);
		} else {
			hsv = (hsv.H, 1 - Math.Clamp (point.Y / Math.Max (1, size - 1), 0, 1), Math.Clamp (point.X / Math.Max (1, size - 1), 0, 1));
		}
		color = ColorSpace.FromHsv (hsv.H, hsv.S, hsv.V, color.A);
		ColorChanged?.Invoke (this, color);
		InvalidateVisual ();
	}
}
