using System.IO;

namespace Pinta.Core;

/// <summary>
/// Platform-neutral reference to a file used by the document and codec layers.
/// </summary>
public interface IFileReference
{
	string DisplayName { get; }
	Stream OpenRead ();
	Stream OpenWrite ();
}
