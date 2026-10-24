namespace Pintora.Avalonia.Canvas;

public sealed record ToolDescriptor (
	CanvasHost.CanvasTool Tool,
	string Id,
	string DisplayName,
	string Group,
	bool SupportsOptions = true);
