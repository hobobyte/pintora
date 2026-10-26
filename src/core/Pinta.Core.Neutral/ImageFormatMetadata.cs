using System.Collections.Immutable;
using System.Collections.Generic;

namespace Pinta.Core;

/// <summary>Platform-neutral metadata describing an image format.</summary>
public sealed class ImageFormatMetadata
{
	public ImageFormatMetadata (IEnumerable<string> extensions, IEnumerable<string> mimes, bool supportsLayers)
	{
		Extensions = [.. extensions];
		Mimes = [.. mimes];
		SupportsLayers = supportsLayers;
	}

	public ImmutableArray<string> Extensions { get; }
	public ImmutableArray<string> Mimes { get; }
	public bool SupportsLayers { get; }
}
