using System.Collections.Generic;
using System.Linq;
using Pinta.Core;

namespace Pinta.Tools;

/// <summary>
/// A handle that the user can click and move, e.g. for resizing a selection.
/// </summary>
public sealed class MoveHandle : IToolHandle
{
	private static readonly ColorD fill_color = new (0, 0, 1, 1);
	private static readonly ColorD selection_fill_color = new (1, 0.5, 0, 1);
	private static readonly ColorD stroke_color = new (1, 1, 1, 0.7);

	private const double RADIUS = 4.5;

	private readonly IWorkspaceService workspace;

	public MoveHandle (IWorkspaceService workspace)
	{
		this.workspace = workspace;
	}

	public PointD CanvasPosition { get; set; }

	/// <summary>
	/// Inactive handles are not drawn.
	/// </summary>
	public bool Active { get; set; } = false;

	/// <summary>
	/// A handle that is selected by the user for interaction is drawn in a different color.
	/// </summary>
	public bool Selected { get; set; } = false;

	/// <summary>
	/// Name of the cursor to display when hovering over this handle
	/// (a standard CSS cursor name, see Pinta.Resources.StandardCursors).
	/// The platform canvas converts the name to a native cursor.
	/// </summary>
	public string Cursor { get; init; } = Pinta.Resources.StandardCursors.Default;

	/// <summary>
	/// Tests whether the window point is inside the handle's area.
	/// The area to grab a handle is a bit larger than the rendered area for easier selection.
	/// </summary>
	public bool ContainsPoint (PointD window_point)
	{
		const int TOLERANCE = 5;

		RectangleD bounds = ComputeWindowRect ().Inflated (TOLERANCE, TOLERANCE);
		return bounds.ContainsPoint (window_point);
	}

	/// <summary>
	/// Draw the handle, at a constant window space size (i.e. not depending on the image zoom or resolution)
	/// </summary>
	public void Draw (IToolHandleRenderer renderer)
	{
		PointD windowPt = workspace.CanvasPointToView (CanvasPosition);

		ColorD fillColor = Selected ? selection_fill_color : fill_color;
		renderer.FillCircle (windowPt, RADIUS, fillColor);

		renderer.StrokeCircle (windowPt, RADIUS, 1.0, stroke_color);
	}

	/// <summary>
	/// Bounding rectangle to use with InvalidateWindowRect() when triggering a redraw.
	/// </summary>
	public RectangleI InvalidateRect => ComputeWindowRect ().Inflated (2, 2).ToInt ();

	/// <summary>
	/// Bounding rectangle of the handle (in window space).
	/// </summary>
	private RectangleD ComputeWindowRect ()
	{
		const double DIAMETER = 2 * RADIUS;

		PointD windowPt = workspace.CanvasPointToView (CanvasPosition);
		return new RectangleD (windowPt.X - RADIUS, windowPt.Y - RADIUS, DIAMETER, DIAMETER);
	}

	/// <summary>
	/// Returns the union of the invalidate rectangles for a collection of handles.
	/// </summary>
	public static RectangleI UnionInvalidateRects (IEnumerable<MoveHandle> handles) =>
		handles
		.Select (c => c.InvalidateRect)
		.DefaultIfEmpty (RectangleI.Zero)
		.Aggregate ((accum, r) => accum.Union (r));
}
