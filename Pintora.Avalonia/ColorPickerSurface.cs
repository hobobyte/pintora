using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Pintora.Avalonia;

internal sealed class ColorPickerSurface : Control
{
	public enum SurfaceMode { HueAndSaturation, SaturationAndValue }
	private SurfaceMode mode;
	private Color color = Colors.White;
	private bool showValue = true;
	private WriteableBitmap? bitmap;
	private int bitmapSize;
	private bool bitmapDirty = true;

	public event EventHandler<Color>? ColorChanged;
	public SurfaceMode Mode { get => mode; set { if (mode == value) return; mode = value; InvalidateSurface (); } }
	public Color Color { get => color; set { if (color == value) return; color = value; InvalidateSurface (); } }
	public bool ShowValue { get => showValue; set { if (showValue == value) return; showValue = value; InvalidateSurface (); } }

	public ColorPickerSurface ()
	{
		Width = 168;
		Height = 168;
		PointerPressed += OnPointer;
		PointerMoved += OnPointer;
		PointerReleased += OnPointerReleased;
	}

	public override void Render (DrawingContext context)
	{
		int size = Math.Max (1, (int) Math.Min (Bounds.Width, Bounds.Height));
		int radius = size / 2;
		var hsv = ColorSpace.ToHsv (color);
		if (bitmap is null || bitmapDirty || bitmapSize != size) {
			bitmap?.Dispose ();
			bitmap = CreateBitmap (size, radius, hsv);
			bitmapSize = size;
			bitmapDirty = false;
		}
		context.DrawImage (bitmap, new Rect (0, 0, size, size), new Rect (0, 0, size, size));
		Point cursor = GetCursor (hsv, radius);
		context.DrawEllipse (null, new Pen (Brushes.Black, 2), cursor, 6, 6);
		context.DrawEllipse (null, new Pen (Brushes.White, 1), cursor, 5, 5);
	}

	private WriteableBitmap CreateBitmap (int size, int radius, (double H, double S, double V) hsv)
	{
		var pixels = new byte[size * size * 4];
		for (int y = 0; y < size; y++) {
			for (int x = 0; x < size; x++) {
				double dx = x - radius + 0.5, dy = y - radius + 0.5;
				Color pixel;
				if (Mode == SurfaceMode.HueAndSaturation) {
					double distance = Math.Sqrt (dx * dx + dy * dy);
					if (distance > radius) { pixels[(y * size + x) * 4 + 3] = 0; continue; }
					double hue = (Math.Atan2 (dy, -dx) + Math.PI) / (2 * Math.PI) * 360;
					double saturation = Math.Min (distance / radius, 1);
					pixel = ColorSpace.FromHsv (hue, saturation, ShowValue ? hsv.V : 1, 255);
				} else {
					double saturation = 1 - Math.Clamp ((double) y / Math.Max (1, size - 1), 0, 1);
					double value = Math.Clamp ((double) x / Math.Max (1, size - 1), 0, 1);
					pixel = ColorSpace.FromHsv (hsv.H, saturation, value, 255);
				}
				int offset = (y * size + x) * 4;
				pixels[offset] = pixel.B; pixels[offset + 1] = pixel.G; pixels[offset + 2] = pixel.R; pixels[offset + 3] = pixel.A;
			}
		}
		var result = new WriteableBitmap (new PixelSize (size, size), new Vector (96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
		using (ILockedFramebuffer locked = result.Lock ())
			for (int y = 0; y < size; y++)
				Marshal.Copy (pixels, y * size * 4, locked.Address + y * locked.RowBytes, size * 4);
		return result;
	}

	private void InvalidateSurface () { bitmapDirty = true; InvalidateVisual (); }

	protected override void OnDetachedFromVisualTree (VisualTreeAttachmentEventArgs e)
	{
		bitmap?.Dispose ();
		bitmap = null;
		base.OnDetachedFromVisualTree (e);
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
		if (args is PointerPressedEventArgs pressed) {
			if (!pressed.GetCurrentPoint (this).Properties.IsLeftButtonPressed) return;
			pressed.Pointer.Capture (this);
		} else if (!args.GetCurrentPoint (this).Properties.IsLeftButtonPressed) return;
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

	private void OnPointerReleased (object? sender, PointerReleasedEventArgs args)
	{
		if (args.InitialPressMouseButton == MouseButton.Left)
			args.Pointer.Capture (null);
	}
}
