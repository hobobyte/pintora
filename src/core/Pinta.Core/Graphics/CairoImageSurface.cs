using System;
using Cairo;

namespace Pinta.Core;

/// <summary>
/// Cairo-backed implementation of <see cref="IImageSurface"/>.
/// </summary>
/// <remarks>
/// This adapter keeps the current renderer available while allowing consumers to depend on the
/// surface contract instead of the Cairo type directly.
/// </remarks>
public sealed class CairoImageSurface : IImageSurface
{
	private readonly ImageSurface surface;
	private readonly bool ownsSurface;

	public CairoImageSurface (ImageSurface surface)
		: this (surface, ownsSurface: true)
	{
	}

	/// <summary>
	/// Wraps an existing Cairo surface without taking ownership of it.
	/// </summary>
	public static CairoImageSurface Wrap (ImageSurface surface)
		=> new (surface, ownsSurface: false);

	private CairoImageSurface (ImageSurface surface, bool ownsSurface)
	{
		this.surface = surface ?? throw new ArgumentNullException (nameof (surface));
		this.ownsSurface = ownsSurface;
	}

	public ImageSurface NativeSurface => surface;

	public static implicit operator CairoImageSurface (ImageSurface surface)
		=> new (surface, ownsSurface: false);

	public static implicit operator ImageSurface (CairoImageSurface surface)
		=> surface.NativeSurface;

	public int Width => surface.Width;
	public int Height => surface.Height;

	public void Clear () => surface.Clear ();

	public ReadOnlySpan<ColorBgra> GetReadOnlyPixelData () => surface.GetReadOnlyPixelData ();

	public Span<ColorBgra> GetPixelData () => surface.GetPixelData ();

	public void Dispose ()
	{
		if (ownsSurface)
			surface.Dispose ();
	}
}
