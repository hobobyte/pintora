using System;
using System.Collections.ObjectModel;

namespace Pinta.Core;

/// <summary>Neutral tool-option dropdown contract implemented by each platform.</summary>
public interface IToolBarDropDownButton
{
	ReadOnlyCollection<ToolBarItem> Items { get; }
	ToolBarItem SelectedItem { get; set; }
	int SelectedIndex { get; set; }
	ToolBarItem AddItem (string text, string imageId);
	ToolBarItem AddItem (string text, string imageId, object? tag);
	event EventHandler? SelectedItemChanged;
	object NativeWidget { get; }
	bool Visible { get; set; }
}
