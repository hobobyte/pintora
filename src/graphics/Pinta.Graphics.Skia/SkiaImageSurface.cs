using SkiaSharp;
using System.Runtime.InteropServices;

namespace Pinta.Graphics.Skia;

public sealed class SkiaImageSurface : Pinta.Core.IImageSurface
{
	private readonly SKBitmap bitmap;
	private readonly Pinta.Core.ColorBgra[] pixels;
	public int Width => bitmap.Width;
	public int Height => bitmap.Height;

	public SkiaImageSurface (SKBitmap bitmap)
	{
		this.bitmap = bitmap;
		pixels = new Pinta.Core.ColorBgra[Width * Height];
		byte[] bytes = new byte[pixels.Length * sizeof (uint)];
		Marshal.Copy (bitmap.GetPixels (), bytes, 0, bytes.Length);
		MemoryMarshal.Cast<byte, Pinta.Core.ColorBgra> (bytes).CopyTo (pixels);
	}

	internal SKBitmap Bitmap => bitmap;
	internal void SyncToBitmap ()
	{
		ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes (pixels.AsSpan ());
		Marshal.Copy (bytes.ToArray (), 0, bitmap.GetPixels (), bytes.Length);
	}
	public void Clear () => Array.Clear (pixels);
	public ReadOnlySpan<Pinta.Core.ColorBgra> GetReadOnlyPixelData () => pixels;
	public Span<Pinta.Core.ColorBgra> GetPixelData () => pixels;
	public void Dispose () => bitmap.Dispose ();
}
