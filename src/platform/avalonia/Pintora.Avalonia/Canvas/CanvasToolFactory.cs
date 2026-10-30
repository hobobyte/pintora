using System.Linq;

namespace Pintora.Avalonia.Canvas;

public static class CanvasToolFactory
{
	public static ToolDescriptor[] Descriptors { get; } = [
		new (CanvasHost.CanvasTool.Selection, "tool.selection", "Selection", "Select"),
		new (CanvasHost.CanvasTool.Text, "tool.text", "Text", "Draw"),
		new (CanvasHost.CanvasTool.Brush, "tool.brush", "Brush", "Paint"),
		new (CanvasHost.CanvasTool.Pencil, "tool.pencil", "Pencil", "Paint"),
		new (CanvasHost.CanvasTool.Shapes, "tool.shapes", "Shapes", "Draw"),
		new (CanvasHost.CanvasTool.Gradient, "tool.gradient", "Gradient", "Fill"),
		new (CanvasHost.CanvasTool.Fill, "tool.fill", "Fill", "Fill"),
		new (CanvasHost.CanvasTool.MagicWand, "tool.magic-wand", "Magic Wand", "Select"),
		new (CanvasHost.CanvasTool.ColorPicker, "tool.color-picker", "Color Picker", "Colors"),
		new (CanvasHost.CanvasTool.Zoom, "tool.zoom", "Zoom", "View", false),
		new (CanvasHost.CanvasTool.Eraser, "tool.eraser", "Eraser", "Paint"),
		new (CanvasHost.CanvasTool.Blur, "tool.blur", "Blur", "Effects"),
		new (CanvasHost.CanvasTool.Pixelate, "tool.pixelate", "Pixelate", "Effects"),
		new (CanvasHost.CanvasTool.Highlight, "tool.highlight", "Highlight", "Effects"),
		new (CanvasHost.CanvasTool.Magnify, "tool.magnify", "Magnify", "Effects"),
		new (CanvasHost.CanvasTool.Spotlight, "tool.spotlight", "Spotlight", "Effects")
	];

	internal static CanvasToolSet CreateTools ()
	{
		ICanvasTool[] tools = [
			new SelectionTool (),
			new TextTool (),
			new BrushTool (CanvasHost.CanvasTool.Brush),
			new BrushTool (CanvasHost.CanvasTool.Pencil),
			new ShapeTool (),
			new GradientTool (),
			new FillTool (),
			new MagicWandTool (),
			new ColorPickerTool (),
			new ZoomTool (),
			new BrushTool (CanvasHost.CanvasTool.Eraser),
			new EffectTool (CanvasHost.CanvasTool.Blur),
			new EffectTool (CanvasHost.CanvasTool.Pixelate),
			new EffectTool (CanvasHost.CanvasTool.Highlight),
			new EffectTool (CanvasHost.CanvasTool.Magnify),
			new EffectTool (CanvasHost.CanvasTool.Spotlight)
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
