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
			StandardKey.BackSpace => Gdk.Constants.KEY_BackSpace,
			StandardKey.Home => Gdk.Constants.KEY_Home,
			StandardKey.End => Gdk.Constants.KEY_End,
			StandardKey.PageUp => Gdk.Constants.KEY_Prior,
			StandardKey.PageDown => Gdk.Constants.KEY_Next,
			StandardKey.Escape => Gdk.Constants.KEY_Escape,
			StandardKey.Insert => Gdk.Constants.KEY_Insert,
			StandardKey.A => Gdk.Constants.KEY_a,
			StandardKey.B => Gdk.Constants.KEY_b,
			StandardKey.E => Gdk.Constants.KEY_e,
			StandardKey.F => Gdk.Constants.KEY_f,
			StandardKey.G => Gdk.Constants.KEY_g,
			StandardKey.H => Gdk.Constants.KEY_h,
			StandardKey.I => Gdk.Constants.KEY_i,
			StandardKey.K => Gdk.Constants.KEY_k,
			StandardKey.L => Gdk.Constants.KEY_l,
			StandardKey.M => Gdk.Constants.KEY_m,
			StandardKey.O => Gdk.Constants.KEY_o,
			StandardKey.P => Gdk.Constants.KEY_p,
			StandardKey.R => Gdk.Constants.KEY_r,
			StandardKey.S => Gdk.Constants.KEY_s,
			StandardKey.T => Gdk.Constants.KEY_t,
			StandardKey.U => Gdk.Constants.KEY_u,
			StandardKey.Z => Gdk.Constants.KEY_z,
			_ => Gdk.Constants.KEY_VoidSymbol,
		};
}
