//
// GtkDashPatternBox.cs
//
// Author:
//       Andrew Davis <andrew.3.1415@gmail.com>
//
// Copyright (c) 2013 Andrew Davis, GSoC 2013
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

using System;

namespace Pinta.Core;

/// <summary>GTK implementation of <see cref="IDashPatternBox"/>.</summary>
public sealed class GtkDashPatternBox : IDashPatternBox
{
	private readonly Gtk.Box container = Gtk.Box.New (Gtk.Orientation.Horizontal, 0);
	private readonly ToolBarComboBox combo_box;

	public GtkDashPatternBox ()
	{
		container.Append (GtkExtensions.CreateToolBarSeparator ());
		container.Append (Gtk.Label.New ($" {Translations.GetString ("Dash")}: "));

		combo_box = ToolBarComboBox.New (50, 0, true,
				"-", " -", " --", " ---", "  -", "   -", " - --", " - - --------", " - - ---- - ----");
		combo_box.ComboBox.OnChanged += (_, _) => PatternChanged?.Invoke (this, EventArgs.Empty);
		container.Append (combo_box);
	}

	public string Pattern {
		get => combo_box.ComboBox.GetEntry ().GetText ();
		set => combo_box.ComboBox.GetEntry ().SetText (value);
	}

	public event EventHandler? PatternChanged;

	public object NativeWidget => container;

	public bool Visible {
		get => container.Visible;
		set => container.Visible = value;
	}
}
