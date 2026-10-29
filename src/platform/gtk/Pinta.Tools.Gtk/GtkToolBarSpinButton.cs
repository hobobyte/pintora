using System;

namespace Pinta.Core;

/// <summary>GTK implementation of <see cref="IToolBarSpinButton"/>, modeled on
/// GtkExtensions.CreateToolBarSpinButton (no focus on click, focus returns to the canvas
/// after the value is changed).</summary>
public sealed class GtkToolBarSpinButton : IToolBarSpinButton
{
	private readonly Gtk.SpinButton spinButton;
	private EventHandler? valueChanged;

	public GtkToolBarSpinButton (double min, double max, double step, double initialValue)
	{
		spinButton = Gtk.SpinButton.NewWithRange (min, max, step);
		spinButton.FocusOnClick = false;
		spinButton.Value = initialValue;

		// After a spin button is edited, return focus to the canvas so that
		// tools can handle subsequent key events.
		spinButton.OnValueChanged += (o, e) => {
			if (!PintaCore.Workspace.HasOpenDocuments) return;
			PintaCore.Workspace.ActiveWorkspace.GrabFocusToCanvas ();
		};
		spinButton.OnValueChanged += (sender, args) => valueChanged?.Invoke (this, EventArgs.Empty);
	}

	public double Value {
		get => spinButton.Value;
		set => spinButton.Value = value;
	}

	public string TooltipText {
		get => spinButton.TooltipText ?? string.Empty;
		set => spinButton.TooltipText = value;
	}

	public event EventHandler? ValueChanged {
		add => valueChanged += value;
		remove => valueChanged -= value;
	}

	public object NativeWidget => spinButton;

	public bool Visible {
		get => spinButton.Visible;
		set => spinButton.Visible = value;
	}
}
