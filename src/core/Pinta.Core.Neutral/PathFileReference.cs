using System;
using System.IO;

namespace Pinta.Core;

/// <summary>File reference backed by a local filesystem path.</summary>
public sealed class PathFileReference : IFileReference
{
	private readonly string path;

	public PathFileReference (string path)
	{
		this.path = !string.IsNullOrWhiteSpace (path)
			? Path.GetFullPath (path)
			: throw new ArgumentException ("A file path is required.", nameof (path));
	}

	public string DisplayName => Path.GetFileName (path);

	public Stream OpenRead () => File.Open (path, FileMode.Open, FileAccess.Read, FileShare.Read);

	public Stream OpenWrite () => File.Open (path, FileMode.Create, FileAccess.Write, FileShare.None);
}
