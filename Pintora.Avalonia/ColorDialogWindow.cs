using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
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
		var hue = Number (0, 360, 68); var saturation = Number (0, 100, 68); var value = Number (0, 100, 68);
		var red = Number (0, 255, 68); var green = Number (0, 255, 68); var blue = Number (0, 255, 68); var alpha = Number (0, 255, 68);
		void PaintPreview () { preview.Background = new SolidColorBrush (current); previous.Background = new SolidColorBrush (initial); hex.Text = current.ToString ().TrimStart ('#'); }
		void SetRgb (Color color)
		{
			syncing = true; current = color; var hsv = ColorSpace.ToHsv (color);
			red.Value = color.R; green.Value = color.G; blue.Value = color.B; alpha.Value = color.A;
			hue.Value = (decimal) hsv.H; saturation.Value = (decimal) (hsv.S * 100); value.Value = (decimal) (hsv.V * 100); PaintPreview (); syncing = false;
		}
		void SetHsv () { if (syncing) return; current = ColorSpace.FromHsv ((double) (hue.Value ?? 0), (double) (saturation.Value ?? 0) / 100, (double) (value.Value ?? 0) / 100, (byte) (alpha.Value ?? 255)); SetRgb (current); }
		void SetRgbFromFields () { if (syncing) return; SetRgb (Color.FromArgb ((byte) (alpha.Value ?? 255), (byte) (red.Value ?? 0), (byte) (green.Value ?? 0), (byte) (blue.Value ?? 0))); }
		foreach (NumericUpDown input in new[] { red, green, blue, alpha }) input.ValueChanged += (_, _) => SetRgbFromFields ();
		foreach (NumericUpDown input in new[] { hue, saturation, value }) input.ValueChanged += (_, _) => SetHsv ();
		hex.TextChanged += (_, _) => { if (syncing) return; string text = hex.Text?.Trim () ?? string.Empty; if (text.Length is 6 or 8 && Color.TryParse ($"#{text}", out Color parsed)) SetRgb (parsed); };
		SetRgb (initial);
		var reset = new Button { Content = "Reset", MinWidth = 72 }; reset.Click += (_, _) => SetRgb (initial);
		var cancel = new Button { Content = "Cancel", MinWidth = 72 }; cancel.Click += (_, _) => Close ();
		var ok = new Button { Content = "OK", MinWidth = 72, Background = new SolidColorBrush (Color.Parse ("#3B8CFF")) }; ok.Click += (_, _) => { apply (current); Close (); };
		Content = new StackPanel { Margin = new Thickness (14), Spacing = 10, Children = {
			new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { reset, new TextBlock { Text = "Choose Colors", FontSize = 16, FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center }, new Border { Width = 1 }, cancel, ok } },
			new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = {
				new StackPanel { Width = 220, Spacing = 7, Children = { new Border { Width = 148, Height = 148, CornerRadius = new CornerRadius (74), Background = new LinearGradientBrush { StartPoint = new RelativePoint (0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint (1, 1, RelativeUnit.Relative), GradientStops = { new GradientStop (Colors.Red, 0), new GradientStop (Colors.Blue, 0.5), new GradientStop (Colors.Red, 1) } } }, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { previous, preview } }, new CheckBox { Content = "Show value" } } },
				new StackPanel { Spacing = 6, Children = { Row ("Hex", hex), new TextBlock { Text = "Hue / Saturation / Value", FontWeight = FontWeight.Bold }, Row ("Hue", hue), Row ("Sat", saturation), Row ("Value", value), new TextBlock { Text = "Red / Green / Blue", FontWeight = FontWeight.Bold }, Row ("Red", red), Row ("Green", green), Row ("Blue", blue), Row ("Alpha", alpha) } }
			} } } };
	}
	private static NumericUpDown Number (decimal min, decimal max, double width) => new () { Minimum = min, Maximum = max, Width = width };
	private static Control Row (string label, Control control) => new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { new TextBlock { Text = label, Width = 58, VerticalAlignment = VerticalAlignment.Center }, control } };
}
