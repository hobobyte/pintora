using System;
using System.Linq;
using NUnit.Framework;

namespace Pinta.Core.Tests;

[TestFixture]
[NonParallelizable] // Run in isolation since this test modifies the environment.
internal sealed class TranslationsTest
{
	[OneTimeSetUp]
	public void Setup ()
	{
		// Set to some language other than English to test translations.
		Environment.SetEnvironmentVariable ("LANGUAGE", "fr_FR");

		// The test runs from a path like Pinta/tests/Pinta.Core.Tests/bin/Debug/net10.0,
		// so we need the relative path to the translation folder (src/platform/gtk/build/bin/locale).
		string localeDir = "../../../../../src/platform/gtk/build/bin/locale";
		Translations.SetProvider (new ManagedGettextProvider ());
		Translations.Init (localeDir, langPref: string.Empty);
	}

	[OneTimeTearDown]
	public void TearDown ()
	{
		Environment.SetEnvironmentVariable ("LANGUAGE", null);
		Translations.SetProvider (new IdentityTranslationProvider ());
	}

	[Test]
	[Description ("Test that a string can be translated. If this test fails, make sure you built with -p:BuildTranslations=true.")]
	public void SimpleTranslation ()
	{
		if (!Translations.GetAvailableLanguages ().Contains ("fr"))
			Assert.Ignore ("No compiled translations found; build with -p:BuildTranslations=true.");

		Assert.That (Translations.GetString ("Color"), Is.EqualTo ("Couleur"));
	}
}
