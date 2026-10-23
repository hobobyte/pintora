using System;

namespace Pinta.Core;

/// <summary>
/// UI-independent access to a raster image surface.
/// </summary>
/// <remarks>
/// Implementations own their pixel storage and may use CPU or GPU-backed memory. The first
/// contract intentionally exposes only the operations needed by document state and Core tests.
/// </remarks>
public interface IImageSurface : IDisposable
{
	int Width { get; }
	int Height { get; }

	void Clear ();

	ReadOnlySpan<ColorBgra> GetReadOnlyPixelData ();

	Span<ColorBgra> GetPixelData ();
}
