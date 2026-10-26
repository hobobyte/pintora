//
// PaletteDescriptor.cs
//
// Author:
//       Matthias Mailänder
//
// Copyright (c) 2017 Matthias Mailänder
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Gtk;

namespace Pinta.Core;

public sealed class PaletteDescriptor : IPaletteFormatDescriptor
{
	public ImmutableArray<string> Extensions { get; }
	public string DisplayName { get; }
	public ImmutableArray<string> Patterns { get; }

	public IPaletteLoader Loader { get; }

	public IPaletteSaver Saver { get; }
	public IPaletteStreamLoader? StreamLoader => Loader as IPaletteStreamLoader;
	public IPaletteStreamSaver? StreamSaver => Saver as IPaletteStreamSaver;

	public FileFilter? Filter => filter;
	private FileFilter? filter;
	private readonly string display_prefix;

	public PaletteDescriptor (string displayPrefix, IEnumerable<string> extensions, IPaletteLoader loader, IPaletteSaver saver, bool createGtkFilter = true)
	{
		display_prefix = displayPrefix;
		Extensions = [.. extensions];
		Patterns = [.. Extensions.Select (ext => $"*.{ext}")];
		DisplayName = $"{displayPrefix} palette ({string.Join (", ", Patterns)})";
		Loader = loader;
		Saver = saver;

		if (createGtkFilter)
			filter = CreateGtkFilter (displayPrefix);
	}

	public FileFilter GetGtkFilter ()
	{
		return filter ??= CreateGtkFilter (display_prefix);
	}

	private FileFilter CreateGtkFilter (string displayPrefix)
	{
		var ff = FileFilter.New ();
		StringBuilder formatNames = new StringBuilder ();

		foreach (string wildcard in Patterns) {
			if (formatNames.Length > 0)
				formatNames.Append (", ");

			ff.AddPattern (wildcard);
			formatNames.Append (wildcard);
		}

		// Translators: {0} is the palette format (e.g. "GIMP") and {1} is a list of file extensions.
		ff.Name = Translations.GetString ("{0} palette ({1})", displayPrefix, formatNames);
		return ff;
	}

	public bool IsReadOnly ()
	{
		return Saver == null;
	}

	public bool IsWriteOnly ()
	{
		return Loader == null;
	}
}
