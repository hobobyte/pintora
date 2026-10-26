using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Pinta.Core;

/// <summary>Neutral stream codec for text-based P3 portable pixmaps.</summary>
public sealed class PpmStreamCodec : IImageStreamCodec
{
	public IImageSurface Decode (Stream source)
	{
		using var reader = new StreamReader (source, Encoding.ASCII, leaveOpen: true);
		var tokens = Tokenize (reader).GetEnumerator ();
		string Next () => tokens.MoveNext () ? tokens.Current : throw new FormatException ("Unexpected end of PPM stream.");
		if (Next () != "P3") throw new FormatException ("Expected P3 PPM.");
		int width = ParsePositive (Next ());
		int height = ParsePositive (Next ());
		int max = ParsePositive (Next ());
		var surface = new ArrayImageSurface (width, height);
		Span<ColorBgra> pixels = surface.GetPixelData ();
		for (int i = 0; i < pixels.Length; i++) {
			byte r = Scale (Next (), max);
			byte g = Scale (Next (), max);
			byte b = Scale (Next (), max);
			pixels[i] = ColorBgra.FromBgra (b, g, r, byte.MaxValue);
		}
		return surface;
	}

	public void Encode (IImageSurface source, Stream destination)
	{
		using var writer = new StreamWriter (destination, Encoding.ASCII, leaveOpen: true) { NewLine = "\n" };
		writer.WriteLine ($"P3\n{source.Width} {source.Height}\n255");
		foreach (ColorBgra pixel in source.GetReadOnlyPixelData ())
			writer.WriteLine ($"{pixel.R} {pixel.G} {pixel.B}");
		writer.Flush ();
	}

	private static int ParsePositive (string value)
		=> int.TryParse (value, NumberStyles.None, CultureInfo.InvariantCulture, out int result) && result > 0
			? result : throw new FormatException ($"Invalid positive integer '{value}'.");

	private static byte Scale (string value, int max)
	{
		int number = int.Parse (value, CultureInfo.InvariantCulture);
		if (number < 0 || number > max) throw new FormatException ("PPM channel is outside range.");
		return (byte) Math.Clamp ((int) Math.Round (number * 255.0 / max), 0, 255);
	}

	private static IEnumerable<string> Tokenize (TextReader reader)
	{
		string? line;
		while ((line = reader.ReadLine ()) is not null) {
			int comment = line.IndexOf ('#');
			if (comment >= 0) line = line[..comment];
			foreach (string token in line.Split ((char[]?) null, StringSplitOptions.RemoveEmptyEntries))
				yield return token;
		}
	}
}
