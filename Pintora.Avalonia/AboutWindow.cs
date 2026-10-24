using System.Reflection;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Pintora.Avalonia;

public sealed class AboutWindow : Window
{
	public AboutWindow ()
	{
		string version = typeof (AboutWindow).Assembly
			.GetCustomAttribute<AssemblyInformationalVersionAttribute> ()?.InformationalVersion
			?? "0.0.1-alpha";
		Title = "About Pintora";
		Width = 420;
		Height = 260;
		CanResize = false;
		var close = new Button { Content = "Close", HorizontalAlignment = HorizontalAlignment.Right };
		close.Click += (_, _) => Close ();
		Content = new StackPanel {
			Spacing = 10,
			Margin = new global::Avalonia.Thickness (24),
			Children = {
				new TextBlock { Text = "Pintora", FontSize = 28, FontWeight = global::Avalonia.Media.FontWeight.Bold },
				new TextBlock { Text = $"Simple. Powerful. Creative.\nVersion {version}" },
				new TextBlock { Text = "An open-source raster graphics editor built with Avalonia UI." },
				new TextBlock { Text = "Licensed under MIT. Pinta and Paint.NET notices are included in third-party/." },
				close
			}
		};
	}
}
