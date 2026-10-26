// 
// RecentFileManager.cs
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

using System;
using System.IO;

namespace Pinta.Core;

public sealed class RecentFileManager
{
	public Action<IFileReference>? FileAdded { get; set; }
	private IFileReference? last_dialog_directory;

	public RecentFileManager ()
	{
		last_dialog_directory = DefaultDialogDirectory;
	}

	public IFileReference? LastDialogDirectory {
		get => last_dialog_directory;
		set {
			// The file chooser dialog may return null for the current folder in certain cases,
			// such as the Recently Used pane in the Gnome file chooser.
			if (value != null)
				last_dialog_directory = value;
		}
	}

	public IFileReference? DefaultDialogDirectory {
		get {
			string path = System.Environment.GetFolderPath (Environment.SpecialFolder.MyPictures);
			return !string.IsNullOrEmpty (path) && Directory.Exists (path) ? new PathFileReference (path) : null;
		}
	}

	/// <summary>
	/// Returns a directory for use in a dialog. The last dialog directory is
	/// returned if it exists, otherwise the default directory is used.
	/// </summary>
	public IFileReference? GetDialogDirectory ()
	{
		return last_dialog_directory is PathFileReference path && Directory.Exists (path.Path) ? path : DefaultDialogDirectory;
	}

	/// <summary>
	/// Add a file to the list of recently-used files.
	/// </summary>
	public void AddFile (IFileReference file) => FileAdded?.Invoke (file);
}
