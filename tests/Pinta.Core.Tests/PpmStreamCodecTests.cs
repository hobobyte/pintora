using System.IO;
using System.Text;
using NUnit.Framework;

namespace Pinta.Core.Tests;

[TestFixture]
internal sealed class PpmStreamCodecTests
{
	[Test]
	public void RoundTripsP3StreamWithoutPlatformFileApis ()
	{
		var codec = new PpmStreamCodec ();
		using var input = new MemoryStream (Encoding.ASCII.GetBytes ("P3\n2 1\n255\n255 0 0\n0 255 0\n"));
		using IImageSurface decoded = codec.Decode (input);
		Assert.That (decoded.Width, Is.EqualTo (2));
		Assert.That (decoded.GetReadOnlyPixelData ()[0], Is.EqualTo (ColorBgra.Red));

		using var output = new MemoryStream ();
		codec.Encode (decoded, output);
		Assert.That (Encoding.ASCII.GetString (output.ToArray ()), Does.StartWith ("P3\n2 1\n255\n"));
	}
}
