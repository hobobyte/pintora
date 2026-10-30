//
// GdkColors.cs
//
// Gdk.RGBA to Cairo.Color conversion. Lives in the GTK platform assembly;
// namespace kept as Pinta.Core so call sites need no using changes.
//

namespace Pinta.Core;

public static class GdkColors
{
	/// <summary>
	/// Convert from Gdk.RGBA to Cairo.Color.
	/// </summary>
	public static Cairo.Color ToCairoColor (this Gdk.RGBA color)
		=> new (color.Red, color.Green, color.Blue, color.Alpha);
}
