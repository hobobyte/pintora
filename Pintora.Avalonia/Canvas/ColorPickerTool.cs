using System;
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
		color = default;
		if (surface is null)
			return false;
		int x = (int) Math.Round (point.X), y = (int) Math.Round (point.Y);
		if ((uint) x >= surface.Width || (uint) y >= surface.Height)
			return false;
		color = surface.GetReadOnlyPixelData ()[y * surface.Width + x];
		return true;
	}
}
