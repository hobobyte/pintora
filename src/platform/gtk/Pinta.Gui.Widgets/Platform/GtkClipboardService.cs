using System;
using System.Threading.Tasks;
using Gdk;
using Pinta.Core;

namespace Pinta.Gui.Widgets;

/// <summary>GTK clipboard adapter for the neutral clipboard contract.</summary>
public sealed class GtkClipboardService : IClipboardService
{
	private readonly Clipboard clipboard;

	public GtkClipboardService (Clipboard clipboard)
	{
		this.clipboard = clipboard ?? throw new ArgumentNullException (nameof (clipboard));
	}

	public Task<bool> ContainsImageAsync ()
		=> Task.FromResult (clipboard.GetFormats ().ContainGtype (Texture.GetGType ()));

	public async Task<ClipboardImageData?> ReadImageAsync ()
	{
		Gdk.Texture? texture = await clipboard.ReadTextureAsync ();
		if (texture is null)
			return null;

		Cairo.ImageSurface surface = texture.ToSurface ();
		return new ClipboardImageData (new CairoImageSurface (surface), new PointI (0, 0));
	}

	public void WriteImage (ClipboardImageData image)
	{
		ArgumentNullException.ThrowIfNull (image);

		if (image.Surface is not CairoImageSurface cairo)
			throw new NotSupportedException ("GTK clipboard currently requires a Cairo-backed image surface.");

		clipboard.SetContent (GdkExtensions.CreateContentProviderUnion ([
			Gdk.ContentProvider.NewForValue (CreateTextureValue (cairo.NativeSurface.ToTexture ()))
		]));
	}

	public void SetText (string text) => clipboard.SetText (text);

	public Task<string?> ReadTextAsync () => clipboard.ReadTextAsync ();

	private static GObject.Value CreateTextureValue (Texture texture)
	{
		GObject.Value value = new (Texture.GetGType ());
		value.SetObject (texture);
		return value;
	}
}
