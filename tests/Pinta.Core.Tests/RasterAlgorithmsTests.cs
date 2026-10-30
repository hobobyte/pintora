using NUnit.Framework;

namespace Pinta.Core.Tests;

[TestFixture]
internal sealed class RasterAlgorithmsTests
{
	[Test]
	public void FloodFill_ChangesOnlyContiguousMatchingPixels ()
	{
		using var surface = new ArrayImageSurface (4, 1);
		ColorBgra source = ColorBgra.White;
		ColorBgra barrier = ColorBgra.Black;
		ColorBgra replacement = ColorBgra.Red;
		surface.GetPixelData ().Fill (source);
		surface.GetPixelData ()[2] = barrier;

		Assert.That (RasterAlgorithms.FloodFill (surface, new PointD (0, 0), replacement, 0), Is.True);
		ColorBgra[] pixels = surface.GetReadOnlyPixelData ().ToArray ();
		Assert.That (pixels[0], Is.EqualTo (replacement));
		Assert.That (pixels[1], Is.EqualTo (replacement));
		Assert.That (pixels[2], Is.EqualTo (barrier));
		Assert.That (pixels[3], Is.EqualTo (source));
	}

	[Test]
	public void GradientAndSampling_UseSharedCoreMath ()
	{
		using var surface = new ArrayImageSurface (2, 1);
		RasterAlgorithms.ApplyLinearGradient (surface, new PointD (0, 0), new PointD (1, 0), ColorBgra.Black, ColorBgra.White, false);

		Assert.That (RasterAlgorithms.TrySampleNearest (surface, new PointD (1, 0), out ColorBgra sampled), Is.True);
		Assert.That (sampled, Is.EqualTo (ColorBgra.White));
		Assert.That (surface.GetReadOnlyPixelData ()[0], Is.EqualTo (ColorBgra.Black));
	}

	[Test]
	public void CompositeOver_AppliesLayerOpacity ()
	{
		ColorBgra source = RasterAlgorithms.ApplyOpacity (ColorBgra.FromBgra (0, 0, 255, 255), 0.5);
		ColorBgra result = RasterAlgorithms.CompositeOver (ColorBgra.FromBgra (255, 0, 0, 255), source);

		Assert.That (result.A, Is.EqualTo (255));
		Assert.That (result.R, Is.EqualTo (128).Within (1));
		Assert.That (result.B, Is.EqualTo (127).Within (1));
	}
}
