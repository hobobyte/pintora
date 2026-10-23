using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Pintora.Avalonia.Canvas;

namespace Pintora.Avalonia;

public sealed class CropImageWindow : Window
{
	public CropImageWindow (CanvasHost canvas)
	{
		Title = "Crop image";
		Width = 360;
		Height = 300;
		var left = new NumericUpDown { Minimum = 0, Maximum = canvas.ImageSize.Width - 1, Value = 0 };
		var top = new NumericUpDown { Minimum = 0, Maximum = canvas.ImageSize.Height - 1, Value = 0 };
		var width = new NumericUpDown { Minimum = 1, Maximum = canvas.ImageSize.Width, Value = canvas.ImageSize.Width };
		var height = new NumericUpDown { Minimum = 1, Maximum = canvas.ImageSize.Height, Value = canvas.ImageSize.Height };
		var error = new TextBlock { Foreground = global::Avalonia.Media.Brushes.IndianRed };
		var apply = new Button { Content = "Crop", HorizontalAlignment = HorizontalAlignment.Right };
		apply.Click += (_, _) => {
			try {
				canvas.CropImage ((int) (left.Value ?? 0), (int) (top.Value ?? 0), (int) (width.Value ?? 1), (int) (height.Value ?? 1));
				Close ();
			} catch (ArgumentOutOfRangeException exception) { error.Text = exception.Message; }
		};
		Content = new StackPanel {
			Margin = new global::Avalonia.Thickness (16), Spacing = 8,
			Children = { new TextBlock { Text = "Left" }, left, new TextBlock { Text = "Top" }, top, new TextBlock { Text = "Width" }, width, new TextBlock { Text = "Height" }, height, error, apply }
		};
	}
}
