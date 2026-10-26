using System.Collections.Generic;

namespace Pinta.Core;

/// <summary>Supplies platform-specific image format descriptors to the core manager.</summary>
public interface IImageFormatProvider
{
	IEnumerable<FormatDescriptor> GetFormats ();
}
