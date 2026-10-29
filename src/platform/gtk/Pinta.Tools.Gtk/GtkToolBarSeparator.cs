namespace Pinta.Core;

/// <summary>GTK implementation of <see cref="IToolBarSeparator"/>.</summary>
public sealed class GtkToolBarSeparator : IToolBarSeparator
{
	private readonly Gtk.Separator separator;

	public GtkToolBarSeparator ()
	{
		separator = Gtk.Separator.New (Gtk.Orientation.Horizontal);
		separator.AddCssClass (AdwaitaStyles.Spacer);
	}

	public object NativeWidget => separator;

	public bool Visible {
		get => separator.Visible;
		set => separator.Visible = value;
	}
}
