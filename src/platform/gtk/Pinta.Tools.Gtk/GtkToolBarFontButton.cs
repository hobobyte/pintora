using System;
<<<<<<< HEAD
using Pinta.Core;
=======
>>>>>>> origin/master

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

<<<<<<< HEAD
	public TextFont Font {
		get => ToTextFont (button.FontDesc!);
		set => button.FontDesc = ToFontDescription (value);
=======
	public Pango.FontDescription Font {
		get => button.FontDesc!;
		set => button.FontDesc = value;
>>>>>>> origin/master
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
<<<<<<< HEAD

	public static TextFont ToTextFont (Pango.FontDescription desc)
	{
		return new TextFont (
			desc.GetFamily () ?? string.Empty,
			desc.GetSize (),
			(TextFontVariant) desc.GetVariant (),
			(TextFontWeight) desc.GetWeight (),
			(TextFontStyle) desc.GetStyle ());
	}

	public static Pango.FontDescription ToFontDescription (TextFont font)
	{
		var desc = Pango.FontDescription.New ();
		if (!string.IsNullOrEmpty (font.Family))
			desc.SetFamily (font.Family);
		if (font.SizePangoUnits > 0)
			desc.SetSize (font.SizePangoUnits);
		desc.SetVariant ((Pango.Variant) font.Variant);
		desc.SetWeight ((Pango.Weight) font.Weight);
		desc.SetStyle ((Pango.Style) font.Style);
		return desc;
	}
=======
>>>>>>> origin/master
}
