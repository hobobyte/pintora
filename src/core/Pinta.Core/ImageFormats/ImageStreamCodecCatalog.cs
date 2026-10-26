using System;
using System.Collections.Generic;

namespace Pinta.Core;

/// <summary>Neutral registry for stream-based image codecs.</summary>
public sealed class ImageStreamCodecCatalog
{
	private readonly Dictionary<string, IImageStreamCodec> codecs = new (StringComparer.OrdinalIgnoreCase);

	public void Register (IEnumerable<string> extensions, IImageStreamCodec codec)
	{
		ArgumentNullException.ThrowIfNull (codec);
		foreach (string extension in extensions)
			codecs[Normalize (extension)] = codec;
	}

	public bool TryGet (string fileName, out IImageStreamCodec? codec)
	{
		string extension = System.IO.Path.GetExtension (fileName);
		return codecs.TryGetValue (Normalize (extension), out codec);
	}

	private static string Normalize (string extension)
		=> extension.Trim ().TrimStart ('.').ToLowerInvariant ();
}
