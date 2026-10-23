using System;
using Pinta.Core;

namespace Pinta.Gui.Widgets;

/// <summary>
/// GTK implementation of the document viewport host contract.
/// </summary>
public sealed class GtkDocumentViewportHost : IDocumentViewportHost
{
	private readonly CanvasWindow canvas_window;
	private readonly Document document;

	public GtkDocumentViewportHost (CanvasWindow canvasWindow, Document document)
	{
		canvas_window = canvasWindow ?? throw new ArgumentNullException (nameof (canvasWindow));
		this.document = document ?? throw new ArgumentNullException (nameof (document));
	}

	private Gtk.Viewport Viewport => (Gtk.Viewport) canvas_window.Canvas.Parent!;

	public bool ImageViewFitsInWindow
		=> canvas_window.Canvas.GetAllocatedWidth () <= Viewport.GetAllocatedWidth ()
			&& canvas_window.Canvas.GetAllocatedHeight () <= Viewport.GetAllocatedHeight ();

	public bool ImageFitsInWindow
		=> Viewport.GetHadjustment ()!.PageSize >= document.ImageSize.Width
			&& Viewport.GetVadjustment ()!.PageSize >= document.ImageSize.Height;

	public PointD ViewCenter => new (
		Viewport.GetHadjustment ()!.Value + Viewport.GetHadjustment ()!.PageSize / 2.0,
		Viewport.GetVadjustment ()!.Value + Viewport.GetVadjustment ()!.PageSize / 2.0);

	public PointD ScrollOffset => new (
		Viewport.GetHadjustment ()!.Value,
		Viewport.GetVadjustment ()!.Value);

	public void GrabFocus ()
	{
		if (!canvas_window.GrabFocus ())
			Console.Error.WriteLine ("Failed to gain focus on the canvas widget!");
	}

	public void Recenter (PointD point, double scale)
	{
		var horizontal = Viewport.GetHadjustment ()!;
		horizontal.Value = Math.Clamp (point.X * scale - horizontal.PageSize / 2, horizontal.Lower, horizontal.Upper);

		var vertical = Viewport.GetVadjustment ()!;
		vertical.Value = Math.Clamp (point.Y * scale - vertical.PageSize / 2, vertical.Lower, vertical.Upper);
	}

	public void Scroll (PointI delta)
	{
		var horizontal = Viewport.GetHadjustment ()!;
		horizontal.Value = Math.Clamp (delta.X + horizontal.Value, horizontal.Lower, horizontal.Upper - horizontal.PageSize);

		var vertical = Viewport.GetVadjustment ()!;
		vertical.Value = Math.Clamp (delta.Y + vertical.Value, vertical.Lower, vertical.Upper - vertical.PageSize);
	}

	public void SetScrollOffset (PointD offset)
	{
		var horizontal = Viewport.GetHadjustment ()!;
		horizontal.Value = Math.Clamp (offset.X, horizontal.Lower, horizontal.Upper - horizontal.PageSize);

		var vertical = Viewport.GetVadjustment ()!;
		vertical.Value = Math.Clamp (offset.Y, vertical.Lower, vertical.Upper - vertical.PageSize);
	}

	public void UpdateContentSize (Size size)
	{
		var horizontal = Viewport.GetHadjustment ()!;
		horizontal.Upper = Math.Max (size.Width, horizontal.PageSize);

		var vertical = Viewport.GetVadjustment ()!;
		vertical.Upper = Math.Max (size.Height, vertical.PageSize);
	}
}
