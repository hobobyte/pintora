using System;

namespace Pintora.Avalonia.Document;

/// <summary>
/// Notifies listeners that the rendered content of a layer changed (pixels
/// or annotations). Name and lock metadata are not content changes.
/// </summary>
public sealed class LayerContentChangedEventArgs : EventArgs
{
	public LayerContentChangedEventArgs (AvaloniaLayer layer)
	{
		Layer = layer ?? throw new ArgumentNullException (nameof (layer));
	}

	/// <summary>The layer whose content changed.</summary>
	public AvaloniaLayer Layer { get; }
}
