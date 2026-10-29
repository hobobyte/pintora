using Pinta.Core;

namespace Pinta;

/// <summary>GTK adapter for the platform-neutral toolbar widget factory.</summary>
internal sealed class GtkToolbarWidgetFactory : IToolbarWidgetFactory
{
	public IToolBarDropDownButton CreateDropDownButton (bool showLabel = false)
		=> ToolBarDropDownButton.New (showLabel);

	public IDashPatternBox CreateDashPatternBox ()
		=> new GtkDashPatternBox ();

	public IToolBarLabel CreateLabel (string text)
		=> new GtkToolBarLabel (text);

	public IToolBarSeparator CreateSeparator ()
		=> new GtkToolBarSeparator ();

	public IToolBarSpinButton CreateSpinButton (double min, double max, double step, double initialValue)
		=> new GtkToolBarSpinButton (min, max, step, initialValue);

	public IToolBarCheckButton CreateCheckButton (string label)
		=> new GtkToolBarCheckButton (label);

	public IToolBarToggleButton CreateToggleButton (string iconName, string tooltipText)
		=> new GtkToolBarToggleButton (iconName, tooltipText);

	public IToolBarFontButton CreateFontButton (string? initialFont)
		=> new GtkToolBarFontButton (initialFont);
}
