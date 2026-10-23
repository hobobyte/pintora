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
		Title = "Choose color";
		Width = 320;
		Height = 230;
		CanResize = false;

		var value = new TextBox { Text = initial.ToString (), Width = 150 };
		var preview = new Border { Width = 32, Height = 32, Background = new SolidColorBrush (initial) };
		var hsvLabel = new TextBlock ();
		var hue = new NumericUpDown { Minimum = 0, Maximum = 360, Value = (decimal) ColorSpace.ToHsv (initial).H, Width = 64 };
		var saturation = new NumericUpDown { Minimum = 0, Maximum = 100, Value = (decimal) (ColorSpace.ToHsv (initial).S * 100), Width = 64 };
		var brightness = new NumericUpDown { Minimum = 0, Maximum = 100, Value = (decimal) (ColorSpace.ToHsv (initial).V * 100), Width = 64 };
		bool syncing = false;
		var red = new NumericUpDown { Minimum = 0, Maximum = 255, Value = initial.R, Width = 58 };
		var green = new NumericUpDown { Minimum = 0, Maximum = 255, Value = initial.G, Width = 58 };
		var blue = new NumericUpDown { Minimum = 0, Maximum = 255, Value = initial.B, Width = 58 };
		void RefreshFromRgb ()
		{
			syncing = true;
			var color = Color.FromArgb (initial.A, (byte) (red.Value ?? 0), (byte) (green.Value ?? 0), (byte) (blue.Value ?? 0));
			value.Text = color.ToString ();
			preview.Background = new SolidColorBrush (color);
			var hsv = ColorSpace.ToHsv (color);
			hsvLabel.Text = $"HSV: {hsv.H:0}°, {hsv.S:P0}, {hsv.V:P0}";
			hue.Value = (decimal) hsv.H; saturation.Value = (decimal) (hsv.S * 100); brightness.Value = (decimal) (hsv.V * 100);
			syncing = false;
		}
		void RefreshFromHsv ()
		{
			if (syncing) return;
			var color = ColorSpace.FromHsv ((double) (hue.Value ?? 0), (double) (saturation.Value ?? 0) / 100, (double) (brightness.Value ?? 0) / 100, initial.A);
			red.Value = color.R; green.Value = color.G; blue.Value = color.B;
		}
		red.ValueChanged += (_, _) => RefreshFromRgb ();
		green.ValueChanged += (_, _) => RefreshFromRgb ();
		blue.ValueChanged += (_, _) => RefreshFromRgb ();
		hue.ValueChanged += (_, _) => RefreshFromHsv ();
		saturation.ValueChanged += (_, _) => RefreshFromHsv ();
		brightness.ValueChanged += (_, _) => RefreshFromHsv ();
		RefreshFromRgb ();
		value.TextChanged += (_, _) => {
			if (Color.TryParse (value.Text, out Color color))
				preview.Background = new SolidColorBrush (color);
		};
		var applyButton = new Button { Content = "Apply" };
		applyButton.Click += (_, _) => {
			if (Color.TryParse (value.Text, out Color color)) {
				apply (color);
				Close ();
			}
		};
		var cancelButton = new Button { Content = "Cancel" };
		cancelButton.Click += (_, _) => Close ();
		Content = new StackPanel {
			Margin = new Thickness (16),
			Spacing = 10,
			Children = {
				new TextBlock { Text = "Hex color (#AARRGGBB or #RRGGBB)" },
				new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { value, preview } },
				new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = "R" }, red, new TextBlock { Text = "G" }, green, new TextBlock { Text = "B" }, blue } },
				new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = "H" }, hue, new TextBlock { Text = "S%" }, saturation, new TextBlock { Text = "V%" }, brightness } },
				hsvLabel,
				new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Children = { cancelButton, applyButton } }
			}
		};
	}
}
