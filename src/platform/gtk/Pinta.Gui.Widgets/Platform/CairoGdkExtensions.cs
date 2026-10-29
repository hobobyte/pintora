//
// CairoGdkExtensions.cs
//
// Cairo surface to Gdk pixbuf/texture conversions. GTK-platform helpers that
// live in Pinta.Gui.Widgets; namespace kept as Pinta.Core so call sites need
// no using changes.
//

using System;
using System.Runtime.InteropServices;
using Cairo;

namespace Pinta.Core;

public static class CairoGdkExtensions
{
	public static GdkPixbuf.Pixbuf ToPixbuf (this ImageSurface sourceSurface, bool includeAlpha = true)
	{
		int width = sourceSurface.Width;
		int height = sourceSurface.Height;

		if (includeAlpha) {
			return Gdk.Functions.PixbufGetFromSurface (sourceSurface, 0, 0, width, height)!;
		}

		// If we need a pixbuf without alpha, it's easiest to convert from a temporary RGB cairo surface.
		using Cairo.ImageSurface rgbSurface = new (Format.Rgb24, width, height);
		using Cairo.Context context = new (rgbSurface);
		context.SetSourceSurface (sourceSurface, 0.0, 0.0);
		context.Paint ();

		return Gdk.Functions.PixbufGetFromSurface (rgbSurface, 0, 0, width, height)!;
	}

	/// <summary>
	/// Convert a Cairo surface to a Gdk.Texture.
	/// This can optionally be configured to act as an update to a region of an existing texture.
	/// </summary>
	public static Gdk.Texture ToTexture (this Cairo.ImageSurface surface, Gdk.Texture? updateTexture = null, Cairo.Region? updateRegion = null)
	{
		GLib.Bytes bytes = GLib.Bytes.New (surface.GetData ());

		Gdk.MemoryTextureBuilder builder = Gdk.MemoryTextureBuilder.New ();
		builder.Bytes = bytes;
		builder.Stride = (ulong) surface.Stride;
		builder.Width = surface.Width;
		builder.Height = surface.Height;
		builder.Format = Gdk.MemoryFormat.B8g8r8a8Premultiplied;
		builder.UpdateTexture = updateTexture;
		builder.UpdateRegion = updateRegion ?? CairoExtensions.CreateRegion (RectangleI.Zero);

		return builder.Build ();
	}
}
