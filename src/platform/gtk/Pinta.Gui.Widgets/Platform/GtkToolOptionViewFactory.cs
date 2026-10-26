using Gtk;
using Pinta.Core;
using Pinta.Tools;

namespace Pinta.Gui.Widgets;

/// <summary>GTK view factory for the neutral tool option contract.</summary>
public sealed class GtkToolOptionViewFactory : IToolOptionViewFactory
{
	public object CreateView (ToolOption option)
		=> ToolOptionWidgetService.GetWidgetForOption (option);
}
