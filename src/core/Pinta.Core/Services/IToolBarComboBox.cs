using System;
using System.Collections.Generic;

namespace Pinta.Core;

/// <summary>Neutral toolbar combo box (editable or list-only) implemented by each platform.</summary>
public interface IToolBarComboBox
{
	/// <summary>Text of the active item, or the entry text for editable combos.</summary>
	string? ActiveText { get; }

	/// <summary>Index of the active item (-1 for none).</summary>
	int Active { get; set; }

	/// <summary>Number of items in the list.</summary>
	int ItemCount { get; }

	bool Sensitive { get; set; }

	/// <summary>Raised when the active item or (for editable combos) the entry text changes.</summary>
	event EventHandler? Changed;

	/// <summary>Raised when the text entry gains focus (editable combos only).</summary>
	event EventHandler? FocusEntered;

	/// <summary>Raised when the text entry loses focus (editable combos only).</summary>
	event EventHandler? FocusLeft;

	/// <summary>Appends an item to the list.</summary>
	void AppendItem (string text);

	/// <summary>Removes all items from the list.</summary>
	void RemoveAllItems ();

	/// <summary>Sets the text of the entry (editable combos only).</summary>
	void SetEntryText (string text);

	/// <summary>Gets the text of the entry (editable combos only).</summary>
	string GetEntryText ();

	/// <summary>Native platform widget to append to a toolbar surface.</summary>
	object NativeWidget { get; }
}
