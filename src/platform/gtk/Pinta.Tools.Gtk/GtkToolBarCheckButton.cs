using System;

namespace Pinta.Core;

/// <summary>GTK implementation of <see cref="IToolBarCheckButton"/>.</summary>
public sealed class GtkToolBarCheckButton : IToolBarCheckButton
{
	private readonly Gtk.CheckButton checkButton;
	private EventHandler? toggled;

	public GtkToolBarCheckButton (string label)
	{
		checkButton = Gtk.CheckButton.NewWithLabel (label);
		checkButton.FocusOnClick = false;
		checkButton.OnToggled += (sender, args) => toggled?.Invoke (this, EventArgs.Empty);
	}

	public bool Active {
		get => checkButton.Active;
		set => checkButton.Active = value;
	}

	public string Label => checkButton.Label ?? string.Empty;

	public event EventHandler? Toggled {
		add => toggled += value;
		remove => toggled -= value;
	}

	public object NativeWidget => checkButton;

	public bool Visible {
		get => checkButton.Visible;
		set => checkButton.Visible = value;
	}
}
