using System.Collections.Generic;
using Pinta.Core;

namespace Pinta.Application;

/// <summary>Default provider for frontends that supply their own codecs.</summary>
public sealed class NeutralImageFormatProvider : IImageFormatProvider
{
	public IEnumerable<FormatDescriptor> GetFormats ()
	{
		yield break;
	}
}
