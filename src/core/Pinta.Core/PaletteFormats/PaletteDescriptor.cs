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

	public PaletteDescriptor (string displayPrefix, IEnumerable<string> extensions, IPaletteLoader loader, IPaletteSaver saver)
	{
		Extensions = [.. extensions];
		Patterns = [.. Extensions.Select (ext => $"*.{ext}")];
		DisplayName = $"{displayPrefix} palette ({string.Join (", ", Patterns)})";
		Loader = loader;
		Saver = saver;
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
