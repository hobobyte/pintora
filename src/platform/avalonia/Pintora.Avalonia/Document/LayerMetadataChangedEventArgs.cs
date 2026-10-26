using System;

namespace Pintora.Avalonia.Document;

/// <summary>
/// Raised when layer metadata changes. Visibility and opacity affect the
/// composite canvas; name and lock do not.
/// </summary>
public sealed class LayerMetadataChangedEventArgs : EventArgs
{
	public LayerMetadataChangedEventArgs (bool affectsRenderedAppearance)
	{
		AffectsRenderedAppearance = affectsRenderedAppearance;
	}

	public bool AffectsRenderedAppearance { get; }
}
