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
		ArgumentNullException.ThrowIfNull (options);
		using FileChooserNative dialog = FileChooserNative.New (
			options.Title,
			parent,
			FileChooserAction.Save,
			"Save",
			"Cancel");

		foreach (var filter in options.Filters) {
			FileFilter gtkFilter = FileFilter.New ();
			gtkFilter.Name = filter.Name;
			foreach (string extension in filter.Extensions)
				gtkFilter.AddPattern (extension.StartsWith (".", StringComparison.Ordinal) ? $"*{extension}" : $"*.{extension}");
			dialog.AddFilter (gtkFilter);
		}

		if (options.InitialDirectory is PathFileReference initialDirectory)
			dialog.SetCurrentFolder (Gio.FileHelper.NewForPath (initialDirectory.Path));
		if (!string.IsNullOrWhiteSpace (options.SuggestedName))
			dialog.SetCurrentName (options.SuggestedName);

		var result = new TaskCompletionSource<IFileReference?> (TaskCreationOptions.RunContinuationsAsynchronously);
		dialog.OnResponse += (_, args) => {
			if ((ResponseType) args.ResponseId == ResponseType.Accept) {
				Gio.File? file = dialog.GetFile ();
				result.TrySetResult (file is null ? null : new PathFileReference (file.GetParseName ()));
			} else {
				result.TrySetResult (null);
			}
		};
		dialog.Show ();
		return await result.Task;
	}
}
