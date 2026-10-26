using System.Collections.Generic;
using System.IO;

namespace Pinta.Core.Neutral;

/// <summary>
/// Platform-neutral palette decoder contract. Implementations must not require
/// GTK, Cairo, or a platform file abstraction; callers provide an input stream.
/// </summary>
public interface IPaletteColorStreamLoader
{
	IReadOnlyList<ColorBgra> LoadColors (Stream stream);
}

/// <summary>Platform-neutral palette encoder contract.</summary>
public interface IPaletteColorStreamSaver
{
	void SaveColors (IReadOnlyList<ColorBgra> colors, Stream stream);
}
