using System.Linq;

namespace Pintora.Avalonia.Canvas;

internal static class CanvasToolFactory
{
	public static CanvasToolSet CreateTools ()
	{
		ICanvasTool[] tools = [
			new SelectionTool (),
			new BrushTool (CanvasHost.CanvasTool.Brush),
			new BrushTool (CanvasHost.CanvasTool.Pencil),
			new ShapeTool (),
			new GradientTool (),
			new FillTool (),
			new ColorPickerTool (),
			new ZoomTool (),
			new BrushTool (CanvasHost.CanvasTool.Eraser)
		];
		return new CanvasToolSet (tools);
	}
}

internal sealed class CanvasToolSet
{
	private readonly System.Collections.Generic.Dictionary<CanvasHost.CanvasTool, ICanvasTool> tools;
	public CanvasToolSet (ICanvasTool[] toolInstances)
		=> tools = toolInstances.ToDictionary (tool => tool.Kind);

	public T Get<T> (CanvasHost.CanvasTool kind) where T : class, ICanvasTool
		=> tools[kind] as T ?? throw new System.InvalidOperationException ($"Tool {kind} has an unexpected controller type.");

	public BrushTool Brush (CanvasHost.CanvasTool kind) => Get<BrushTool> (kind);
	public bool TryGetBrush (CanvasHost.CanvasTool kind, out BrushTool? brush)
	{
		if (tools.TryGetValue (kind, out ICanvasTool? tool) && tool is BrushTool typed) {
			brush = typed;
			return true;
		}
		brush = null;
		return false;
	}

	public void CancelPointerActions ()
	{
		foreach (ICanvasTool tool in tools.Values)
			tool.CancelPointerAction ();
	}
}
