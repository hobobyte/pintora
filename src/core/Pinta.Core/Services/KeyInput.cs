using System;

namespace Pinta.Core;

/// <summary>
/// Platform-neutral keyboard modifier keys.
/// The flag values match GDK's ModifierType bits, so the GTK platform can convert
/// with a simple cast.
/// </summary>
[Flags]
public enum ModifierKeys : uint
{
	None = 0,
	Shift = 1 << 0,
	Control = 1 << 2,
	Alt = 1 << 3,
	LeftMouseButton = 1 << 8,
	RightMouseButton = 1 << 10,
	Meta = 1 << 26,
}

public static class ModifierKeysExtensions
{
	public static bool IsShiftPressed (this ModifierKeys m)
		=> m.HasFlag (ModifierKeys.Shift);

	/// <summary>
	/// Returns whether a Ctrl modifier is pressed (or the Cmd key on macOS).
	/// </summary>
	public static bool IsControlPressed (this ModifierKeys m)
	{
		if (PintaCore.System.OperatingSystem == OS.Mac)
			return m.HasFlag (ModifierKeys.Meta);
		else
			return m.HasFlag (ModifierKeys.Control);
	}

	public static bool IsAltPressed (this ModifierKeys m)
		=> m.HasFlag (ModifierKeys.Alt);

	public static bool IsLeftMousePressed (this ModifierKeys m)
		=> m.HasFlag (ModifierKeys.LeftMouseButton);

	public static bool IsRightMousePressed (this ModifierKeys m)
		=> m.HasFlag (ModifierKeys.RightMouseButton);

	/// <summary>
	/// Returns whether any of the Ctrl/Cmd/Shift/Alt modifiers are active.
	/// This prevents Caps Lock, Num Lock, etc from appearing as active modifier keys.
	/// </summary>
	public static bool HasModifierKey (this ModifierKeys current_state)
		=> current_state.IsControlPressed () || current_state.IsShiftPressed () || current_state.IsAltPressed ();
}

/// <summary>
/// Wrapper for platform-neutral key values (GDK keyvals on the GTK platform,
/// see https://docs.gtk.org/gdk4/keys.html).
/// </summary>
public readonly record struct ToolKey (uint Value)
{
	/// <summary>GDK_KEY_VoidSymbol - no key.</summary>
	public static ToolKey Invalid { get; } = new (KeyValues.VoidSymbol);

	// GDK_KEY_Control_L / GDK_KEY_Control_R / GDK_KEY_Meta_L / GDK_KEY_Meta_R (fixed X11 values).
	private const uint ControlL = 0xffe3;
	private const uint ControlR = 0xffe4;
	private const uint MetaL = 0xffe7;
	private const uint MetaR = 0xffe8;

	/// <summary>
	/// Returns whether this key is a Ctrl key (or the Cmd key on macOS).
	/// </summary>
	public bool IsControlKey ()
	{
		if (PintaCore.System.OperatingSystem == OS.Mac)
			return Value == MetaL || Value == MetaR;
		else
			return Value == ControlL || Value == ControlR;
	}

	/// <summary>
	/// Returns whether this key matches a platform-neutral <see cref="StandardKey"/>.
	/// </summary>
	public bool Is (StandardKey key) => Value == key.ToKeyValue ();

	/// <summary>
	/// Upper-case version of this key. All tool shortcut keys are ASCII letters,
	/// for which this matches GDK's KeyvalToUpper() behavior.
	/// </summary>
	public ToolKey ToUpper ()
		=> Value is >= 'a' and <= 'z' ? new ToolKey (Value - ('a' - 'A')) : this;
}
