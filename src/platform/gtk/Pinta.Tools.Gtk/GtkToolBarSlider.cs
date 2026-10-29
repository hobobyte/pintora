namespace Pinta.Core;

/// <summary>GTK implementation of <see cref="IToolBarSlider"/>, modeled on
/// GtkExtensions.CreateToolBarSlider.</summary>
public sealed class GtkToolBarSlider : IToolBarSlider
{
	private readonly Gtk.Scale slider;

	public GtkToolBarSlider (double min, double max, double step, double initialValue)
	{
		slider = Gtk.Scale.NewWithRange (Gtk.Orientation.Horizontal, min, max, step);
		slider.WidthRequest = 150;
		slider.DrawValue = true;
		slider.ValuePos = Gtk.PositionType.Left;
		slider.SetValue (initialValue);
	}

	public double Value => slider.GetValue ();

	public object NativeWidget => slider;
}
