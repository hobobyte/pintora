using System;
using Pinta.Core;
using Pintora.Avalonia.Imaging;

namespace Pintora.Avalonia.Canvas;

/// <summary>
/// Select-by-color tool, ported from the GTK MagicWandTool / FloodTool
/// (`Pinta.Tools/Tools/MagicWandTool.cs`, `FloodTool.cs`). Uses the same
/// tolerance math (<see cref="ColorBgra.ColorsWithinTolerance"/>) and the same
/// modifier mapping as the GTK SelectionModeHandler:
/// Ctrl+click = Add, Alt+click = Intersect, right-click = Subtract,
/// Ctrl+right-click = Xor, Shift (or Global mode) = full-layer scan.
/// </summary>
internal sealed class MagicWandTool : ICanvasTool
{
	public CanvasHost.CanvasTool Kind => CanvasHost.CanvasTool.MagicWand;

	public bool PointerPressed (CanvasToolPointerContext context)
	{
		CanvasHost host = context.Host;
		if (host.Surface is not MemoryImageSurface surface)
			return false;
		PointD bounded = SelectionAlgorithms.ClampPointToImage (context.Point, new Pinta.Core.Size (surface.Width - 1, surface.Height - 1));
		int x = (int) bounded.X;
		int y = (int) bounded.Y;

		bool global = host.MagicWandGlobalMode || context.ShiftPressed;
		int tolerance = FloodStencil.PercentToTolerance (host.MagicWandTolerance);
		bool[]? mask = global
			? FloodStencil.ByColor (surface, x, y, tolerance)
			: FloodStencil.FromPoint (surface, x, y, tolerance);
		if (mask is null)
			return false;

		SelectionCombineMode mode = DetermineMode (context);
		return host.ApplySelectionMask (mask, mode);
	}

	private static SelectionCombineMode DetermineMode (CanvasToolPointerContext context)
	{
		if (context.IsRightButton)
			return context.ControlPressed ? SelectionCombineMode.Xor : SelectionCombineMode.Subtract;
		if (context.ControlPressed)
			return SelectionCombineMode.Add;
		if (context.AltPressed)
			return SelectionCombineMode.Intersect;
		return SelectionCombineMode.Replace;
	}

}
