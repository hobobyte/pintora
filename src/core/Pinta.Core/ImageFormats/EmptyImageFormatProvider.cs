using System.Collections.Generic;

namespace Pinta.Core;

/// <summary>Neutral image provider used when a platform has not installed a codec backend.</summary>
public sealed class EmptyImageFormatProvider : IImageFormatProvider
{
	public IEnumerable<FormatDescriptor> GetFormats ()
	{
		yield break;
	}
}
