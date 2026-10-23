using Avalonia.Controls;
using Avalonia.Layout;
using Pintora.Avalonia.Input;

namespace Pintora.Avalonia;

public sealed class KeyboardSettingsWindow : Window
{
	public KeyboardSettingsWindow (PintoraKeyboardSettings settings)
	{
		Title = "Keyboard shortcuts";
		Width = 420;
		Height = 300;
		CanResize = false;

		var rows = new StackPanel { Spacing = 8 };
		rows.Children.Add (CreateRow ("Undo", PintoraKeyCommands.Undo, settings));
		rows.Children.Add (CreateRow ("Redo", PintoraKeyCommands.Redo, settings));
		rows.Children.Add (CreateRow ("Decrease brush", PintoraKeyCommands.DecreaseBrush, settings));
		rows.Children.Add (CreateRow ("Increase brush", PintoraKeyCommands.IncreaseBrush, settings));
		rows.Children.Add (CreateRow ("Toggle eraser", PintoraKeyCommands.ToggleEraser, settings));
		Content = new Border {
			Padding = new global::Avalonia.Thickness (20),
			Child = rows
		};
	}

	private static Control CreateRow (string title, PintoraKeyCommand command, PintoraKeyboardSettings settings)
	{
		var input = new TextBox { Text = settings.GestureTextFor (command), Width = 150 };
		input.LostFocus += (_, _) => settings.SetGesture (command, input.Text);
		var row = new Grid {
			ColumnDefinitions = new ColumnDefinitions ("*,Auto"),
			Children = {
				new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center },
				input
			}
		};
		Grid.SetColumn (input, 1);
		return row;
	}
}
