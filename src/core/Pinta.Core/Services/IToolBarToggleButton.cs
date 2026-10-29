using System;

namespace Pinta.Core;

/// <summary>Neutral toolbar toggle button widget implemented by each platform.</summary>
public interface IToolBarToggleButton
{
	/// <summary>Whether the button is active (toggled on).</summary>
	bool Active { get; set; }

	/// <summary>Raised after the active state has been toggled.</summary>
	event EventHandler? Toggled;

	/// <summary>Native platform widget to append to a toolbar surface.</summary>
	object NativeWidget { get; }

	bool Visible { get; set; }
}
