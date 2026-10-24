using NUnit.Framework;
using Pintora.Avalonia.Input;

namespace Pintora.Avalonia.Tests;

[TestFixture]
internal sealed class PintoraKeyboardSettingsTests
{
	[Test]
	public void DefaultsAreStable ()
	{
		PintoraKeyboardSettings settings = new ();

		Assert.That (settings.GestureTextFor (PintoraKeyCommands.Undo), Is.EqualTo ("Ctrl+Z"));
		Assert.That (settings.GestureTextFor (PintoraKeyCommands.DecreaseBrush), Is.EqualTo ("["));
		Assert.That (settings.GestureTextFor (PintoraKeyCommands.ToggleEraser), Is.EqualTo ("E"));
		Assert.That (settings.GestureTextFor (PintoraKeyCommands.ClearLayer), Is.EqualTo ("Delete"));
		Assert.That (settings.GestureTextFor (PintoraKeyCommands.ActualSize), Is.EqualTo ("1"));
		Assert.That (settings.GestureTextFor (PintoraKeyCommands.Copy), Is.EqualTo ("Ctrl+C"));
		Assert.That (settings.GestureTextFor (PintoraKeyCommands.SelectAll), Is.EqualTo ("Ctrl+A"));
	}

	[Test]
	public void UserBindingReplacesDefault ()
	{
		PintoraKeyboardSettings settings = new ();

		settings.SetGesture (PintoraKeyCommands.Undo, "Ctrl+U");

		Assert.That (settings.GestureTextFor (PintoraKeyCommands.Undo), Is.EqualTo ("Ctrl+U"));
		Assert.That (settings.GestureFor (PintoraKeyCommands.Undo), Is.Not.Null);
	}
}
