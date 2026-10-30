using System.Linq;
using NUnit.Framework;
using Pinta.Resources;

namespace Pintora.Avalonia.Tests;

[TestFixture]
internal sealed class EmbeddedResourceReaderTests
{
	[Test]
	public void ReadsEmbeddedResourceBytesWithoutGtk ()
	{
<<<<<<< HEAD
		var assembly = typeof (Styles).Assembly;
=======
		var assembly = typeof (ResourceLoader).Assembly;
>>>>>>> origin/master
		string resourceName = assembly.GetManifestResourceNames ().First (name => name.EndsWith ("Cursor.Paintbrush.png", System.StringComparison.Ordinal));

		byte[]? bytes = EmbeddedResourceReader.Read (assembly, resourceName);

		Assert.That (bytes, Is.Not.Null.And.Not.Empty);
	}

	[Test]
	public void MissingEmbeddedResourceReturnsNull ()
	{
<<<<<<< HEAD
		byte[]? bytes = EmbeddedResourceReader.Read (typeof (Styles).Assembly, "missing-resource");
=======
		byte[]? bytes = EmbeddedResourceReader.Read (typeof (ResourceLoader).Assembly, "missing-resource");
>>>>>>> origin/master

		Assert.That (bytes, Is.Null);
	}

	[Test]
	public void ReflectionReaderImplementsNeutralContract ()
	{
		IEmbeddedResourceReader reader = new ReflectionEmbeddedResourceReader ();
<<<<<<< HEAD
		Assert.That (reader.Read (typeof (Styles).Assembly, "missing-resource"), Is.Null);
=======
		Assert.That (reader.Read (typeof (ResourceLoader).Assembly, "missing-resource"), Is.Null);
>>>>>>> origin/master
	}
}
