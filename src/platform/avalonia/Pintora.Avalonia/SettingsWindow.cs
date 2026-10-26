using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
using Pinta.Core;
using Pintora.Avalonia.Canvas;

namespace Pintora.Avalonia;

public sealed class SettingsWindow : Window
{
	public SettingsWindow (CanvasHost canvas, SettingsManager settings, Input.PintoraKeyboardSettings keyboard)
	{
		Title = "Settings";
		Width = 520;
		Height = 520;
		CanResize = false;

		string savedTheme = settings.GetSetting ("Pintora.Theme", "System");
		var theme = new ComboBox { Width = 180, SelectedIndex = savedTheme == "Light" ? 1 : savedTheme == "Dark" ? 2 : 0, ItemsSource = new[] { "System", "Light", "Dark" } };
		theme.SelectionChanged += (_, _) => {
			if (theme.SelectedIndex == 1) App.SetTheme (ThemeVariant.Light);
			else if (theme.SelectedIndex == 2) App.SetTheme (ThemeVariant.Dark);
			else App.SetTheme (ThemeVariant.Default);
			settings.PutSetting ("Pintora.Theme", theme.SelectedIndex == 1 ? "Light" : theme.SelectedIndex == 2 ? "Dark" : "System");
		};

		var shapeFill = new ComboBox { Width = 180, ItemsSource = new[] { "Outline", "Fill", "Fill + outline" }, SelectedIndex = (int) canvas.FillStyle };
		shapeFill.SelectionChanged += (_, _) => {
			if (shapeFill.SelectedIndex < 0) return;
			canvas.FillStyle = (CanvasHost.ShapeFillStyle) shapeFill.SelectedIndex;
			settings.PutSetting ("Pintora.Shape.FillStyle", canvas.FillStyle.ToString ());
		};
		var reversed = new CheckBox { Content = "Reverse gradients by default", IsChecked = canvas.GradientReversed };
		reversed.IsCheckedChanged += (_, _) => { canvas.GradientReversed = reversed.IsChecked == true; settings.PutSetting ("Pintora.Gradient.Reversed", canvas.GradientReversed); };

		var brush = new Slider { Minimum = 1, Maximum = 256, Value = canvas.BrushSize, Width = 220 };
		brush.PropertyChanged += (_, args) => { if (args.Property == Slider.ValueProperty) { canvas.BrushSize = (int) brush.Value; settings.PutSetting ("Pintora.Brush.Size", canvas.BrushSize); } };
		var opacity = new Slider { Minimum = 0, Maximum = 1, Value = canvas.BrushOpacity, Width = 220 };
		opacity.PropertyChanged += (_, args) => { if (args.Property == Slider.ValueProperty) { canvas.BrushOpacity = opacity.Value; settings.PutSetting ("Pintora.Brush.Opacity", canvas.BrushOpacity); } };
		var hardness = new Slider { Minimum = 0, Maximum = 1, Value = canvas.BrushHardness, Width = 220 };
		hardness.PropertyChanged += (_, args) => { if (args.Property == Slider.ValueProperty) { canvas.BrushHardness = hardness.Value; settings.PutSetting ("Pintora.Brush.Hardness", canvas.BrushHardness); } };
		var tolerance = new Slider { Minimum = 0, Maximum = 255, Value = canvas.FillTolerance, Width = 220 };
		tolerance.PropertyChanged += (_, args) => { if (args.Property == Slider.ValueProperty) { canvas.FillTolerance = (int) tolerance.Value; settings.PutSetting ("Pintora.Fill.Tolerance", canvas.FillTolerance); } };

		var open_shortcuts = new Button { Content = "Configure keyboard shortcuts" };
		open_shortcuts.Click += (_, _) => new KeyboardSettingsWindow (keyboard).Show (this);
		var content = new StackPanel {
			Spacing = 12,
			Children = {
			new TextBlock { Text = "Appearance", FontSize = 17 },
			Row ("Theme", theme),
			new Separator (),
			new TextBlock { Text = "Brush and tools", FontSize = 17 },
			Row ("Brush size", brush),
			Row ("Brush opacity", opacity),
			Row ("Brush hardness", hardness),
			Row ("Fill tolerance", tolerance),
			Row ("Shape fill style", shapeFill), reversed,
			new Separator (), open_shortcuts
		}
		};
		Content = new Border { Padding = new global::Avalonia.Thickness (22), Child = content };
	}

	private static Control Row (string label, Control control)
	{
		var row = new Grid { ColumnDefinitions = new ColumnDefinitions ("160,*"), Children = { new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center }, control } };
		Grid.SetColumn (control, 1);
		return row;
	}
}
