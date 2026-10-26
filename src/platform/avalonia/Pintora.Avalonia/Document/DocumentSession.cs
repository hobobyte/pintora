using System;
using System.IO;
using Pinta.Core;

namespace Pintora.Avalonia.Document;

/// <summary>
/// Per-document aggregate for the multi-document tab plan (Slice 1).
/// Wraps the existing <see cref="AvaloniaDocumentSession"/> and owns the
/// document-level state that must survive tab switches: dirty flag, the
/// optional backing file path, and a stable display title.
/// </summary>
/// <remarks>
/// TODO (Slice 2+): move undo/redo history and annotation-source ownership
/// from <c>CanvasHost</c> into this aggregate so each tab keeps its own
/// history. For now those remain on the canvas to avoid breaking the
/// existing undo/layer tests.
/// </remarks>
public sealed class DocumentSession
{
	private const string UntitledTitle = "Untitled";

	private string? file_path;
	private bool is_dirty;
	private string title;

	public DocumentSession (AvaloniaDocumentSession document)
		: this (document, null)
	{
	}

	public DocumentSession (AvaloniaDocumentSession document, string? filePath)
	{
		Document = document ?? throw new ArgumentNullException (nameof (document));
		file_path = NormalizePath (filePath);
		title = BuildTitle (file_path);
	}

	/// <summary>The underlying viewport/layer state for this document.</summary>
	public AvaloniaDocumentSession Document { get; }

	/// <summary>Raised when <see cref="IsDirty"/>, <see cref="FilePath"/> or <see cref="Title"/> change.</summary>
	public event EventHandler? StateChanged;

	/// <summary>True when the document has unsaved changes.</summary>
	public bool IsDirty => is_dirty;

	/// <summary>Backing file path, or <c>null</c> for a never-saved document.</summary>
	public string? FilePath => file_path;

	/// <summary>Stable display title derived from <see cref="FilePath"/>.</summary>
	public string Title => title;

	/// <summary>Marks the document as having unsaved changes.</summary>
	public void MarkDirty ()
	{
		if (is_dirty)
			return;
		is_dirty = true;
		StateChanged?.Invoke (this, EventArgs.Empty);
	}

	/// <summary>Marks the document as saved (no unsaved changes).</summary>
	public void MarkClean ()
	{
		if (!is_dirty)
			return;
		is_dirty = false;
		StateChanged?.Invoke (this, EventArgs.Empty);
	}

	/// <summary>
	/// Records a successful save. The path is only updated here, so callers
	/// must invoke this after the write has actually succeeded.
	/// </summary>
	public void MarkSaved (string path)
	{
		if (string.IsNullOrWhiteSpace (path))
			throw new ArgumentException ("A save path is required.", nameof (path));

		string normalized = NormalizePath (path)!;
		bool path_changed = !string.Equals (file_path, normalized, StringComparison.Ordinal);
		bool dirty_changed = is_dirty;
		file_path = normalized;
		is_dirty = false;
		if (path_changed) {
			title = BuildTitle (file_path);
			StateChanged?.Invoke (this, EventArgs.Empty);
		} else if (dirty_changed)
			StateChanged?.Invoke (this, EventArgs.Empty);
	}

	private static string? NormalizePath (string? path)
		=> string.IsNullOrWhiteSpace (path) ? null : path;

	private static string BuildTitle (string? path)
	{
		if (path is null)
			return UntitledTitle;
		string name = Path.GetFileName (path);
		return string.IsNullOrEmpty (name) ? UntitledTitle : name;
	}
}
