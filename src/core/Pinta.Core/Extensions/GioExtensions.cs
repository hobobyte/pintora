//
// GioExtensions.cs
//
// Author:
//       Jonathan Pobst <monkey@jpobst.com>
//
// Copyright (c) 2010 Jonathan Pobst
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

namespace Pinta.Core;

public static class GioMenuExtensions
{
	public static void Remove (this Gio.Menu menu, Command action)
	{
		for (int i = 0; i < menu.GetNItems (); ++i) {
			string name_attr = menu.GetItemAttributeValue (i, "action", GLib.VariantType.String)!.GetString (out nuint _);
			if (name_attr != action.FullName) continue;
			menu.Remove (i);
			return;
		}
	}

	public static void AppendMenuItemSorted (this Gio.Menu menu, Gio.MenuItem item)
	{
		string newLabel = item.GetAttributeValue ("label", GLib.VariantType.String)!.GetString (out nuint _);

		for (int i = 0; i < menu.GetNItems (); i++) {
			string label = menu.GetItemAttributeValue (i, "label", GLib.VariantType.String)!.GetString (out nuint _);
			if (string.Compare (label, newLabel) <= 0) continue;
			menu.InsertItem (i, item);
			return;
		}

		menu.AppendItem (item);
	}

	public static void RemoveMultiple (
		this Gio.ListStore store,
		uint position,
		uint n_removals)
	{
		store.Splice (position, n_removals, [], 0);
	}
}
