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
			Gdk.Texture texture = ResourceLoader.GetIcon (descriptor.ResourceName, 32);
			return Cursor.NewFromTexture (texture, descriptor.HotspotX, descriptor.HotspotY, null);
		}

		return GdkExtensions.CursorFromName (descriptor.ResourceName);
	}
}
