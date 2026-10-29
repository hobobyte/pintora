namespace Pinta.Core;

/// <summary>Maps neutral <see cref="StandardKey"/> values to platform key values.</summary>
public static class StandardKeyExtensions
{
	/// <summary>Map a neutral key to the key value used by the platform (GDK keyvals on GTK).</summary>
	public static uint ToKeyValue (this StandardKey key)
		=> key switch {
			StandardKey.Delete => KeyValues.Delete,
			StandardKey.Return => KeyValues.Return,
			StandardKey.KeypadEnter => KeyValues.KeypadEnter,
			StandardKey.Space => KeyValues.Space,
			StandardKey.Up => KeyValues.Up,
			StandardKey.Down => KeyValues.Down,
			StandardKey.Left => KeyValues.Left,
			StandardKey.Right => KeyValues.Right,
			StandardKey.BracketLeft => KeyValues.BracketLeft,
			StandardKey.BracketRight => KeyValues.BracketRight,
			StandardKey.BackSpace => KeyValues.BackSpace,
			StandardKey.Home => KeyValues.Home,
			StandardKey.End => KeyValues.End,
			StandardKey.PageUp => KeyValues.PageUp,
			StandardKey.PageDown => KeyValues.PageDown,
			StandardKey.Escape => KeyValues.Escape,
			StandardKey.Insert => KeyValues.Insert,
			StandardKey.A => KeyValues.A,
			StandardKey.B => KeyValues.B,
			StandardKey.E => KeyValues.E,
			StandardKey.F => KeyValues.F,
			StandardKey.G => KeyValues.G,
			StandardKey.H => KeyValues.H,
			StandardKey.I => KeyValues.I,
			StandardKey.K => KeyValues.K,
			StandardKey.L => KeyValues.L,
			StandardKey.M => KeyValues.M,
			StandardKey.O => KeyValues.O,
			StandardKey.P => KeyValues.P,
			StandardKey.R => KeyValues.R,
			StandardKey.S => KeyValues.S,
			StandardKey.T => KeyValues.T,
			StandardKey.U => KeyValues.U,
			StandardKey.Z => KeyValues.Z,
			_ => KeyValues.VoidSymbol,
		};
}
