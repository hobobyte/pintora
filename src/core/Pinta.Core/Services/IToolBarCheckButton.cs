using System;

namespace Pinta.Core;

/// <summary>Neutral toolbar check button widget implemented by each platform.</summary>
public interface IToolBarCheckButton
{
	/// <summary>Whether the check button is active (checked).</summary>
	bool Active { get; set; }

	/// <summary>Label text shown next to the check box.</summary>
	string Label { get; }

	/// <summary>Raised after the active state has been toggled.</summary>
	event EventHandler? Toggled;

	/// <summary>Native platform widget to append to a toolbar surface.</summary>
	object NativeWidget { get; }

	bool Visible { get; set; }
}
