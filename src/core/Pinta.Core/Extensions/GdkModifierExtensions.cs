//
// GdkModifierExtensions.cs
//
// GDK modifier-key extensions. These remain in Pinta.Core for now because the
// tool/mouse event args (ToolKeyEventArgs, ToolMouseEventArgs) are still
// Gdk-typed; they move to the GTK platform together with those event args
// in a later Core slice.
//

namespace Pinta.Core;

public static class GdkModifierExtensions
{
	public static bool IsShiftPressed (this Gdk.ModifierType m)
		=> m.HasFlag (Gdk.ModifierType.ShiftMask);

	/// <summary>
	/// Returns whether a Ctrl modifier is pressed (or the Cmd key on macOS).
	/// </summary>
	public static bool IsControlPressed (this Gdk.ModifierType m)
	{
		if (PintaCore.System.OperatingSystem == OS.Mac)
			return m.HasFlag (Gdk.ModifierType.MetaMask);
		else
			return m.HasFlag (Gdk.ModifierType.ControlMask);
	}

	public static bool IsAltPressed (this Gdk.ModifierType m)
		=> m.HasFlag (Gdk.ModifierType.AltMask);

	public static bool IsLeftMousePressed (this Gdk.ModifierType m)
		=> m.HasFlag (Gdk.ModifierType.Button1Mask);

	public static bool IsRightMousePressed (this Gdk.ModifierType m)
		=> m.HasFlag (Gdk.ModifierType.Button3Mask);

	/// <summary>
	/// Returns whether any of the Ctrl/Cmd/Shift/Alt modifiers are active.
	/// This prevents Caps Lock, Num Lock, etc from appearing as active modifier keys.
	/// </summary>
	public static bool HasModifierKey (this Gdk.ModifierType current_state)
		=> current_state.IsControlPressed () || current_state.IsShiftPressed () || current_state.IsAltPressed ();
}
