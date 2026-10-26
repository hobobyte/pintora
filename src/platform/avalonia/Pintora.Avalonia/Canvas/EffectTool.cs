using Avalonia;
using Pinta.Core;
using Pintora.Avalonia.Editor.Annotations;
using Pintora.Avalonia.Imaging;

namespace Pintora.Avalonia.Canvas;

internal sealed class EffectTool : ICanvasTool
{
	public CanvasHost.CanvasTool Kind { get; }
	public EffectToolOptions Options { get; set; } = new ();
	private PointD start;
	private PointD current;
	private bool active;
	public bool IsCreating => active;
	public PointD Start => start;
	public PointD Current => current;

	public EffectTool (CanvasHost.CanvasTool kind) => Kind = kind;

	public bool PointerPressed (CanvasToolPointerContext context)
	{
		if (context.Host.IsActiveLayerLocked || context.Host.Surface is not MemoryImageSurface)
			return false;
		start = context.Point;
		current = start;
		active = true;
		context.Host.BeginStroke ();
		return true;
	}

	public bool PointerMoved (CanvasToolPointerContext context)
	{
		if (!active) return false;
		current = context.Point;
		context.Host.InvalidateToolVisual ();
		return true;
	}

	public bool PointerReleased (CanvasToolPointerContext context)
	{
		if (!active || context.Host.Surface is not MemoryImageSurface surface || context.Host.ActiveLayer is not { } layer)
			return false;
		active = false;
		PointD end = context.Point;
		Rect bounds = new (System.Math.Min (start.X, end.X), System.Math.Min (start.Y, end.Y), System.Math.Abs (end.X - start.X), System.Math.Abs (end.Y - start.Y));
		if (bounds.Width < 1 || bounds.Height < 1) {
			context.Host.CancelCurrentStroke ();
			return false;
		}
		var annotation = new RegionEffectAnnotation {
			Bounds = bounds,
			Effect = Kind switch {
				CanvasHost.CanvasTool.Blur => RegionEffectKind.Blur,
				CanvasHost.CanvasTool.Pixelate => RegionEffectKind.Pixelate,
				CanvasHost.CanvasTool.Highlight => RegionEffectKind.Highlight,
				CanvasHost.CanvasTool.Magnify => RegionEffectKind.Magnify,
				_ => RegionEffectKind.Spotlight
			},
			Strength = Options.Strength,
			PixelSize = Options.PixelSize
		};
		layer.Annotations.Add (annotation);
		context.Host.CommitStroke ();
		context.Host.MarkCanvasChanged ();
		return true;
	}

	public void CancelPointerAction () => active = false;
}
