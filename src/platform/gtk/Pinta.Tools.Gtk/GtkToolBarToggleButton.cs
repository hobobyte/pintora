using System;

namespace Pinta.Core;

/// <summary>GTK implementation of <see cref="IToolBarToggleButton"/>.</summary>
public sealed class GtkToolBarToggleButton : IToolBarToggleButton
{
	private readonly Gtk.ToggleButton button;
	private EventHandler? toggled;

	public GtkToolBarToggleButton (string iconName, string tooltipText)
	{
		button = Gtk.ToggleButton.New ();
		button.IconName = iconName;
		button.TooltipText = tooltipText;
		button.CanFocus = false;
		button.OnToggled += (sender, args) => toggled?.Invoke (this, EventArgs.Empty);
	}

	public bool Active {
		get => button.Active;
		set => button.Active = value;
	}

	public event EventHandler? Toggled {
		add => toggled += value;
		remove => toggled -= value;
	}

	public object NativeWidget => button;

	public bool Visible {
		get => button.Visible;
		set => button.Visible = value;
	}
}
