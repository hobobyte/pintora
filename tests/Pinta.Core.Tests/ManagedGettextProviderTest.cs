using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Pinta.Core.Tests;

[TestFixture]
internal sealed class ManagedGettextProviderTest
{
	// Builds a minimal .mo file (no hash table) with the requested byte order.
	private static byte[] BuildMo (bool littleEndian, params (string original, string translation)[] entries)
	{
		List<byte> data = [];
		void Write (uint value)
		{
			byte[] bytes = BitConverter.GetBytes (value);
			if (BitConverter.IsLittleEndian != littleEndian)
				Array.Reverse (bytes);
			data.AddRange (bytes);
		}

		int count = entries.Length;
		int originalsTable = 28;
		int translationsTable = originalsTable + count * 8;
		int stringsStart = translationsTable + count * 8;

		List<byte[]> strings = [];
		foreach (var (original, translation) in entries) {
			strings.Add (Encoding.UTF8.GetBytes (original));
			strings.Add (Encoding.UTF8.GetBytes (translation));
		}

		Write (0x950412de);
		Write (0);
		Write ((uint) count);
		Write ((uint) originalsTable);
		Write ((uint) translationsTable);
		Write (0);
		Write ((uint) stringsStart);

		int offset = stringsStart;
		foreach (byte[] s in strings) {
			Write ((uint) s.Length);
			Write ((uint) offset);
			offset += s.Length + 1;
		}

		foreach (byte[] s in strings) {
			data.AddRange (s);
			data.Add (0);
		}

		// Tables were interleaved (originals then translations) in the loop above; reorder them.
		byte[] result = data.ToArray ();
		return Reorder (result, count, originalsTable, translationsTable);
	}

	// The table loop above wrote entries as original, translation, original, ...; rearrange them
	// into the two contiguous tables the format requires.
	private static byte[] Reorder (byte[] file, int count, int originalsTable, int translationsTable)
	{
		byte[] result = (byte[]) file.Clone ();
		for (int i = 0; i < count; i++) {
			Array.Copy (file, originalsTable + i * 16, result, originalsTable + i * 8, 8);
			Array.Copy (file, originalsTable + i * 16 + 8, result, translationsTable + i * 8, 8);
		}
		return result;
	}

	[TestCase (true)]
	[TestCase (false)]
	public void ParseMo_ReadsEntriesInBothByteOrders (bool littleEndian)
	{
		byte[] mo = BuildMo (littleEndian, ("Color", "Couleur"), ("Open", "Ouvrir"));

		var catalog = ManagedGettextProvider.ParseMo (mo);

		Assert.That (catalog["Color"], Is.EqualTo ("Couleur"));
		Assert.That (catalog["Open"], Is.EqualTo ("Ouvrir"));
	}

	[Test]
	public void ParseMo_SkipsHeaderAndUntranslatedEntries ()
	{
		byte[] mo = BuildMo (true, ("", "Language: fr\n"), ("Save", ""), ("Close", "Fermer"));

		var catalog = ManagedGettextProvider.ParseMo (mo);

		Assert.That (catalog.Keys, Is.EquivalentTo (new[] { "Close" }));
	}

	[Test]
	public void ParseMo_UsesFirstPluralFormOnly ()
	{
		byte[] mo = BuildMo (true, ("File\0Files", "Fichier\0Fichiers"));

		var catalog = ManagedGettextProvider.ParseMo (mo);

		Assert.That (catalog["File"], Is.EqualTo ("Fichier"));
	}

	[Test]
	public void ParseMo_MalformedDataYieldsEmptyCatalog ()
	{
		byte[] valid = BuildMo (true, ("Color", "Couleur"));

		Assert.That (ManagedGettextProvider.ParseMo ([]), Is.Empty);
		Assert.That (ManagedGettextProvider.ParseMo (new byte[32]), Is.Empty);
		Assert.That (ManagedGettextProvider.ParseMo (valid[..(valid.Length - 6)]), Is.Empty);
	}

	[Test]
	public void GetCandidateLanguages_FallsBackFromRegionToLanguage ()
	{
		Assert.That (
			ManagedGettextProvider.GetCandidateLanguages ("fr_CA.UTF-8:de").ToArray (),
			Is.EqualTo (new[] { "fr_CA", "fr", "de" }));
	}

	[Test]
	public void Provider_LoadsCatalogForPreferredLanguageAndFallsBackToOriginalText ()
	{
		string dir = Path.Combine (Path.GetTempPath (), "pinta-mo-" + Guid.NewGuid ().ToString ("N"));
		string langDir = Path.Combine (dir, "fr", "LC_MESSAGES");
		Directory.CreateDirectory (langDir);
		try {
			File.WriteAllBytes (Path.Combine (langDir, "pinta.mo"), BuildMo (true, ("Color", "Couleur")));

			ManagedGettextProvider provider = new ();
			provider.Initialize (dir, "fr_CA");

			Assert.That (provider.GetString ("Color"), Is.EqualTo ("Couleur"));
			Assert.That (provider.GetString ("Unknown"), Is.EqualTo ("Unknown"));
		} finally {
			Directory.Delete (dir, recursive: true);
		}
	}

	[Test]
	public void Provider_MissingLocaleDirReturnsOriginalText ()
	{
		ManagedGettextProvider provider = new ();
		provider.Initialize ("/nonexistent/locale", "fr");

		Assert.That (provider.GetString ("Color"), Is.EqualTo ("Color"));
	}
}
