using NUnit.Framework;

namespace Pinta.Core.Tests;

[TestFixture]
internal sealed class ImageStreamCodecCatalogTests
{
	[Test]
	public void ResolvesCodecByFileExtension ()
	{
		var catalog = new ImageStreamCodecCatalog ();
		var codec = new PpmStreamCodec ();
		catalog.Register (["ppm", ".PPM"], codec);

		Assert.That (catalog.TryGet ("image.ppm", out IImageStreamCodec? resolved), Is.True);
		Assert.That (resolved, Is.SameAs (codec));
		Assert.That (catalog.TryGet ("image.png", out _), Is.False);
	}
}
