using System;

namespace Pinta.Core;

/// <summary>GTK implementation of <see cref="IToolBarFontButton"/>.</summary>
public sealed class GtkToolBarFontButton : IToolBarFontButton
{
	private readonly Gtk.FontDialogButton button;
	private EventHandler? fontChanged;

	public GtkToolBarFontButton (string? initialFont)
	{
		Gtk.FontDialog fontDialog = Gtk.FontDialog.New ();
		fontDialog.Modal = true;

		button = Gtk.FontDialogButton.New (fontDialog);
		button.UseSize = false;
		button.UseFont = true;
		button.CanFocus = false;
		button.Level = Gtk.FontLevel.Family;

		string fontName = !string.IsNullOrEmpty (initialFont)
			? initialFont
			: Gtk.Settings.GetDefault ()!.GtkFontName!;
		button.FontDesc = Pango.FontDescription.FromString (fontName);

		Gtk.FontDialogButton.FontDescPropertyDefinition.Notify (button, (_, _) => fontChanged?.Invoke (this, EventArgs.Empty));
	}

	public Pango.FontDescription Font {
		get => button.FontDesc!;
		set => button.FontDesc = value;
	}

	public event EventHandler? FontChanged {
		add => fontChanged += value;
		remove => fontChanged -= value;
	}

	public object NativeWidget => button;

	public bool Visible {
		get => button.Visible;
		set => button.Visible = value;
	}
}
