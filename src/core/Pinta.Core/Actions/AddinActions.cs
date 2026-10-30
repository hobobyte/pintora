//
// AddinActions.cs
//
// Author:
//       Cameron White <cameronwhite91@gmail.com>
//
// Copyright (c) 2012 Cameron White
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

public sealed class AddinActions
{
<<<<<<< HEAD
	private IActionMenu addins_menu = null!; // NRT - Set by RegisterActions
=======
	private Gio.Menu addins_menu = null!; // NRT - Set by RegisterActions
>>>>>>> origin/master

	public Command AddinManager { get; }

	public AddinActions ()
	{
		AddinManager = new Command (
			"AddinManager",
			Translations.GetString ("Add-in Manager..."),
			null,
			Resources.Icons.AddinsManage);
	}

	/// <summary>
	/// Adds a new item to the Add-ins menu.
	/// </summary>
<<<<<<< HEAD
	public void AddMenuItem (CommandMenuItem item)
=======
	public void AddMenuItem (Gio.MenuItem item)
>>>>>>> origin/master
	{
		addins_menu.AppendItem (item);
	}

	/// <summary>
	/// Removes an item from the Add-ins menu.
	/// </summary>
<<<<<<< HEAD
	public void RemoveMenuItem (CommandMenuItem item)
	{
		// TODO-GTK3 (addins)
		throw new NotImplementedException ();
	}

	#region Initialization
	public void RegisterActions (IActionRegistrar registrar, IActionMenu menu)
	{
		registrar.AddCommand (AddinManager);
		menu.AppendItem (AddinManager);

		addins_menu = menu.CreateSubmenu ();
=======
	public void RemoveMenuItem (Gio.MenuItem item)
	{
		// TODO-GTK3 (addins)
		throw new NotImplementedException ();
#if false
		addins_menu.Remove (item);
#endif
	}

	#region Initialization
	public void RegisterActions (IActionRegistrar registrar, Gio.Menu menu)
	{
		registrar.AddCommand (AddinManager);
		menu.AppendItem (AddinManager.CreateMenuItem ());

		addins_menu = Gio.Menu.New ();
>>>>>>> origin/master
		menu.AppendSection (null, addins_menu);
	}
	#endregion
}

