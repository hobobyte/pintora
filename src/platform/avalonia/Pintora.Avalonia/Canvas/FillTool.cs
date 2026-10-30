using System;
using Pinta.Core;

namespace Pintora.Avalonia.Canvas;

internal sealed class FillTool : ICanvasTool
{
	public CanvasHost.CanvasTool Kind => CanvasHost.CanvasTool.Fill;
	public bool PointerPressed (CanvasToolPointerContext context)
		=> context.Host.FillAt (context.Point, context.Color);

	public bool Fill (IImageSurface? surface, PointD point, ColorBgra color, int tolerance,
		Action beginStroke, Action commitStroke)
	{
		if (!RasterAlgorithms.TrySampleNearest (surface, point, out ColorBgra target) || target == color)
			return false;
		beginStroke ();
		RasterAlgorithms.FloodFill (surface, point, color, tolerance);
		commitStroke ();
		return true;
	}
}
