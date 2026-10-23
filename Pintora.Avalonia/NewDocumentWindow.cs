using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Pintora.Avalonia.Canvas;

namespace Pintora.Avalonia;

public sealed class NewDocumentWindow : Window
{
	public NewDocumentWindow (CanvasHost canvas)
	{
		Title = "New image";
		Width = 360;
		Height = 220;
		var width = new NumericUpDown { Minimum = 1, Maximum = 10000, Value = 800 };
		var height = new NumericUpDown { Minimum = 1, Maximum = 10000, Value = 600 };
		var error = new TextBlock { Foreground = global::Avalonia.Media.Brushes.IndianRed };
		var create = new Button { Content = "Create", HorizontalAlignment = HorizontalAlignment.Right };
		create.Click += (_, _) => {
			try {
				canvas.NewDocument ((int) (width.Value ?? 800), (int) (height.Value ?? 600));
				Close ();
			} catch (ArgumentOutOfRangeException exception) {
				error.Text = exception.Message;
			}
		};
		Content = new StackPanel {
			Margin = new global::Avalonia.Thickness (16), Spacing = 10,
			Children = { new TextBlock { Text = "Width" }, width, new TextBlock { Text = "Height" }, height, error, create }
		};
	}
}
