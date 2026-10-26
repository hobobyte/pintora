using System;
using System.Collections.Generic;
using System.Linq;

namespace Pintora.Avalonia.Editor.Hosting;

/// <summary>In-memory cursor catalog suitable for platform or plugin-provided cursor bitmaps.</summary>
public sealed class DictionaryCursorProvider : ICursorProvider
{
	private readonly IReadOnlyDictionary<string, CursorSourceData> cursors;

	public DictionaryCursorProvider (IEnumerable<CursorSourceData> sources)
	{
		if (sources is null)
			throw new ArgumentNullException (nameof (sources));
		cursors = sources.ToDictionary (source => source.CursorId, StringComparer.Ordinal);
	}

	public IReadOnlyList<string> GetAvailableCursors ()
		=> cursors.Keys.OrderBy (id => id, StringComparer.Ordinal).ToArray ();

	public CursorSourceData? GetCursor (string cursorId)
		=> cursors.TryGetValue (cursorId, out CursorSourceData? cursor) ? cursor : null;
}
