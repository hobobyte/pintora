using System;
using Pinta.Core;

namespace Pinta.Tools;

// That handle is used as second brush to show position of stamp tool origin
public class BrushHandle : IToolHandle
{
	private int brush_width;
	public int BrushWidth {
		get { return brush_width; }
		set {
			if (value > 0)
				brush_width = value;
		}
	}
	public bool Active { get; set; }
	public PointD CanvasPosition { get; set; }
	private readonly IWorkspaceService workspace;

	public BrushHandle (IWorkspaceService workspace)
	{
		this.workspace = workspace;
	}

	public bool ContainsPoint (PointD windowPoint)
		=> ComputeWindowRect ().ContainsPoint (windowPoint);

	/// <summary>
	/// Drawing shape like in GdkExtensions.CreateIconWithShape
	/// </summary>
	public void Draw (IToolHandleRenderer renderer)
	{
		double zoom =
		    (PintaCore.Workspace.HasOpenDocuments)
		    ? Math.Min (30d, workspace.GetScale ())
		    : 1d;
		int clampedWidth = (int) Math.Min (800d, brush_width * zoom);

		if (clampedWidth < 3)
			return;

		int halfOfShapeWidth = clampedWidth / 2;

		var center = new PointD (CanvasPosition.X * zoom, CanvasPosition.Y * zoom);

		ColorD outerColor = new (1.0, 1.0, 1.0, 0.75);
		ColorD innerColor = new (0, 0, 0, 1.0);

		renderer.StrokeCircle (center, halfOfShapeWidth, 2, outerColor);
		renderer.StrokeCircle (center, halfOfShapeWidth, 1, innerColor);
	}

	public RectangleI InvalidateRect => ComputeWindowRect ().Inflated (2, 2).ToInt ();

	/// <summary>
	/// Bounding rectangle of the handle (in window space). Similar to MoveHandle.
	/// </summary>

	private RectangleD ComputeWindowRect ()
	{
		double diameter = brush_width;
		double radius = diameter / 2.0;

		PointD windowPt = workspace.CanvasPointToView (CanvasPosition);
		return new RectangleD (windowPt.X - radius, windowPt.Y - radius, diameter, diameter);
	}
}
