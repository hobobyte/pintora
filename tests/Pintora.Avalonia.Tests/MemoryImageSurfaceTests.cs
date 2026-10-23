using NUnit.Framework;
using Pinta.Core;
using Pintora.Avalonia.Imaging;

namespace Pintora.Avalonia.Tests;

[TestFixture]
internal sealed class MemoryImageSurfaceTests
{
	[Test]
	public void StoresBgraPixels ()
	{
		using MemoryImageSurface surface = new (2, 1);
		surface.GetPixelData ()[0] = ColorBgra.Red;

		Assert.That (surface.GetReadOnlyPixelData ()[0], Is.EqualTo (ColorBgra.Red));
	}

	[Test]
	public void ClearResetsPixels ()
	{
		using MemoryImageSurface surface = new (2, 1);
		surface.Fill (ColorBgra.Blue);
		surface.Clear ();

		Assert.That (surface.GetReadOnlyPixelData ()[0], Is.EqualTo (ColorBgra.Zero));
	}
}
