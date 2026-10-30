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
<<<<<<< HEAD
		var action = Gio.SimpleAction.New (command.Name, null);
		action.OnActivate += (_, _) => command.Activate ();
		action.Enabled = command.Sensitive;
		command.SensitivityChanged += (_, _) => action.Enabled = command.Sensitive;
		command.NativeAction = action;
		app.AddAction (action);
=======
		app.AddAction (command.Action);
>>>>>>> origin/master
		SetAccelerators (command.FullName, [.. command.Shortcuts]);
	}

	public void AddCommands (ReadOnlySpan<Command> commands)
	{
		foreach (var command in commands)
			AddCommand (command);
	}

<<<<<<< HEAD
	public void AddIntStateCommand (IntStateCommand command)
	{
		var action = Gio.SimpleAction.NewStateful (
			command.Name,
			GLib.VariantType.New ("i"),
			GLib.Variant.NewInt32 (command.Value));

		action.OnActivate += (_, args) => {
			int newValue = args.Parameter!.GetInt32 ();
			action.ChangeState (args.Parameter!);
			command.Activate (newValue);
		};

		command.Changed += v => action.ChangeState (GLib.Variant.NewInt32 (v));

		command.NativeAction = action;
		app.AddAction (action);
	}

=======
>>>>>>> origin/master
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
