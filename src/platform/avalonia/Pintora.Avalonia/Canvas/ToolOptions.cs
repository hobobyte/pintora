using System;

namespace Pintora.Avalonia.Canvas;

public interface IToolOptions { }

public sealed class BrushToolOptions : IToolOptions
{
	public int Size { get; set; } = 1;
	public double Opacity { get; set; } = 1;
	public double Hardness { get; set; } = 1;
}

public sealed record EffectToolOptions : IToolOptions
{
	public double Strength { get; set; } = 1;
	public int PixelSize { get; set; } = 8;
}

public sealed class TextToolOptions : IToolOptions
{
	// Start with an empty annotation value; placeholder/sample text must not
	// appear when the user switches between document tabs.
	public string Text { get; set; } = string.Empty;
	public string FontFamily { get; set; } = "Inter";
	public double FontSize { get; set; } = 16;
	public bool Bold { get; set; }
	public bool Underline { get; set; }
}

public sealed class ShapeToolOptions : IToolOptions
{
	public CanvasHost.ShapeKind Kind { get; set; } = CanvasHost.ShapeKind.Rectangle;
	public CanvasHost.ShapeFillStyle FillStyle { get; set; }
	public int OutlineWidth { get; set; } = 2;
	public CanvasHost.ShapeDashStyle Dash { get; set; }
	public int CornerRadius { get; set; } = 8;
	public bool Antialias { get; set; } = true;
}

public sealed class FillToolOptions : IToolOptions
{
	public int Tolerance { get; set; }
}

public sealed class GradientToolOptions : IToolOptions
{
	public bool Reversed { get; set; }
}

public sealed class ToolOptionsChangedEventArgs (IToolOptions options) : EventArgs
{
	public IToolOptions Options { get; } = options;
}
