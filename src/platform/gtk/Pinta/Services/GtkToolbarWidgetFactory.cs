using Pinta.Core;

namespace Pinta;

/// <summary>GTK adapter for the platform-neutral toolbar widget factory.</summary>
internal sealed class GtkToolbarWidgetFactory : IToolbarWidgetFactory
{
	public IToolBarDropDownButton CreateDropDownButton (bool showLabel = false)
		=> ToolBarDropDownButton.New (showLabel);
}
