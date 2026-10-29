//
// GtkToolHandleRenderer.cs
//
// GTK implementation of the renderer-neutral Pinta.Core.IToolHandleRenderer,
// used by the GTK canvas to draw IToolHandle elements.
//

using Pinta.Core;

namespace Pinta.Gui.Widgets;

/// <summary>
/// GTK implementation of <see cref="IToolHandleRenderer"/> that draws onto a
/// <see cref="Gtk.Snapshot"/> using Gsk paths, preserving the original handle
/// rendering behavior (circle fill + stroke).
/// </summary>
internal sealed class GtkToolHandleRenderer : IToolHandleRenderer
{
	private readonly Gtk.Snapshot snapshot;

	public GtkToolHandleRenderer (Gtk.Snapshot snapshot)
	{
		this.snapshot = snapshot;
	}

	public void FillCircle (PointD center, double radius, ColorD color)
	{
		Gsk.Path path = BuildCirclePath (center, radius);
		snapshot.AppendFill (path, Gsk.FillRule.EvenOdd, ToRgba (color));
	}

	public void StrokeCircle (PointD center, double radius, double lineWidth, ColorD color)
	{
		Gsk.Path path = BuildCirclePath (center, radius);
		Gsk.Stroke stroke = Gsk.Stroke.New ((float) lineWidth);
		snapshot.AppendStroke (path, stroke, ToRgba (color));
	}

	private static Gsk.Path BuildCirclePath (PointD center, double radius)
	{
		Gsk.PathBuilder pathBuilder = Gsk.PathBuilder.New ();
		Graphene.Point centerPt = new ();
		centerPt.Init ((float) center.X, (float) center.Y);
		pathBuilder.AddCircle (centerPt, (float) radius);
		return pathBuilder.ToPath ();
	}

	private static Gdk.RGBA ToRgba (ColorD color)
		=> new () {
			Red = (float) color.R,
			Green = (float) color.G,
			Blue = (float) color.B,
			Alpha = (float) color.A
		};
}
