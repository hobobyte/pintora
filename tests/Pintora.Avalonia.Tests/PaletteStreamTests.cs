using System.IO;
using System.Linq;
using Cairo;
using NUnit.Framework;
using Pinta.Core;
using Pinta.Core.Neutral;

namespace Pintora.Avalonia.Tests;

[TestFixture]
internal sealed class PaletteStreamTests
{
	private static readonly Color[] colors = [new Color (1, 0, 0), new Color (0, 0.5, 1, 0.75)];

	[Test]
	public void GimpPaletteRoundTripsThroughStream ()
	{
		var palette = new GimpPalette ();
		using var stream = new MemoryStream ();
		palette.Save (colors, stream);
		stream.Position = 0;
		Assert.That (palette.Load (stream), Has.Count.EqualTo (2));
		Assert.That (stream.CanRead, Is.True);
	}

	[Test]
	public void PaintDotNetPaletteRoundTripsThroughStream ()
	{
		var palette = new PaintDotNetPalette ();
		using var stream = new MemoryStream ();
		palette.Save (colors, stream);
		stream.Position = 0;
		Assert.That (palette.Load (stream), Has.Count.EqualTo (2));
		Assert.That (stream.CanRead, Is.True);
	}

	[Test]
	public void PaintShopProPaletteRoundTripsThroughStream ()
	{
		var palette = new PaintShopProPalette ();
		using var stream = new MemoryStream ();
		palette.Save (colors, stream);
		stream.Position = 0;
		Assert.That (palette.Load (stream), Has.Count.EqualTo (2));
		Assert.That (stream.CanRead, Is.True);
	}

	[Test]
	public void PaletteFormatManagerCanBeCreatedWithoutGtk ()
	{
		var manager = new PaletteFormatManager ();
		Assert.That (manager.Formats, Has.Count.EqualTo (3));
	}

	[Test]
	public void FormatsExposePlatformNeutralStreamContracts ()
	{
		Assert.That (new GimpPalette (), Is.AssignableTo<IPaletteStreamLoader> ());
		Assert.That (new PaintDotNetPalette (), Is.AssignableTo<IPaletteStreamSaver> ());
		Assert.That (new PaintShopProPalette (), Is.AssignableTo<IPaletteStreamLoader> ());
	}

	[Test]
	public void FormatsExposeNeutralColorStreamLoaders ()
	{
		foreach (IPaletteColorStreamLoader palette in new IPaletteColorStreamLoader[] { new GimpPalette (), new PaintDotNetPalette (), new PaintShopProPalette () }) {
			using var stream = new MemoryStream ();
			((IPaletteSaver) palette).Save (colors, stream);
			stream.Position = 0;
			Assert.That (palette.LoadColors (stream), Has.Count.EqualTo (2));
		}
	}

	[Test]
	public void FormatsExposeNeutralColorStreamSavers ()
	{
		ColorBgra[] neutralColors = [ColorBgra.Red, ColorBgra.FromBgra (255, 128, 0, 255)];
		foreach (IPaletteColorStreamSaver palette in new IPaletteColorStreamSaver[] { new GimpPalette (), new PaintDotNetPalette (), new PaintShopProPalette () }) {
			using var stream = new MemoryStream ();
			palette.SaveColors (neutralColors, stream);
			Assert.That (stream.Length, Is.GreaterThan (0));
		}
	}

	[Test]
	public void PaletteDescriptorExposesNeutralMetadata ()
	{
		var descriptor = new PaletteDescriptor ("GIMP", ["gpl"], new GimpPalette (), new GimpPalette ());
		IPaletteFormatDescriptor neutral = descriptor;

		Assert.That (neutral.DisplayName, Does.Contain ("GIMP"));
		Assert.That (neutral.Patterns, Has.Member ("*.gpl"));
		Assert.That (neutral.StreamLoader, Is.Not.Null);
		Assert.That (neutral.StreamSaver, Is.Not.Null);
	}

	[Test]
	public void PaletteFormatManagerExposesNeutralCatalog ()
	{
		var manager = new PaletteFormatManager ();

		Assert.That (manager.NeutralFormats, Has.Count.EqualTo (manager.Formats.Count));
		Assert.That (manager.NeutralFormats.All (format => format.StreamLoader is not null), Is.True);
	}

	[Test]
	public void PaletteFormatManagerImplementsNeutralCatalog ()
	{
		IPaletteFormatCatalog catalog = new PaletteFormatManager ();
		Assert.That (catalog.Formats, Has.Count.EqualTo (3));
	}

	[Test]
	public void ImageFormatMetadataIsIndependentFromGtkFilter ()
	{
		var metadata = new ImageFormatMetadata (["ppm"], ["image/x-portable-pixmap"], supportsLayers: false);
		Assert.That (metadata.Extensions, Has.Member ("ppm"));
		Assert.That (metadata.Mimes, Has.Member ("image/x-portable-pixmap"));
		Assert.That (metadata.SupportsLayers, Is.False);
	}

}
