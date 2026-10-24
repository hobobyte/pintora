using NUnit.Framework;
using Pintora.Avalonia.Editor.Commands;
using Pintora.Avalonia.Input;

namespace Pintora.Avalonia.Tests;

[TestFixture]
internal sealed class EditorCommandTests
{
	[Test]
	public void RegistryExecutesRegisteredCommand ()
	{
		var registry = new EditorCommandRegistry ();
		bool executed = false;
		registry.Register (new EditorCommand ("edit.undo", "Undo", () => executed = true));

		Assert.That (registry.TryExecute ("edit.undo"), Is.True);
		Assert.That (executed, Is.True);
	}

	[Test]
	public void DisabledCommandIsNotExecuted ()
	{
		var registry = new EditorCommandRegistry ();
		registry.Register (new EditorCommand ("edit.undo", "Undo", () => Assert.Fail (), () => false));

		Assert.That (registry.TryExecute ("edit.undo"), Is.False);
	}

	[Test]
	public void KeyCommandUsesTheSameStableId ()
	{
		var registry = new EditorCommandRegistry ();
		registry.Register (PintoraKeyCommands.Undo, () => { });

		Assert.That (registry.Contains ("edit.undo"), Is.True);
	}
}
