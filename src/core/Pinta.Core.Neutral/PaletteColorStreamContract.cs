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
