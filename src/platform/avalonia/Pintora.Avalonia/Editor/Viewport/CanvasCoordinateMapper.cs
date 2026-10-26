using Avalonia;
using Pinta.Core;

namespace Pintora.Avalonia.Editor.Viewport;

public readonly record struct CanvasCoordinateMapper (Point Origin, double Scale)
{
	public PointD ViewToCanvas (Point viewPoint)
	{
		double scale = Scale > 0 ? Scale : 1;
		return new PointD ((viewPoint.X - Origin.X) / scale, (viewPoint.Y - Origin.Y) / scale);
	}

	public Point CanvasToView (PointD canvasPoint)
		=> new (Origin.X + canvasPoint.X * Scale, Origin.Y + canvasPoint.Y * Scale);
}
