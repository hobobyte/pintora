namespace Pintora.Avalonia.Canvas;

internal sealed class RecolorTool : ICanvasTool
{
	private bool painting;
	private bool reverse;

	public CanvasHost.CanvasTool Kind => CanvasHost.CanvasTool.Recolor;

	public bool PointerPressed (CanvasToolPointerContext context)
	{
		painting = true;
		reverse = context.IsRightButton;
		return context.Host.RecolorAt (context.Point, reverse);
	}

	public bool PointerMoved (CanvasToolPointerContext context)
		=> painting && context.Host.RecolorAt (context.Point, reverse);

	public bool PointerReleased (CanvasToolPointerContext context)
	{
		painting = false;
		return true;
	}

	public void CancelPointerAction () => painting = false;
}
