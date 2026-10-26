using System;
using NUnit.Framework;

namespace Pinta.Core.Tests;

[TestFixture]
internal sealed class NeutralBlendOpTests
{
	[Test]
	public void BinaryBlendOperationWorksWithNeutralSurface ()
	{
		using var destination = new ArrayImageSurface (1, 1);
		using var lhs = new ArrayImageSurface (1, 1);
		using var rhs = new ArrayImageSurface (1, 1);
		lhs.GetPixelData ()[0] = ColorBgra.FromBgra (10, 20, 30, 255);
		rhs.GetPixelData ()[0] = ColorBgra.FromBgra (20, 30, 40, 255);

		new UserBlendOps.NormalBlendOp ().Apply (destination, lhs, rhs);

		Assert.That (destination.GetReadOnlyPixelData ()[0], Is.EqualTo (rhs.GetReadOnlyPixelData ()[0]));
	}

	[Test]
	public void UnaryPixelOperationWorksWithNeutralSurface ()
	{
		using var source = new ArrayImageSurface (1, 1);
		using var destination = new ArrayImageSurface (1, 1);
		source.GetPixelData ()[0] = ColorBgra.FromBgra (10, 20, 30, 255);

		new UnaryPixelOps.Invert ().Apply (
			destination,
			source,
			[new RectangleI (PointI.Zero, new Size (1, 1))]);

		Assert.That (destination.GetReadOnlyPixelData ()[0].R, Is.EqualTo (225));
		Assert.That (destination.GetReadOnlyPixelData ()[0].G, Is.EqualTo (235));
		Assert.That (destination.GetReadOnlyPixelData ()[0].B, Is.EqualTo (245));
	}
}
