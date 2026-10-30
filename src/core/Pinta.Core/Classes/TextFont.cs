using System;

namespace Pinta.Core;

/// <summary>Font variant for text rendering (mirrors Pango variant values).</summary>
public enum TextFontVariant {
	Normal = 0,
	SmallCaps = 1,
	AllSmallCaps = 2,
	PetiteCaps = 3,
	AllPetiteCaps = 4,
	Unicase = 5,
	TitleCaps = 6,
}

/// <summary>Font weight for text rendering (mirrors Pango weight values).</summary>
public enum TextFontWeight {
	Thin = 100,
	Ultralight = 200,
	Light = 300,
	Semilight = 350,
	Book = 380,
	Normal = 400,
	Medium = 500,
	Semibold = 600,
	Bold = 700,
	Ultrabold = 800,
	Heavy = 900,
	Ultraheavy = 1000,
}

/// <summary>
/// Platform-neutral font description for the text engine.
/// Size is expressed in Pango units (points * 1024) so existing unit conversions stay valid.
/// </summary>
public sealed record TextFont (
	string Family,
	int SizePangoUnits,
	TextFontVariant Variant,
	TextFontWeight Weight,
	bool Italic)
{
	/// <summary>Default font matching a blank Pango font description.</summary>
	public static TextFont Empty { get; } = new (string.Empty, 0, TextFontVariant.Normal, TextFontWeight.Normal, false);

	/// <summary>Produces a Pango-style font string (e.g. "Sans Bold Italic 12") for settings persistence.</summary>
	public override string ToString ()
	{
		var result = Family;
		if (Italic) result += " Italic";
		if (Variant != TextFontVariant.Normal) result += $" {Variant}";
		if (Weight != TextFontWeight.Normal) result += $" {Weight}";
		if (SizePangoUnits > 0) result += $" {SizePangoUnits / 1024.0}";
		return result;
	}
}
