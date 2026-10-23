using Avalonia;
using Pinta.Core;
using Pintora.Avalonia.Document;

namespace Pintora.Avalonia.Canvas;

internal sealed class ZoomTool : ICanvasTool
{
	public CanvasHost.CanvasTool Kind => CanvasHost.CanvasTool.Zoom;
	internal readonly record struct ZoomResult (double Scale, Vector Offset);

	public bool PointerPressed (CanvasToolPointerContext context)
	{
		context.Host.ZoomAt (context.ViewPoint, context.IsRightButton ? 1 / 1.25 : 1.25);
		return true;
	}

	public ZoomResult Calculate (Point viewPoint, double factor, double currentScale, Point canvasOrigin,
		Point centeredCanvasOrigin, DocumentViewportState viewport)
	{
		PointD canvasPoint = viewport.ViewPointToCanvas (new PointD (viewPoint.X - canvasOrigin.X, viewPoint.Y - canvasOrigin.Y));
		double nextScale = System.Math.Clamp (currentScale * factor, 0.1, 32);
		var nextOffset = new Vector (
			viewPoint.X - centeredCanvasOrigin.X - canvasPoint.X * nextScale,
			viewPoint.Y - centeredCanvasOrigin.Y - canvasPoint.Y * nextScale);
		return new ZoomResult (nextScale, nextOffset);
	}
}
