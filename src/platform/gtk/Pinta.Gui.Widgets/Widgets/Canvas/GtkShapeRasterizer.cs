using Cairo;
using Pinta.Core;

namespace Pinta.Gui.Widgets;

/// <summary>
/// GTK shape adapter. Geometry stays in Core; this class owns only Cairo path creation
/// and painting for the GTK surface.
/// </summary>
public static class GtkShapeRasterizer
{
	public static RectangleD DrawEllipse (Context context, PointD from, PointD to, Color color, int lineWidth)
		=> context.DrawEllipse (ShapeAlgorithms.Bounds (from, to), color, lineWidth);
	public static RectangleD DrawEllipse (Context context, RectangleD bounds, Color color, int lineWidth)
		=> context.DrawEllipse (bounds, color, lineWidth);

	public static RectangleD FillEllipse (Context context, PointD from, PointD to, Color color)
		=> context.FillEllipse (ShapeAlgorithms.Bounds (from, to), color);
	public static RectangleD FillEllipse (Context context, RectangleD bounds, Color color)
		=> context.FillEllipse (bounds, color);

	public static RectangleD FillRoundedRectangle (Context context, PointD from, PointD to, double radius, Color color)
		=> context.FillRoundedRectangle (ShapeAlgorithms.Bounds (from, to), radius, color);
	public static RectangleD FillRoundedRectangle (Context context, RectangleD bounds, double radius, Color color)
		=> context.FillRoundedRectangle (bounds, radius, color);
}
