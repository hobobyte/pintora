using System;
using System.IO;
using NUnit.Framework;
using Pinta.Graphics.Skia;

namespace Pintora.Avalonia.Tests;

[TestFixture]
public sealed class SkiaImageCodecTests
{
	private static readonly byte[] OnePixelPng = Convert.FromBase64String (
		"iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADElEQVR42mNk+M/wHwAF/gL+N9yV3QAAAABJRU5ErkJggg==");

	[Test]
	public void DecodeAndEncodeRoundTrip ()
	{
		var codec = new SkiaImageCodec ();
		using var source = new MemoryStream (OnePixelPng, writable: false);
		using Pinta.Core.IImageSurface surface = codec.Decode (source);

		Assert.That (surface.Width, Is.EqualTo (1));
		Assert.That (surface.Height, Is.EqualTo (1));

		using var encoded = new MemoryStream ();
		codec.Encode (surface, encoded);
		Assert.That (encoded.Length, Is.GreaterThan (0));
	}

	[Test]
	public void SurfaceFactoryCreatesNeutralSurface ()
	{
		var factory = new SkiaImageSurfaceFactory ();
		using Pinta.Core.IImageSurface surface = factory.Create (7, 5);

		Assert.That (surface.Width, Is.EqualTo (7));
		Assert.That (surface.Height, Is.EqualTo (5));
	}

	[Test]
	public void CodecExposesNeutralFileReferenceBoundary ()
	{
		Assert.That (new SkiaImageCodec (), Is.AssignableTo<Pinta.Core.IImageFileCodec> ());
	}

	[Test]
	public void PathFileReferenceProvidesNeutralFileAccess ()
	{
		string path = Path.Combine (Path.GetTempPath (), $"pintora-{Guid.NewGuid ():N}.tmp");
		try {
			var reference = new Pinta.Core.PathFileReference (path);
			using (Stream output = reference.OpenWrite ())
				output.WriteByte (42);
			using Stream input = reference.OpenRead ();
			Assert.That (input.ReadByte (), Is.EqualTo (42));
			Assert.That (reference.DisplayName, Does.EndWith (".tmp"));
		} finally {
			if (File.Exists (path)) File.Delete (path);
		}
	}
}
