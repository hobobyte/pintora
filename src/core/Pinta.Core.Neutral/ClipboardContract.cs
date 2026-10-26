using System.Threading.Tasks;

namespace Pinta.Core;

/// <summary>Neutral image payload exchanged with a platform clipboard.</summary>
public sealed record ClipboardImageData (
	IImageSurface Surface,
	PointI SourcePosition,
	byte[]? SelectionMask = null);

/// <summary>Platform boundary for clipboard image exchange.</summary>
public interface IClipboardService
{
	Task<bool> ContainsImageAsync ();
	Task<ClipboardImageData?> ReadImageAsync ();
	void WriteImage (ClipboardImageData image);
	void SetText (string text);
	Task<string?> ReadTextAsync ();
}
