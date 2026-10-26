using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Pintora.Avalonia.Canvas;

namespace Pintora.Avalonia;

public sealed class ResizeCanvasWindow : Window
{
	public ResizeCanvasWindow (CanvasHost canvas)
	{
		Title = "Resize canvas";
		Width = 360;
		Height = 220;
		var width = new NumericUpDown { Minimum = 1, Maximum = 10000, Value = canvas.ImageSize.Width };
		var height = new NumericUpDown { Minimum = 1, Maximum = 10000, Value = canvas.ImageSize.Height };
		var error = new TextBlock { Foreground = global::Avalonia.Media.Brushes.IndianRed };
		var apply = new Button { Content = "Resize canvas", HorizontalAlignment = HorizontalAlignment.Right };
		apply.Click += (_, _) => {
			try {
				canvas.ResizeCanvas ((int) (width.Value ?? canvas.ImageSize.Width), (int) (height.Value ?? canvas.ImageSize.Height));
				Close ();
			} catch (ArgumentOutOfRangeException exception) {
				error.Text = exception.Message;
			}
		};
		Content = new StackPanel {
			Margin = new global::Avalonia.Thickness (16),
			Spacing = 10,
			Children = {
				new TextBlock { Text = "Width" }, width,
				new TextBlock { Text = "Height" }, height,
				error, apply
			}
		};
	}
}
