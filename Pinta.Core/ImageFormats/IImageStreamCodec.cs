using System.IO;

namespace Pinta.Core;

/// <summary>
/// Neutral image codec boundary for stream-based import and export.
/// Implementations must not require a file dialog or a platform file handle.
/// </summary>
public interface IImageStreamCodec
{
	/// <summary>Decodes an image stream into the engine surface abstraction.</summary>
	IImageSurface Decode (Stream source);

	/// <summary>Encodes an engine surface into the destination stream.</summary>
	void Encode (IImageSurface source, Stream destination);
}
