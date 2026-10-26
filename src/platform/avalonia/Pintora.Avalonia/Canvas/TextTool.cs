using Pinta.Core;
using Avalonia.Media;

namespace Pintora.Avalonia.Canvas;

internal sealed class TextTool : ICanvasTool
{
	public CanvasHost.CanvasTool Kind => CanvasHost.CanvasTool.Text;
	public TextToolOptions Options { get; } = new ();
	public string Text { get => Options.Text; set => Options.Text = value; }
	public string FontFamily { get => Options.FontFamily; set => Options.FontFamily = value; }
	public double FontSize { get => Options.FontSize; set => Options.FontSize = value; }
	public bool Bold { get => Options.Bold; set => Options.Bold = value; }
	public bool Underline { get => Options.Underline; set => Options.Underline = value; }

	public bool PointerPressed (CanvasToolPointerContext context)
	{
		if (context.Host.IsActiveLayerLocked || string.IsNullOrWhiteSpace (Text))
			return false;
		context.Host.AddTextAnnotation (Text, context.Point, FontSize, context.Color, FontFamily,
			fontWeight: Bold ? FontWeight.Bold : FontWeight.Normal,
			underline: Underline);
		return true;
	}
}
