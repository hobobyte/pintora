namespace Pinta.Core;

/// <summary>GTK implementation of <see cref="IToolbarContainer"/> backed by a horizontal Gtk.Box.</summary>
public sealed class GtkBoxContainer : IToolbarContainer
{
	private readonly Gtk.Box box;

	public GtkBoxContainer (int spacing, int marginStart)
	{
		box = Gtk.Box.New (Gtk.Orientation.Horizontal, spacing);
		box.MarginStart = marginStart;
	}

	public object NativeWidget => box;

	public void Append (object widget) => box.Append ((Gtk.Widget) widget);

	public void RemoveAll () => box.RemoveAll ();
}
