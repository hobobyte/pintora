using System;

namespace Pinta.Core;

/// <summary>
/// Platform-neutral stateful command holding an integer value (e.g. the ruler metric).
/// The platform-native action object is attached by the <see cref="IActionRegistrar"/>.
/// </summary>
public sealed class IntStateCommand
{
	/// <summary>The platform-native action object (e.g. Gio.SimpleAction), set by the registrar.</summary>
	public object? NativeAction { get; internal set; }

	public string Name { get; }

	private int value;
	public int Value {
		get => value;
		set {
			if (value == this.value) return;
			this.value = value;
			Changed?.Invoke (value);
		}
	}

	/// <summary>Raised when the value changes from any source (user activation or programmatic set).</summary>
	public event Action<int>? Changed;

	/// <summary>Raised only on user activation (platform action triggered with a parameter).</summary>
	public event Action<int>? Activated;

	public IntStateCommand (string name, int initialValue)
	{
		Name = name;
		value = initialValue;
	}

	/// <summary>Handles a user activation carrying the new value as its parameter.</summary>
	public void Activate (int newValue)
	{
		Value = newValue;
		Activated?.Invoke (newValue);
	}
}
