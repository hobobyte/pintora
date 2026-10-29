//
// GdkColors.cs
//
// Gdk.RGBA to Cairo.Color conversion. Remains in Pinta.Core while Core classes
// (Document, HsvColor, CairoExtensions.Color) still consume Gdk.RGBA; moves with
// them in a later slice.
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
