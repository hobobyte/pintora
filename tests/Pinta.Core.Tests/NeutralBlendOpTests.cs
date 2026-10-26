using System;
using NUnit.Framework;

namespace Pinta.Core.Tests;

[TestFixture]
internal sealed class NeutralBlendOpTests
{
	[Test]
	public void BinaryBlendOperationWorksWithNeutralSurface ()
	{
		using var destination = new MemorySurface (1, 1);
		using var lhs = new MemorySurface (1, 1);
		using var rhs = new MemorySurface (1, 1);
		lhs.Pixels[0] = ColorBgra.FromBgra (10, 20, 30, 255);
		rhs.Pixels[0] = ColorBgra.FromBgra (20, 30, 40, 255);

		new UserBlendOps.NormalBlendOp ().Apply (destination, lhs, rhs);

		Assert.That (destination.Pixels[0], Is.EqualTo (rhs.Pixels[0]));
	}

	[Test]
	public void UnaryPixelOperationWorksWithNeutralSurface ()
	{
		using var source = new MemorySurface (1, 1);
		using var destination = new MemorySurface (1, 1);
		source.Pixels[0] = ColorBgra.FromBgra (10, 20, 30, 255);

		new UnaryPixelOps.Invert ().Apply (
			destination,
			source,
			[new RectangleI (PointI.Zero, new Size (1, 1))]);

		Assert.That (destination.Pixels[0].R, Is.EqualTo (225));
		Assert.That (destination.Pixels[0].G, Is.EqualTo (235));
		Assert.That (destination.Pixels[0].B, Is.EqualTo (245));
	}

	private sealed class MemorySurface : IImageSurface
	{
		public MemorySurface (int width, int height)
		{
			Width = width;
			Height = height;
			Pixels = new ColorBgra[width * height];
		}

		public int Width { get; }
		public int Height { get; }
		public ColorBgra[] Pixels { get; }
		public void Clear () => Array.Clear (Pixels);
		public ReadOnlySpan<ColorBgra> GetReadOnlyPixelData () => Pixels;
		public Span<ColorBgra> GetPixelData () => Pixels;
		public void Dispose () { }
	}
}
