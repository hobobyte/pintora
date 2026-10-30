using Pinta.Core;

namespace Pinta;

/// <summary>Gio adapter for the neutral <see cref="IActionMenu"/> model (namespace kept as Pinta.Core).</summary>
public sealed class GioActionMenu : IActionMenu
{
	public Gio.Menu Menu { get; }

	public GioActionMenu ()
		: this (Gio.Menu.New ())
	{ }

	public GioActionMenu (Gio.Menu menu)
	{
		Menu = menu;
	}

	public IActionMenu CreateSubmenu ()
		=> new GioActionMenu ();

	public void AppendItem (Command command)
		=> Menu.AppendItem (Gio.MenuItem.New (command.Label, command.FullName));

	public void AppendItem (CommandMenuItem item)
		=> Menu.AppendItem (Gio.MenuItem.New (item.Label, item.ActionId));

	public void Append (string label, string detailedAction)
		=> Menu.Append (label, detailedAction);

	public void AppendSubmenu (string label, IActionMenu submenu)
		=> Menu.AppendSubmenu (label, ((GioActionMenu) submenu).Menu);

	public void AppendSection (string? label, IActionMenu section)
		=> Menu.AppendSection (label, ((GioActionMenu) section).Menu);

	public void Remove (Command command)
	{
		for (int i = 0; i < Menu.GetNItems (); ++i) {
			string name_attr = Menu.GetItemAttributeValue (i, "action", GLib.VariantType.String)!.GetString (out nuint _);
			if (name_attr != command.FullName) continue;
			Menu.Remove (i);
			return;
		}
	}

	public void RemoveAll ()
		=> Menu.RemoveAll ();
}
