using System.Collections.Generic;
using Avalonia.Media.Imaging;

namespace Pintora.Avalonia.Editor.Hosting;

public sealed record CursorSourceData (string CursorId, Bitmap Bitmap, int HotspotX, int HotspotY);

public interface ICursorProvider
{
	IReadOnlyList<string> GetAvailableCursors ();
	CursorSourceData? GetCursor (string cursorId);
}
