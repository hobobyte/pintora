using System;
using NUnit.Framework;
using Avalonia;
using Pinta.Core;
using Pintora.Avalonia.Imaging;

namespace Pintora.Avalonia.Tests;

[TestFixture]
internal sealed class RasterBlitterTests
{
	[Test]
	public void CopyCenteredPlacesSmallerSourceInTheMiddle ()
	{
		using var source = new MemoryImageSurface (2, 2);
		source.Fill (ColorBgra.Red);
		using var destination = new MemoryImageSurface (4, 4);

		RasterBlitter.CopyCentered (source, destination);

		Assert.That (destination.GetReadOnlyPixelData ()[5], Is.EqualTo (ColorBgra.Red));
		Assert.That (destination.GetReadOnlyPixelData ()[10], Is.EqualTo (ColorBgra.Red));
		Assert.That (destination.GetReadOnlyPixelData ()[0], Is.EqualTo (ColorBgra.Transparent));
	}

	[Test]
	public void CopyCenteredClipsLargerSourceToDestination ()
	{
		using var source = new MemoryImageSurface (4, 4);
		source.Fill (ColorBgra.Blue);
		using var destination = new MemoryImageSurface (2, 2);

		RasterBlitter.CopyCentered (source, destination);

		Assert.That (destination.GetReadOnlyPixelData ().ToArray (), Is.All.EqualTo (ColorBgra.Blue));
	}

	[Test]
	public void CopyRegionCopiesRequestedArea ()
	{
		using var source = new MemoryImageSurface (4, 4);
		source.SetPixel (2, 1, ColorBgra.Green);
		using var destination = new MemoryImageSurface (2, 2);

		RasterBlitter.CopyRegion (source, new RectangleI (1, 0, 2, 2), destination);

		Assert.That (destination.GetReadOnlyPixelData ()[3], Is.EqualTo (ColorBgra.Green));
	}

	[Test]
	public void CopyRegionClipsAreaOutsideSource ()
	{
		using var source = new MemoryImageSurface (2, 2);
		source.SetPixel (0, 0, ColorBgra.Yellow);
		using var destination = new MemoryImageSurface (2, 2);

		RasterBlitter.CopyRegion (source, new RectangleI (-1, -1, 2, 2), destination);

		Assert.That (destination.GetReadOnlyPixelData ()[0], Is.EqualTo (ColorBgra.Yellow));
		Assert.That (destination.GetReadOnlyPixelData ()[1], Is.EqualTo (ColorBgra.Transparent));
	}

	[Test]
	public void CopyScaledPreservesAspectRatioAndCentersSource ()
	{
		using var source = new MemoryImageSurface (2, 1);
		source.GetPixelData ()[0] = ColorBgra.Red;
		source.GetPixelData ()[1] = ColorBgra.Blue;
		using var destination = new MemoryImageSurface (6, 6);

		RasterBlitter.CopyScaled (source, destination, new Rect (0, 0, 6, 6));

		Assert.That (destination.GetReadOnlyPixelData ()[19], Is.EqualTo (ColorBgra.Red));
		Assert.That (destination.GetReadOnlyPixelData ()[20], Is.EqualTo (ColorBgra.Red));
		Assert.That (destination.GetReadOnlyPixelData ()[22], Is.EqualTo (ColorBgra.Blue));
		Assert.That (destination.GetReadOnlyPixelData ()[23], Is.EqualTo (ColorBgra.Blue));
		Assert.That (destination.GetReadOnlyPixelData ()[0], Is.EqualTo (ColorBgra.Transparent));
	}

	[Test]
	public void CopyScaledCompositesOpacity ()
	{
		using var source = new MemoryImageSurface (1, 1);
		source.SetPixel (0, 0, ColorBgra.FromBgra (0, 0, 255, 128));
		using var destination = new MemoryImageSurface (1, 1);
		destination.SetPixel (0, 0, ColorBgra.White);

		RasterBlitter.CopyScaled (source, destination, new Rect (0, 0, 1, 1), 0.5, false);

		ColorBgra result = destination.GetReadOnlyPixelData ()[0];
		Assert.That (result.R, Is.EqualTo (255));
		Assert.That (result.G, Is.GreaterThan (120));
		Assert.That (result.G, Is.LessThan (255));
		Assert.That (result.B, Is.GreaterThan (120));
	}

	[Test]
	public void CopyScaledAppliesTint ()
	{
		using var source = new MemoryImageSurface (1, 1);
		source.SetPixel (0, 0, ColorBgra.White);
		using var destination = new MemoryImageSurface (1, 1);

		RasterBlitter.CopyScaled (source, destination, new Rect (0, 0, 1, 1), tint: ColorBgra.Blue);

		Assert.That (destination.GetReadOnlyPixelData ()[0], Is.EqualTo (ColorBgra.Blue));
	}

	[Test]
	public void CopyScaledRotationChangesRasterOrientation ()
	{
		using var source = new MemoryImageSurface (1, 2);
		source.SetPixel (0, 0, ColorBgra.Red);
		source.SetPixel (0, 1, ColorBgra.Blue);
		using var unrotated = new MemoryImageSurface (4, 4);
		using var rotated = new MemoryImageSurface (4, 4);

		RasterBlitter.CopyScaled (source, unrotated, new Rect (0, 0, 4, 4));
		RasterBlitter.CopyScaled (source, rotated, new Rect (0, 0, 4, 4), rotation: Math.PI / 2);

		Assert.That (rotated.GetReadOnlyPixelData ().ToArray (), Is.Not.EqualTo (unrotated.GetReadOnlyPixelData ().ToArray ()));
	}
}
