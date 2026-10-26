using System.Collections.Immutable;

namespace Pinta.Core;

/// <summary>Platform-neutral description of a palette format.</summary>
public interface IPaletteFormatDescriptor
{
	ImmutableArray<string> Extensions { get; }
	string DisplayName { get; }
	ImmutableArray<string> Patterns { get; }
	IPaletteStreamLoader? StreamLoader { get; }
	IPaletteStreamSaver? StreamSaver { get; }
}
