using System;
using System.Collections.Generic;
using Pintora.Avalonia.Input;

namespace Pintora.Avalonia.Editor.Commands;

public sealed class EditorCommandRegistry
{
	private readonly Dictionary<string, EditorCommand> commands = new (StringComparer.OrdinalIgnoreCase);

	public IReadOnlyCollection<EditorCommand> Commands => commands.Values;

	public void Register (EditorCommand command)
	{
		if (string.IsNullOrWhiteSpace (command.Id))
			throw new ArgumentException ("Command id cannot be empty.", nameof (command));
		commands[command.Id] = command;
	}

	public bool TryExecute (string id)
	{
		if (!commands.TryGetValue (id, out EditorCommand? command) || !command.IsEnabled)
			return false;
		command.Execute ();
		return true;
	}

	public bool Contains (string id) => commands.ContainsKey (id);

	public void Register (PintoraKeyCommand keyCommand, Action execute, Func<bool>? canExecute = null)
		=> Register (new EditorCommand (keyCommand.Id, keyCommand.Id, execute, canExecute));
}
