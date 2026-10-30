using System;

namespace Pinta.Core;

/// <summary>
/// Neutral toolbar font selection button implemented by each platform.
/// The font is described with a Pango font description (same type the text
/// engine consumes), so no platform types leak through this contract.
/// </summary>
public interface IToolBarFontButton
{
	/// <summary>The currently selected font (never null in practice).</summary>
	TextFont Font { get; set; }

	/// <summary>Raised after the selected font has changed.</summary>
	event EventHandler? FontChanged;

	/// <summary>Native platform widget to append to a toolbar surface.</summary>
	object NativeWidget { get; }

	bool Visible { get; set; }
}
