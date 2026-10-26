using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Pintora.Avalonia.Canvas;

namespace Pintora.Avalonia;

public sealed class LayerRenameWindow : Window
{
	public LayerRenameWindow (CanvasHost canvas, string currentName, Action refresh)
	{
		Title = "Rename layer";
		Width = 360;
		Height = 140;
		var input = new TextBox { Text = currentName };
		var button = new Button { Content = "Rename", HorizontalAlignment = HorizontalAlignment.Right };
		button.Click += (_, _) => {
			canvas.RenameLayer (input.Text ?? currentName);
			refresh ();
			Close ();
		};
		Content = new StackPanel { Margin = new global::Avalonia.Thickness (16), Spacing = 10, Children = { input, button } };
	}
}
