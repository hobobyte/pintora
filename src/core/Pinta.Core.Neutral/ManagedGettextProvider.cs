using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Pinta.Core;

/// <summary>
/// Reads gettext .mo catalogs (<c>locale/&lt;lang&gt;/LC_MESSAGES/pinta.mo</c>) without any native dependency.
/// Plural forms and message contexts are not supported; only the first (singular) form is used.
/// </summary>
public sealed class ManagedGettextProvider : ITranslationProvider
{
	private const uint MagicLittleEndian = 0x950412de;
	private const uint MagicBigEndian = 0xde120495;
	private const string CatalogName = "pinta.mo";

	private IReadOnlyDictionary<string, string> catalog = new Dictionary<string, string> ();

	public void Initialize (string localeDir, string languagePreference)
	{
		catalog = new Dictionary<string, string> ();

		foreach (string language in GetCandidateLanguages (languagePreference)) {
			string path = Path.Combine (localeDir, language, "LC_MESSAGES", CatalogName);
			if (!File.Exists (path))
				continue;

			try {
				catalog = ParseMo (File.ReadAllBytes (path));
			} catch (IOException) {
				continue;
			} catch (UnauthorizedAccessException) {
				continue;
			}
			return;
		}
	}

	public string GetString (string text)
		=> catalog.TryGetValue (text, out string? translated) ? translated : text;

	/// <summary>
	/// Language codes to try in order, e.g. "fr_CA" yields "fr_CA", "fr".
	/// A non-empty preference wins, then the LANGUAGE environment variable (colon separated),
	/// then the current UI culture.
	/// </summary>
	public static IEnumerable<string> GetCandidateLanguages (string languagePreference)
	{
		string? source = languagePreference;
		if (string.IsNullOrEmpty (source))
			source = Environment.GetEnvironmentVariable ("LANGUAGE");
		if (string.IsNullOrEmpty (source))
			source = CultureInfo.CurrentUICulture.Name.Replace ('-', '_');

		HashSet<string> seen = [];
		foreach (string entry in source.Split (':', StringSplitOptions.RemoveEmptyEntries)) {
			string code = entry;
			int cut = code.IndexOfAny (['.', '@']);
			if (cut >= 0)
				code = code[..cut];
			if (code.Length == 0)
				continue;

			if (seen.Add (code))
				yield return code;

			int underscore = code.IndexOf ('_');
			if (underscore > 0 && seen.Add (code[..underscore]))
				yield return code[..underscore];
		}
	}

	/// <summary>
	/// Parses a .mo file into an original -> translation map. Malformed data yields an empty map
	/// rather than throwing.
	/// </summary>
	public static IReadOnlyDictionary<string, string> ParseMo (ReadOnlySpan<byte> data)
	{
		Dictionary<string, string> result = [];

		if (data.Length < 20)
			return result;

		bool littleEndian;
		switch (ReadUInt32 (data, 0, littleEndian: true)) {
			case MagicLittleEndian:
				littleEndian = true;
				break;
			case MagicBigEndian:
				littleEndian = false;
				break;
			default:
				return result;
		}

		long count = ReadUInt32 (data, 8, littleEndian);
		long originalsOffset = ReadUInt32 (data, 12, littleEndian);
		long translationsOffset = ReadUInt32 (data, 16, littleEndian);

		if (count > data.Length / 8 || originalsOffset + count * 8 > data.Length || translationsOffset + count * 8 > data.Length)
			return result;

		for (int i = 0; i < count; i++) {
			if (!TryReadString (data, originalsOffset + i * 8, littleEndian, out string original)
				|| !TryReadString (data, translationsOffset + i * 8, littleEndian, out string translation))
				return new Dictionary<string, string> ();

			// The empty key is the catalog header; empty translations mean "untranslated".
			if (original.Length > 0 && translation.Length > 0)
				result[original] = translation;
		}

		return result;
	}

	private static uint ReadUInt32 (ReadOnlySpan<byte> data, long offset, bool littleEndian)
	{
		ReadOnlySpan<byte> bytes = data.Slice ((int) offset, 4);
		return littleEndian
			? (uint) (bytes[0] | bytes[1] << 8 | bytes[2] << 16 | bytes[3] << 24)
			: (uint) (bytes[3] | bytes[2] << 8 | bytes[1] << 16 | bytes[0] << 24);
	}

	// Table entry: uint32 length, uint32 offset. Strings are NUL terminated; anything after the
	// first NUL is a plural form and ignored.
	private static bool TryReadString (ReadOnlySpan<byte> data, long entryOffset, bool littleEndian, out string value)
	{
		value = "";
		long length = ReadUInt32 (data, entryOffset, littleEndian);
		long offset = ReadUInt32 (data, entryOffset + 4, littleEndian);
		if (offset + length > data.Length)
			return false;

		ReadOnlySpan<byte> raw = data.Slice ((int) offset, (int) length);
		int nul = raw.IndexOf ((byte) 0);
		if (nul >= 0)
			raw = raw[..nul];

		value = Encoding.UTF8.GetString (raw);
		return true;
	}
}
