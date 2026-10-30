using NUnit.Framework;
using Pinta.Core;

namespace Pinta.Core.Tests;

[TestFixture]
internal sealed class CommandTests
{
	[Test]
	public void SensitiveDefaultsToTrue ()
	{
		var command = new Command ("Test", "Test", null, null);
		Assert.That (command.Sensitive, Is.True);
	}

	[Test]
	public void SensitiveChangeRaisesEvent ()
	{
		var command = new Command ("Test", "Test", null, null);
		bool raised = false;
		command.SensitivityChanged += (_, _) => raised = true;

		command.Sensitive = false;

		Assert.Multiple (() => {
			Assert.That (raised, Is.True);
			Assert.That (command.Sensitive, Is.False);
		});
	}

	[Test]
	public void SameSensitiveValueDoesNotRaiseEvent ()
	{
		var command = new Command ("Test", "Test", null, null);
		int count = 0;
		command.SensitivityChanged += (_, _) => count++;

		command.Sensitive = true; // already the default

		Assert.That (count, Is.EqualTo (0));
	}

	[Test]
	public void ActivateRaisesActivated ()
	{
		var command = new Command ("Test", "Test", null, null);
		object? sender = null;
		command.Activated += (o, _) => sender = o;

		command.Activate ();

		Assert.That (sender, Is.SameAs (command));
	}

	[Test]
	public void ToggleCommandTogglesOnActivate ()
	{
		var toggle = new ToggleCommand ("Test", "Test", null, null);
		bool? toggledValue = null;
		bool? interactive = null;
		toggle.Toggled += (v, i) => { toggledValue = v; interactive = i; };

		toggle.Activate ();

		Assert.Multiple (() => {
			Assert.That (toggle.Value, Is.True);
			Assert.That (toggledValue, Is.True);
			Assert.That (interactive, Is.True);
		});
	}

	[Test]
	public void ToggleCommandProgrammaticSetIsNotInteractive ()
	{
		var toggle = new ToggleCommand ("Test", "Test", null, null);
		bool? interactive = null;
		toggle.Toggled += (_, i) => interactive = i;

		toggle.Value = true;

		Assert.Multiple (() => {
			Assert.That (toggle.Value, Is.True);
			Assert.That (interactive, Is.False);
		});
	}
}

[TestFixture]
internal sealed class IntStateCommandTests
{
	[Test]
	public void ValueChangeRaisesChanged ()
	{
		var command = new IntStateCommand ("Test", 0);
		int? changedValue = null;
		command.Changed += v => changedValue = v;

		command.Value = 3;

		Assert.Multiple (() => {
			Assert.That (command.Value, Is.EqualTo (3));
			Assert.That (changedValue, Is.EqualTo (3));
		});
	}

	[Test]
	public void ActivateSetsValueAndRaisesActivated ()
	{
		var command = new IntStateCommand ("Test", 0);
		int? changedValue = null;
		int? activatedValue = null;
		command.Changed += v => changedValue = v;
		command.Activated += v => activatedValue = v;

		command.Activate (2);

		Assert.Multiple (() => {
			Assert.That (command.Value, Is.EqualTo (2));
			Assert.That (changedValue, Is.EqualTo (2));
			Assert.That (activatedValue, Is.EqualTo (2));
		});
	}

	[Test]
	public void SameValueDoesNotRaiseChanged ()
	{
		var command = new IntStateCommand ("Test", 5);
		int count = 0;
		command.Changed += _ => count++;

		command.Value = 5;

		Assert.That (count, Is.EqualTo (0));
	}
}
