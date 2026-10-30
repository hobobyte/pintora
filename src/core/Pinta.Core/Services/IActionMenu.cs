using System;

namespace Pinta.Core;

/// <summary>
/// Platform-neutral menu model that action classes build at registration time.
/// The GTK platform adapts it to <c>Gio.Menu</c>; other platforms can render it natively.
/// </summary>
public interface IActionMenu
{
	/// <summary>Creates a new empty submenu/section that can be appended to this or other menus.</summary>
	IActionMenu CreateSubmenu ();

	/// <summary>Appends an item bound to a command's action id.</summary>
	void AppendItem (Command command);

	/// <summary>Appends an item with a label and an action id (e.g. "app.Save").</summary>
	void AppendItem (CommandMenuItem item);

	/// <summary>Appends an item with a label and a detailed action string (e.g. "win.close").</summary>
	void Append (string label, string detailedAction);

	/// <summary>Appends a submenu.</summary>
	void AppendSubmenu (string label, IActionMenu submenu);

	/// <summary>Appends an inline section.</summary>
	void AppendSection (string? label, IActionMenu section);

	/// <summary>Removes the item bound to the given command, if present.</summary>
	void Remove (Command command);

	/// <summary>Removes all items.</summary>
	void RemoveAll ();
}
