using System;

namespace Pinta.Core;

/// <summary>
/// Platform registrar for command actions. Core action classes build <see cref="Command"/>s
/// and register them through this interface; the platform maps them to its native
/// action system with accelerators.
/// </summary>
public interface IActionRegistrar
{
	/// <summary>Register a command action (name, handler and accelerators).</summary>
	void AddCommand (Command command);

	/// <summary>Register several command actions.</summary>
	void AddCommands (ReadOnlySpan<Command> commands);

	/// <summary>Register a raw native action object (platform escape hatch for non-Command actions).</summary>
	void AddNativeAction (object action);

	/// <summary>Set the accelerators for an already-registered action.</summary>
	void SetAccelerators (string actionName, string[] accelerators);
<<<<<<< HEAD

	/// <summary>Register a stateful integer command (e.g. ruler metric).</summary>
	void AddIntStateCommand (IntStateCommand command);
=======
>>>>>>> origin/master
}
