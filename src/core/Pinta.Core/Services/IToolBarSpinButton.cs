using System;

namespace Pinta.Core;

/// <summary>Neutral toolbar spin button widget implemented by each platform.</summary>
public interface IToolBarSpinButton
{
	/// <summary>Current value of the spin button.</summary>
	double Value { get; set; }

	/// <summary>Tooltip text shown when hovering the widget.</summary>
	string TooltipText { get; set; }

	/// <summary>Raised after the value has changed (by the user or programmatically).</summary>
	event EventHandler? ValueChanged;

	/// <summary>Native platform widget to append to a toolbar surface.</summary>
	object NativeWidget { get; }

	bool Visible { get; set; }
}
