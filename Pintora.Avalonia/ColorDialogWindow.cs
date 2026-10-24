using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;

namespace Pintora.Avalonia;

public sealed class ColorDialogWindow : Window
{
	public ColorDialogWindow (Color initial, Action<Color> apply)
	{
		Title = "Choose Colors";
		Width = 510;
		Height = 430;
		CanResize = false;
		Color current = initial;
		bool syncing = false;
		var hex = new TextBox { Width = 112, PlaceholderText = "RRGGBBAA" };
		var preview = new Border { Width = 42, Height = 42, CornerRadius = new CornerRadius (4), BorderThickness = new Thickness (2) };
		var previous = new Border { Width = 42, Height = 42, CornerRadius = new CornerRadius (4), BorderThickness = new Thickness (2) };
		preview.Bind (Border.BorderBrushProperty, new DynamicResourceExtension ("Pintora.Control.Border"));
		previous.Bind (Border.BorderBrushProperty, new DynamicResourceExtension ("Pintora.Control.Border"));
		var hue = Number (0, 360, 68); var saturation = Number (0, 100, 68); var value = Number (0, 100, 68);
		var red = Number (0, 255, 68); var green = Number (0, 255, 68); var blue = Number (0, 255, 68); var alpha = Number (0, 255, 68);
		var picker = new ColorPickerSurface { Color = initial };
		var hueSat = new Button { Content = "Hue & Sat" };
		var satValue = new Button { Content = "Sat & Value" };
		var showValue = new CheckBox { Content = "Show value", IsChecked = true };
		hueSat.Click += (_, _) => picker.Mode = ColorPickerSurface.SurfaceMode.HueAndSaturation;
		satValue.Click += (_, _) => picker.Mode = ColorPickerSurface.SurfaceMode.SaturationAndValue;
		showValue.IsCheckedChanged += (_, _) => picker.ShowValue = showValue.IsChecked == true;
		void PaintPreview () {
			preview.Background = new SolidColorBrush (current);
			previous.Background = new SolidColorBrush (initial);
			hex.Text = $"{current.R:X2}{current.G:X2}{current.B:X2}{current.A:X2}";
		}
		void SetRgb (Color color)
		{
			syncing = true; current = color; var hsv = ColorSpace.ToHsv (color);
			picker.Color = color;
			red.Value = color.R; green.Value = color.G; blue.Value = color.B; alpha.Value = color.A;
			hue.Value = (decimal) hsv.H; saturation.Value = (decimal) (hsv.S * 100); value.Value = (decimal) (hsv.V * 100); PaintPreview (); syncing = false;
		}
		picker.ColorChanged += (_, color) => SetRgb (color);
		void SetHsv () { if (syncing) return; current = ColorSpace.FromHsv ((double) (hue.Value ?? 0), (double) (saturation.Value ?? 0) / 100, (double) (value.Value ?? 0) / 100, (byte) (alpha.Value ?? 255)); SetRgb (current); }
		void SetRgbFromFields () { if (syncing) return; SetRgb (Color.FromArgb ((byte) (alpha.Value ?? 255), (byte) (red.Value ?? 0), (byte) (green.Value ?? 0), (byte) (blue.Value ?? 0))); }
		foreach (NumericUpDown input in new[] { red, green, blue, alpha }) input.ValueChanged += (_, _) => SetRgbFromFields ();
		foreach (NumericUpDown input in new[] { hue, saturation, value }) input.ValueChanged += (_, _) => SetHsv ();
		hex.TextChanged += (_, _) => {
			if (syncing)
				return;
			string text = hex.Text?.Trim ().TrimStart ('#') ?? string.Empty;
			if (text.Length is not (6 or 8) || !TryParseRgba (text, out Color parsed))
				return;
			SetRgb (parsed);
		};
		SetRgb (initial);
		var reset = new Button { Content = "Reset", MinWidth = 72 }; reset.Click += (_, _) => SetRgb (initial);
		var cancel = new Button { Content = "Cancel", MinWidth = 72 }; cancel.Click += (_, _) => Close ();
		var ok = new Button { Content = "OK", MinWidth = 72 }; ok.Bind (Button.BackgroundProperty, new DynamicResourceExtension ("Pintora.Accent")); ok.Click += (_, _) => { apply (current); Close (); };
		Content = new StackPanel { Margin = new Thickness (14), Spacing = 10, Children = {
			new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { reset, new TextBlock { Text = "Choose Colors", FontSize = 16, FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center }, new Border { Width = 1 }, cancel, ok } },
			new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = {
				new StackPanel { Width = 220, Spacing = 7, Children = { new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { hueSat, satValue } }, picker, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { previous, preview } }, showValue } },
				new StackPanel { Spacing = 6, Children = { Row ("Hex", hex), new TextBlock { Text = "Hue / Saturation / Value", FontWeight = FontWeight.Bold }, Row ("Hue", hue), Row ("Sat", saturation), Row ("Value", value), new TextBlock { Text = "Red / Green / Blue", FontWeight = FontWeight.Bold }, Row ("Red", red), Row ("Green", green), Row ("Blue", blue), Row ("Alpha", alpha) } }
			} } } };
	}
	private static NumericUpDown Number (decimal min, decimal max, double width) => new () { Minimum = min, Maximum = max, Width = width };
	private static Control Row (string label, Control control) => new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { new TextBlock { Text = label, Width = 58, VerticalAlignment = VerticalAlignment.Center }, control } };
	private static bool TryParseRgba (string text, out Color color)
	{
		color = Colors.Transparent;
		if (!uint.TryParse (text, System.Globalization.NumberStyles.HexNumber, null, out uint value))
			return false;
		byte r = (byte) (value >> 16);
		byte g = (byte) (value >> 8);
		byte b = (byte) value;
		byte a = text.Length == 8 ? (byte) value : (byte) 255;
		if (text.Length == 8) {
			r = (byte) (value >> 24);
			g = (byte) (value >> 16);
			b = (byte) (value >> 8);
			a = (byte) value;
		}
		color = Color.FromArgb (a, r, g, b);
		return true;
	}
}
