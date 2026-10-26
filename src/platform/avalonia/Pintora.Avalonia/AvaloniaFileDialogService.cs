using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Pinta.Core;

namespace Pintora.Avalonia;

/// <summary>Avalonia file picker adapter for the platform-neutral core contract.</summary>
public sealed class AvaloniaFileDialogService (Window owner) : IFileDialogService
{
	public async Task<IFileReference?> OpenFileAsync (FileDialogOptions options)
	{
		ArgumentNullException.ThrowIfNull (options);
		if (owner.StorageProvider is null)
			return null;

		var files = await owner.StorageProvider.OpenFilePickerAsync (new FilePickerOpenOptions {
			Title = options.Title,
			AllowMultiple = false,
			FileTypeFilter = CreateFilters (options),
		});
		return files.Count == 0 ? null : ToFileReference (files[0]);
	}

	public async Task<IFileReference?> SaveFileAsync (FileDialogOptions options)
	{
		ArgumentNullException.ThrowIfNull (options);
		if (owner.StorageProvider is null)
			return null;

		var file = await owner.StorageProvider.SaveFilePickerAsync (new FilePickerSaveOptions {
			Title = options.Title,
			SuggestedFileName = options.SuggestedName,
			FileTypeChoices = CreateFilters (options),
		});
		return file is null ? null : ToFileReference (file);
	}

	private static FilePickerFileType[] CreateFilters (FileDialogOptions options) => options.Filters
		.Select (filter => new FilePickerFileType (filter.Name) {
			Patterns = filter.Extensions.Select (extension => extension.StartsWith (".", StringComparison.Ordinal) ? $"*{extension}" : $"*.{extension}").ToArray (),
		})
		.ToArray ();

	private static IFileReference? ToFileReference (IStorageFile file)
	{
		string? path = file.TryGetLocalPath ();
		return string.IsNullOrWhiteSpace (path) ? null : new PathFileReference (path);
	}
}
