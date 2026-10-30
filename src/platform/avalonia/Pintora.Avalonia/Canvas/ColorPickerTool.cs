using Pinta.Core;

namespace Pintora.Avalonia.Canvas;

internal sealed class ColorPickerTool : ICanvasTool
{
	public CanvasHost.CanvasTool Kind => CanvasHost.CanvasTool.ColorPicker;
	public bool PointerPressed (CanvasToolPointerContext context)
	{
		context.Host.PickColorAt (context.Point);
		return true;
	}
	public bool TryPick (IImageSurface? surface, PointD point, out ColorBgra color)
	{
		return RasterAlgorithms.TrySampleNearest (surface, point, out color);
	}
}
