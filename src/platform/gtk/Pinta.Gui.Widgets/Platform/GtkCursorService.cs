using System;
using Gdk;
using Pinta.Core;
using Pinta.Resources;

namespace Pinta.Gui.Widgets;

/// <summary>GTK implementation of the platform-neutral cursor contract.</summary>
public sealed class GtkCursorService : ICursorService
{
	public object? CreateCursor (CursorDescriptor descriptor)
	{
		if (descriptor.ResourceName.EndsWith (".png", StringComparison.OrdinalIgnoreCase)) {
<<<<<<< HEAD
			Gdk.Texture texture = GtkIconProvider.GetIcon (descriptor.ResourceName, 32);
=======
			Gdk.Texture texture = ResourceLoader.GetIcon (descriptor.ResourceName, 32);
>>>>>>> origin/master
			return Cursor.NewFromTexture (texture, descriptor.HotspotX, descriptor.HotspotY, null);
		}

		return GdkExtensions.CursorFromName (descriptor.ResourceName);
	}

	public object? CreateShapedCursor (CursorDescriptor descriptor, CursorShape shape, double scale, int shapeWidth)
	{
		Gdk.Texture texture = GdkExtensions.CreateIconWithShape (
			descriptor.ResourceName,
			shape,
			scale,
			shapeWidth,
			descriptor.HotspotX,
			descriptor.HotspotY,
			out int hotspotX,
			out int hotspotY);
		return Cursor.NewFromTexture (texture, hotspotX, hotspotY, null);
	}

	public object? CreateShapedIconCursor (CursorDescriptor baseCursor, CursorShape shape, double scale, int shapeWidth, int angle)
	{
		Gdk.Texture texture = GdkExtensions.CreateIconWithShape (
			baseCursor.ResourceName,
			shape,
			scale,
			shapeWidth,
			shapeWidth,
			angle,
			baseCursor.HotspotX,
			baseCursor.HotspotY,
			out int hotspotX,
			out int hotspotY);
		return Cursor.NewFromTexture (texture, hotspotX, hotspotY, null);
	}

	public object? GetWidgetCursor (object nativeWidget)
		=> ((Gtk.Widget) nativeWidget).Cursor;

	public void SetWidgetCursor (object nativeWidget, object? cursor)
		=> ((Gtk.Widget) nativeWidget).Cursor = (Gdk.Cursor?) cursor;
}
