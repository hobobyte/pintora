using System;
using System.Collections.Generic;
using Pinta.Core;
using Pintora.Avalonia.Imaging;
using System.Collections.ObjectModel;

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
	public ObservableCollection<AvaloniaLayer> Layers { get; } = new ();
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

	public bool RemoveLayerAt (int index)
	{
		if (Layers.Count <= 1 || (uint) index >= Layers.Count)
			return false;
		Layers.RemoveAt (index);
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

	public bool MoveLayerTo (int from, int to)
	{
		if ((uint) from >= Layers.Count || (uint) to >= Layers.Count || from == to)
			return false;
		AvaloniaLayer layer = Layers[from];
		Layers.RemoveAt (from);
		Layers.Insert (to, layer);
		ActiveLayerIndex = to;
		return true;
	}

	public void RenameActiveLayer (string name)
	{
		if (string.IsNullOrWhiteSpace (name))
			throw new ArgumentException ("Layer name cannot be empty.", nameof (name));
		ActiveLayer!.Name = name.Trim ();
	}

	public void ResetLayers (IImageSurface surface)
	{
		if (surface is not MemoryImageSurface memorySurface)
			throw new ArgumentException ("New document surface must be a memory surface.", nameof (surface));
		Layers.Clear ();
		Layers.Add (new AvaloniaLayer ("Background", memorySurface));
		ActiveLayerIndex = 0;
	}

	public void Resize (Size imageSize)
	{
		Size nextSize = new (Math.Max (1, imageSize.Width), Math.Max (1, imageSize.Height));
		var resized = new List<AvaloniaLayer> (Layers.Count);
		foreach (AvaloniaLayer layer in Layers) {
			var target = new MemoryImageSurface (nextSize.Width, nextSize.Height);
			int width = Math.Min (layer.Surface.Width, target.Width);
			int height = Math.Min (layer.Surface.Height, target.Height);
			ReadOnlySpan<ColorBgra> source = layer.Surface.GetReadOnlyPixelData ();
			Span<ColorBgra> destination = target.GetPixelData ();
			for (int y = 0; y < height; y++)
				source.Slice (y * layer.Surface.Width, width).CopyTo (destination.Slice (y * target.Width, width));
			var copy = new AvaloniaLayer (layer.Name, target) { IsVisible = layer.IsVisible, Opacity = layer.Opacity };
			copy.Annotations.AddRange (layer.Annotations.Items);
			resized.Add (copy);
		}
		Layers.Clear ();
		foreach (AvaloniaLayer layer in resized)
			Layers.Add (layer);
		ActiveLayerIndex = Layers.Count == 0 ? 0 : Math.Min (ActiveLayerIndex, Layers.Count - 1);
		Viewport.ImageSize = nextSize;
	}

	public void Crop (int left, int top, int width, int height)
	{
		if (left < 0 || top < 0 || width <= 0 || height <= 0 || left + width > ImageSize.Width || top + height > ImageSize.Height)
			throw new ArgumentOutOfRangeException (nameof (width), "Crop rectangle must be inside the image.");
		var cropped = new List<AvaloniaLayer> (Layers.Count);
		foreach (AvaloniaLayer layer in Layers) {
			var target = new MemoryImageSurface (width, height);
			ReadOnlySpan<ColorBgra> source = layer.Surface.GetReadOnlyPixelData ();
			Span<ColorBgra> destination = target.GetPixelData ();
			for (int y = 0; y < height; y++)
				source.Slice ((top + y) * layer.Surface.Width + left, width).CopyTo (destination.Slice (y * width, width));
			cropped.Add (new AvaloniaLayer (layer.Name, target) { IsVisible = layer.IsVisible, Opacity = layer.Opacity });
		}
		Layers.Clear ();
		foreach (AvaloniaLayer layer in cropped)
			Layers.Add (layer);
		ActiveLayerIndex = Layers.Count == 0 ? 0 : Math.Min (ActiveLayerIndex, Layers.Count - 1);
		Viewport.ImageSize = new Size (width, height);
	}
}
