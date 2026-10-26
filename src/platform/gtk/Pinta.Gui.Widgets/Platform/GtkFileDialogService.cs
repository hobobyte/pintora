using System;
using System.IO;
using System.Threading.Tasks;
using Gtk;
using Pinta.Core;

namespace Pinta.Gui.Widgets;

/// <summary>GTK implementation of the neutral file dialog contract.</summary>
public sealed class GtkFileDialogService (Window parent) : IFileDialogService
{
	public async Task<IFileReference?> OpenFileAsync (FileDialogOptions options)
	{
		ArgumentNullException.ThrowIfNull (options);
		using FileDialog dialog = FileDialog.New ();
		dialog.SetTitle (options.Title);
		Gio.File? file = await dialog.OpenFileAsync (parent);
		return file is null ? null : new PathFileReference (file.GetParseName ());
	}

	public async Task<IFileReference?> SaveFileAsync (FileDialogOptions options)
	{
		await Task.Yield ();
		throw new NotSupportedException ("The installed GTK binding does not expose Gtk.FileDialog save yet.");
	}
}
