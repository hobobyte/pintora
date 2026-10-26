using System;

namespace Pinta.Core;

/// <summary>
/// CPU-backed image surface for headless processing and tests.
/// </summary>
public sealed class ArrayImageSurface : IImageSurface
{
	private readonly ColorBgra[] pixels;

	public ArrayImageSurface (int width, int height)
	{
		if (width < 0) throw new ArgumentOutOfRangeException (nameof (width));
		if (height < 0) throw new ArgumentOutOfRangeException (nameof (height));
		Width = width;
		Height = height;
		pixels = new ColorBgra[checked (width * height)];
	}

	public int Width { get; }
	public int Height { get; }

	public void Clear () => Array.Clear (pixels);
	public ReadOnlySpan<ColorBgra> GetReadOnlyPixelData () => pixels;
	public Span<ColorBgra> GetPixelData () => pixels;
	public void Dispose () { }
}
