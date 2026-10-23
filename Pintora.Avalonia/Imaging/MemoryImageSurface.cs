using System;
using Pinta.Core;

namespace Pintora.Avalonia.Imaging;

public sealed class MemoryImageSurface : IImageSurface
{
	private readonly ColorBgra[] pixels;

	public MemoryImageSurface (int width, int height)
	{
		if (width <= 0 || height <= 0)
			throw new ArgumentOutOfRangeException (nameof (width));

		Width = width;
		Height = height;
		pixels = new ColorBgra[width * height];
	}

	public int Width { get; }
	public int Height { get; }
	public void Clear () => Array.Clear (pixels);
	public ReadOnlySpan<ColorBgra> GetReadOnlyPixelData () => pixels;
	public Span<ColorBgra> GetPixelData () => pixels;
	public void Dispose () { }
	public void Fill (ColorBgra color) => pixels.AsSpan ().Fill (color);

	public void SetPixel (int x, int y, ColorBgra color)
	{
		if ((uint) x >= Width || (uint) y >= Height)
			return;

		pixels[y * Width + x] = color;
	}
}
