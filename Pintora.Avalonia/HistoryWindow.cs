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
		var clear = new Button { Content = "Clear history", HorizontalAlignment = HorizontalAlignment.Left };
		clear.Click += (_, _) => canvas.ClearHistory ();
		void RefreshHistory () {
			Refresh (canvas);
			clear.IsEnabled = canvas.History.Count > 0;
		}
		canvas.HistoryChanged += (_, _) => RefreshHistory ();
		Content = new StackPanel {
			Spacing = 8,
			Margin = new global::Avalonia.Thickness (12),
			Children = {
				new TextBlock { Text = "Actions", FontSize = 17 }, entries, refresh, clear
		}
		};
		RefreshHistory ();
	}

	private void Refresh (CanvasHost canvas)
	{
		entries.ItemsSource = null;
		entries.ItemsSource = canvas.History;
	}
}
