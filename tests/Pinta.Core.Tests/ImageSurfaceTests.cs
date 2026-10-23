using Cairo;
using NUnit.Framework;

namespace Pinta.Core.Tests;

[TestFixture]
internal sealed class ImageSurfaceTests
{
	[Test]
	public void CairoAdapter_ReportsDimensionsAndPixels ()
	{
		using ImageSurface cairoSurface = new (Format.Argb32, 3, 2);
		using IImageSurface surface = new CairoImageSurface (cairoSurface);

		Assert.That (surface.Width, Is.EqualTo (3));
		Assert.That (surface.Height, Is.EqualTo (2));
		int pixelCount = surface.GetReadOnlyPixelData ().Length;
		Assert.That (pixelCount, Is.EqualTo (6));
	}

	[Test]
	public void CairoAdapter_ClearsPixels ()
	{
		using ImageSurface cairoSurface = new (Format.Argb32, 2, 2);
		using IImageSurface surface = new CairoImageSurface (cairoSurface);
		surface.GetPixelData ()[0] = ColorBgra.Red;
		cairoSurface.MarkDirty ();

		surface.Clear ();
		cairoSurface.Flush ();

		Assert.That (surface.GetReadOnlyPixelData ()[0], Is.EqualTo (default (ColorBgra)));
	}

	[Test]
	public void Layer_Exposes_RasterSurface_Bridge ()
	{
		using ImageSurface cairoSurface = new (Format.Argb32, 4, 3);
		Layer layer = new (cairoSurface);

		using IImageSurface rasterSurface = layer.RasterSurface;

		Assert.That (rasterSurface.Width, Is.EqualTo (4));
		Assert.That (rasterSurface.Height, Is.EqualTo (3));
		Assert.That (layer.CairoSurface, Is.SameAs (cairoSurface));
	}
}
