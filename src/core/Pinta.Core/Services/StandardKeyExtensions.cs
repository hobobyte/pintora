namespace Pinta.Core;

/// <summary>Maps neutral <see cref="StandardKey"/> values to native key values.</summary>
public static class StandardKeyExtensions
{
	/// <summary>Map a neutral key to the GDK key value (the GTK platform mapping).</summary>
	public static uint ToKeyValue (this StandardKey key)
		=> key switch {
			StandardKey.Delete => Gdk.Constants.KEY_Delete,
			StandardKey.Return => Gdk.Constants.KEY_Return,
			StandardKey.KeypadEnter => Gdk.Constants.KEY_KP_Enter,
			StandardKey.Space => Gdk.Constants.KEY_space,
			StandardKey.Up => Gdk.Constants.KEY_Up,
			StandardKey.Down => Gdk.Constants.KEY_Down,
			StandardKey.Left => Gdk.Constants.KEY_Left,
			StandardKey.Right => Gdk.Constants.KEY_Right,
			StandardKey.BracketLeft => Gdk.Constants.KEY_bracketleft,
			StandardKey.BracketRight => Gdk.Constants.KEY_bracketright,
			_ => Gdk.Constants.KEY_VoidSymbol,
		};
}
