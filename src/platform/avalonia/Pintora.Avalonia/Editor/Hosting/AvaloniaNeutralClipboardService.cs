using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Pinta.Core;

namespace Pintora.Avalonia.Editor.Hosting;

/// <summary>Avalonia implementation of the platform-neutral clipboard contract.</summary>
public sealed class AvaloniaNeutralClipboardService (IClipboard clipboard) : Pinta.Core.IClipboardService
{
	public void SetText (string text) => _ = clipboard.SetTextAsync (text);
	public Task<string?> ReadTextAsync () => clipboard.TryGetTextAsync ();

	public async Task<bool> ContainsImageAsync ()
		=> await clipboard.TryGetBitmapAsync () is not null;

	public async Task<ClipboardImageData?> ReadImageAsync ()
	{
		using Bitmap? bitmap = await clipboard.TryGetBitmapAsync ();
		if (bitmap is null)
			return null;

		int width = bitmap.PixelSize.Width;
		int height = bitmap.PixelSize.Height;
		var surface = new Imaging.MemoryImageSurface (width, height);
		int stride = width * 4;
		byte[] pixels = new byte[stride * height];
		IntPtr buffer = Marshal.AllocHGlobal (pixels.Length);
		try {
			bitmap.CopyPixels (new PixelRect (0, 0, width, height), buffer, pixels.Length, stride);
			Marshal.Copy (buffer, pixels, 0, pixels.Length);
		} finally {
			Marshal.FreeHGlobal (buffer);
		}
		MemoryMarshal.Cast<byte, ColorBgra> (pixels).CopyTo (surface.GetPixelData ());
		return new ClipboardImageData (surface, new PointI ());
	}

	public async void WriteImage (ClipboardImageData image)
	{
		ArgumentNullException.ThrowIfNull (image);
		int stride = image.Surface.Width * 4;
		var bitmap = new WriteableBitmap (
			new PixelSize (image.Surface.Width, image.Surface.Height),
			new Vector (96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
		using (ILockedFramebuffer framebuffer = bitmap.Lock ())
		{
			byte[] pixels = MemoryMarshal.AsBytes (image.Surface.GetReadOnlyPixelData ()).ToArray ();
			Marshal.Copy (pixels, 0, framebuffer.Address, pixels.Length);
		}
		await clipboard.SetBitmapAsync (bitmap);
	}
}
