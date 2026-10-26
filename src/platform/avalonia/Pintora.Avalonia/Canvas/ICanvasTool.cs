using Avalonia;
using Pinta.Core;

namespace Pintora.Avalonia.Canvas;

internal interface ICanvasTool
{
	CanvasHost.CanvasTool Kind { get; }
	bool PointerPressed (CanvasToolPointerContext context) => false;
	bool PointerMoved (CanvasToolPointerContext context) => false;
	bool PointerReleased (CanvasToolPointerContext context) => false;
	void CancelPointerAction () { }
}

internal readonly record struct CanvasToolPointerContext (
	CanvasHost Host,
	Point ViewPoint,
	PointD Point,
	PointD PreviousPoint,
	ColorBgra Color,
	bool IsRightButton,
	bool ShiftPressed);
