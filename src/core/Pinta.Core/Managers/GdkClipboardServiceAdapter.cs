using System.Threading.Tasks;
using Gdk;

namespace Pinta.Core;

/// <summary>Temporary compatibility adapter while GTK clipboard ownership moves to the platform layer.</summary>
internal sealed class GdkClipboardServiceAdapter : IClipboardService
{
	private readonly Clipboard clipboard;
	public GdkClipboardServiceAdapter (Clipboard clipboard) => this.clipboard = clipboard;
	public Task<bool> ContainsImageAsync () => Task.FromResult (clipboard.GetFormats ().ContainGtype (Texture.GetGType ()));
	public async Task<ClipboardImageData?> ReadImageAsync ()
	{
		Texture? texture = await clipboard.ReadTextureAsync ();
		return texture is null ? null : new ClipboardImageData (new CairoImageSurface (texture.ToSurface ()), new PointI ());
	}
	public void WriteImage (ClipboardImageData image) => throw new System.NotSupportedException ();
	public void SetText (string text) => clipboard.SetText (text);
	public Task<string?> ReadTextAsync () => clipboard.ReadTextAsync ();
}
