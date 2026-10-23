using Avalonia.Controls;
using Avalonia.Layout;
using Pintora.Avalonia.Canvas;

namespace Pintora.Avalonia;

public sealed class HistoryWindow : Window
{
	private readonly ListBox entries = new ();

	public HistoryWindow (CanvasHost canvas)
	{
		Title = "History";
		Width = 320;
		Height = 420;
		var refresh = new Button { Content = "Refresh", HorizontalAlignment = HorizontalAlignment.Left };
		refresh.Click += (_, _) => Refresh (canvas);
		canvas.HistoryChanged += (_, _) => Refresh (canvas);
		Content = new StackPanel { Spacing = 8, Margin = new global::Avalonia.Thickness (12), Children = {
			new TextBlock { Text = "Actions", FontSize = 17 }, entries, refresh
		} };
		Refresh (canvas);
	}

	private void Refresh (CanvasHost canvas)
	{
		entries.ItemsSource = null;
		entries.ItemsSource = canvas.History;
	}
}
