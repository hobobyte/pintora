namespace Pinta.Core;

/// <summary>
/// Platform-neutral factory boundary for tool option widgets.
/// Implementations may return a native platform widget as an opaque object;
/// Core and tools must not construct the platform widget directly.
/// </summary>
public interface IToolbarWidgetFactory
{
	IToolBarDropDownButton CreateDropDownButton (bool showLabel = false);
	IDashPatternBox CreateDashPatternBox ();
	IToolBarLabel CreateLabel (string text);
	IToolBarSeparator CreateSeparator ();
	IToolBarSpinButton CreateSpinButton (double min, double max, double step, double initialValue);
	IToolBarCheckButton CreateCheckButton (string label);
	IToolBarToggleButton CreateToggleButton (string iconName, string tooltipText);

	/// <summary>
	/// Create a font selection button. When initialFont is null or empty,
	/// the platform default font is used.
	/// </summary>
	IToolBarFontButton CreateFontButton (string? initialFont);

	/// <summary>Create a horizontal slider with the given range and initial value.</summary>
	IToolBarSlider CreateSlider (double min, double max, double step, double initialValue);

	/// <summary>
	/// Create a horizontal container widget (with spacing and left margin)
	/// that can hold native option views. Append/RemoveAll via <see cref="IToolbarContainer"/>.
	/// </summary>
	IToolbarContainer CreateBox (int spacing, int marginStart);
}
