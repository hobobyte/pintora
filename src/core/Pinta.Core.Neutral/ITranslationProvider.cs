namespace Pinta.Core;

/// <summary>Platform boundary for looking up translated UI strings.</summary>
public interface ITranslationProvider
{
	/// <param name="localeDir">Folder containing &lt;lang&gt;/LC_MESSAGES/pinta.mo files.</param>
	/// <param name="languagePreference">User language override (e.g. "fr", "en_CA"), or empty to follow the system.</param>
	void Initialize (string localeDir, string languagePreference);

	/// <summary>Returns the translation of <paramref name="text"/>, or the text itself if there is none.</summary>
	string GetString (string text);
}

/// <summary>Returns strings unchanged. Used until a platform provider is registered.</summary>
public sealed class IdentityTranslationProvider : ITranslationProvider
{
	public void Initialize (string localeDir, string languagePreference)
	{
	}

	public string GetString (string text) => text;
}
