using System;
using System.Collections.Generic;
using Pinta.Core;
using Pintora.Avalonia.Imaging;

namespace Pintora.Avalonia.Document;

/// <summary>
/// Avalonia-side document session while the legacy Pinta composition root is still GTK-based.
/// This keeps viewport state and the raster contract available without importing PintaCore globals.
/// </summary>
public sealed class AvaloniaDocumentSession
{
	public AvaloniaDocumentSession (Size imageSize, IImageSurface? surface = null)
	{
		Viewport = new DocumentViewportState (imageSize);
		if (surface is MemoryImageSurface memorySurface)
			Layers.Add (new AvaloniaLayer ("Background", memorySurface));
	}

	public DocumentViewportState Viewport { get; }
	public List<AvaloniaLayer> Layers { get; } = new ();
	public int ActiveLayerIndex { get; private set; }
	public AvaloniaLayer? ActiveLayer => Layers.Count == 0 ? null : Layers[ActiveLayerIndex];
	public IImageSurface? Surface => ActiveLayer?.Surface;

	public Size ImageSize => Viewport.ImageSize;
	public Size ViewSize => Viewport.ViewSize;
	public double Scale => Viewport.Scale;

	public void SetScale (double scale) => Viewport.SetScale (scale);

	public AvaloniaLayer AddLayer (string name)
	{
		AvaloniaLayer layer = new (name, new MemoryImageSurface (ImageSize.Width, ImageSize.Height));
		Layers.Add (layer);
		ActiveLayerIndex = Layers.Count - 1;
		return layer;
	}

	public void SelectLayer (int index)
	{
		if ((uint) index >= Layers.Count)
			throw new ArgumentOutOfRangeException (nameof (index));
		ActiveLayerIndex = index;
	}

	public bool RemoveActiveLayer ()
	{
		if (Layers.Count <= 1)
			return false;
		Layers.RemoveAt (ActiveLayerIndex);
		ActiveLayerIndex = Math.Min (ActiveLayerIndex, Layers.Count - 1);
		return true;
	}

	public bool MoveActiveLayer (int direction)
	{
		int target = ActiveLayerIndex + Math.Sign (direction);
		if ((uint) target >= Layers.Count)
			return false;
		(Layers[ActiveLayerIndex], Layers[target]) = (Layers[target], Layers[ActiveLayerIndex]);
		ActiveLayerIndex = target;
		return true;
	}

	public void RenameActiveLayer (string name)
	{
		if (string.IsNullOrWhiteSpace (name))
			throw new ArgumentException ("Layer name cannot be empty.", nameof (name));
		ActiveLayer!.Name = name.Trim ();
	}
}
