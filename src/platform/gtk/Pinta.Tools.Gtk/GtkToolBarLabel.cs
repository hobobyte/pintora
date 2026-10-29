namespace Pinta.Core;

/// <summary>GTK implementation of <see cref="IToolBarLabel"/>.</summary>
public sealed class GtkToolBarLabel : IToolBarLabel
{
	private readonly Gtk.Label label;

	public GtkToolBarLabel (string text)
	{
		label = Gtk.Label.New (text);
	}

	public string Text => label.GetText ();

	public object NativeWidget => label;

	public bool Visible {
		get => label.Visible;
		set => label.Visible = value;
	}
}
