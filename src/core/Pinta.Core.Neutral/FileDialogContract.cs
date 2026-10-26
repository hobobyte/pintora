using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pinta.Core;

public sealed record FileDialogFilter (string Name, IReadOnlyList<string> Extensions);

public sealed record FileDialogOptions (
	string Title,
	IReadOnlyList<FileDialogFilter> Filters,
	IFileReference? InitialDirectory = null,
	string? SuggestedName = null);

/// <summary>Platform-neutral file picker boundary.</summary>
public interface IFileDialogService
{
	Task<IFileReference?> OpenFileAsync (FileDialogOptions options);
	Task<IFileReference?> SaveFileAsync (FileDialogOptions options);
}
