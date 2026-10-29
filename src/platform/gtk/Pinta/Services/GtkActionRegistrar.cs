using System;
using Pinta.Core;

namespace Pinta;

/// <summary>GTK implementation of the neutral action registrar, mapping commands to Gtk.Application.</summary>
internal sealed class GtkActionRegistrar : IActionRegistrar
{
	private readonly Gtk.Application app;

	public GtkActionRegistrar (Gtk.Application application)
	{
		app = application;
	}

	public void AddCommand (Command command)
	{
		app.AddAction (command.Action);
		SetAccelerators (command.FullName, [.. command.Shortcuts]);
	}

	public void AddCommands (ReadOnlySpan<Command> commands)
	{
		foreach (var command in commands)
			AddCommand (command);
	}

	public void AddNativeAction (object action)
		=> app.AddAction ((Gio.SimpleAction) action);

	public void SetAccelerators (string actionName, string[] accelerators)
	{
		for (int i = 0; i < accelerators.Length; i++)
			accelerators[i] = ConvertPrimaryKey (accelerators[i]);

		app.SetAccelsForAction (actionName, accelerators);
	}

	private static string ConvertPrimaryKey (string accel)
	{
		// Convert "<Primary>" to Ctrl (or Command on macOS).
		// This was done automatically in GTK3, but does not happen in GTK4.
		return accel.Replace ("<Primary>", PintaCore.System.OperatingSystem == OS.Mac ? "<Meta>" : "<Control>");
	}
}
