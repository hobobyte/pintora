using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Pinta.Core;
using Pintora.Avalonia.Canvas;
using Pintora.Avalonia.Document;
using Pintora.Avalonia.Imaging;

namespace Pintora.Avalonia.Imaging;

/// <summary>
/// Schedules coalesced work. Implementations decide when pending work runs;
/// the default uses the Avalonia dispatcher, while tests can drive it
/// synchronously.
/// </summary>
public interface IThumbnailScheduler
{
	/// <summary>Requests that <paramref name="action"/> runs once, after any
	/// currently pending request has been coalesced with it.</summary>
	void Schedule (Action action);
}

/// <summary>
/// Creates the platform bitmap for a thumbnail. Abstracted so headless tests
/// can run without an <c>IPlatformRenderInterface</c>.
/// </summary>
internal interface IThumbnailSink
{
	/// <summary>Creates a bitmap from straight-alpha BGRA pixels.</summary>
	WriteableBitmap CreateBitmap (int width, int height, ReadOnlySpan<ColorBgra> pixels);
}

/// <summary>Production sink backed by Avalonia's <see cref="WriteableBitmap"/>.</summary>
internal sealed class WriteableBitmapThumbnailSink : IThumbnailSink
{
	public WriteableBitmap CreateBitmap (int width, int height, ReadOnlySpan<ColorBgra> pixels)
	{
		// Straight-alpha ColorBgra matches the canvas preview; premultiplied
		// would darken translucent thumbnails.
		var image = new WriteableBitmap (new PixelSize (width, height), new Vector (96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
		using ILockedFramebuffer locked = image.Lock ();
		int rowBytes = width * ColorBgra.SizeOf;
		for (int y = 0; y < height; y++) {
			ReadOnlySpan<ColorBgra> row = pixels.Slice (y * width, width);
			byte[] bytes = MemoryMarshal.AsBytes (row).ToArray ();
			Marshal.Copy (bytes, 0, locked.Address + y * locked.RowBytes, rowBytes);
		}
		return image;
	}
}

/// <summary>
/// Generates and caches small previews of layer pixels. Lives outside the
/// document model: it observes <see cref="CanvasHost.LayerContentChanged"/>
/// and produces Avalonia bitmaps on demand. Invalidations are coalesced so a
/// continuous brush stroke does not regenerate a thumbnail per pointer event.
/// </summary>
public sealed class LayerThumbnailProvider : IDisposable
{
	private readonly CanvasHost canvas;
	private readonly IThumbnailScheduler scheduler;
	private readonly IThumbnailSink sink;
	private readonly int max_width;
	private readonly int max_height;
	private readonly Dictionary<AvaloniaLayer, WriteableBitmap> thumbnails = new ();
	private readonly HashSet<AvaloniaLayer> pending = new ();
	private bool flush_scheduled;
	private bool disposed;

	/// <summary>Raised after a layer's thumbnail has been regenerated.</summary>
	public event EventHandler<LayerThumbnailUpdatedEventArgs>? ThumbnailUpdated;

	public LayerThumbnailProvider (CanvasHost canvas, IThumbnailScheduler? scheduler = null, int maxWidth = 64, int maxHeight = 40)
		: this (canvas, scheduler, new WriteableBitmapThumbnailSink (), maxWidth, maxHeight)
	{
	}

	internal LayerThumbnailProvider (CanvasHost canvas, IThumbnailScheduler? scheduler, IThumbnailSink sink, int maxWidth = 64, int maxHeight = 40)
	{
		this.canvas = canvas ?? throw new ArgumentNullException (nameof (canvas));
		this.scheduler = scheduler ?? new DispatcherThumbnailScheduler ();
		this.sink = sink ?? throw new ArgumentNullException (nameof (sink));
		if (maxWidth <= 0 || maxHeight <= 0)
			throw new ArgumentOutOfRangeException (nameof (maxWidth), "Thumbnail dimensions must be positive.");
		max_width = maxWidth;
		max_height = maxHeight;
		canvas.LayerContentChanged += OnLayerContentChanged;
		canvas.LayersChanged += OnLayersChanged;
	}

	/// <summary>Returns the current thumbnail for <paramref name="layer"/>, or
	/// null if the layer is not part of the canvas.</summary>
	public WriteableBitmap? GetThumbnail (AvaloniaLayer layer)
	{
		if (disposed || layer is null)
			return null;
		if (thumbnails.TryGetValue (layer, out WriteableBitmap? cached))
			return cached;
		if (!canvas.Layers.Contains (layer))
			return null;
		WriteableBitmap generated = Generate (layer);
		thumbnails[layer] = generated;
		return generated;
	}

	/// <summary>Marks a layer dirty and schedules a coalesced regeneration.</summary>
	public void Invalidate (AvaloniaLayer layer)
	{
		if (disposed || layer is null)
			return;
		if (!canvas.Layers.Contains (layer))
			return;
		pending.Add (layer);
		ScheduleFlush ();
	}

	private void OnLayerContentChanged (object? sender, LayerContentChangedEventArgs e)
		=> Invalidate (e.Layer);

	private void OnLayersChanged (object? sender, EventArgs e)
	{
		// Drop cached thumbnails for layers that are no longer in the stack so
		// removed layers do not keep bitmaps alive.
		if (thumbnails.Count == 0)
			return;
		var stale = new List<AvaloniaLayer> ();
		foreach (AvaloniaLayer layer in thumbnails.Keys)
			if (!canvas.Layers.Contains (layer))
				stale.Add (layer);
		foreach (AvaloniaLayer layer in stale) {
			thumbnails.Remove (layer);
			pending.Remove (layer);
		}
	}

	private void ScheduleFlush ()
	{
		if (flush_scheduled)
			return;
		flush_scheduled = true;
		scheduler.Schedule (Flush);
	}

	private void Flush ()
	{
		flush_scheduled = false;
		if (disposed || pending.Count == 0)
			return;
		AvaloniaLayer[] layers = new AvaloniaLayer[pending.Count];
		pending.CopyTo (layers);
		pending.Clear ();
		foreach (AvaloniaLayer layer in layers) {
			if (!canvas.Layers.Contains (layer))
				continue;
			WriteableBitmap bitmap = Generate (layer);
			thumbnails[layer] = bitmap;
			ThumbnailUpdated?.Invoke (this, new LayerThumbnailUpdatedEventArgs (layer, bitmap));
		}
	}

	private WriteableBitmap Generate (AvaloniaLayer layer)
	{
		MemoryImageSurface surface = layer.Surface;
		(int width, int height, ColorBgra[] pixels) = SampleThumbnail (surface, max_width, max_height);
		return sink.CreateBitmap (width, height, pixels);
	}

	/// <summary>
	/// Nearest-neighbour downscales <paramref name="surface"/> to fit within
	/// <paramref name="maxWidth"/> x <paramref name="maxHeight"/>. Pure and
	/// platform-independent so it can be tested headlessly.
	/// </summary>
	internal static (int Width, int Height, ColorBgra[] Pixels) SampleThumbnail (MemoryImageSurface surface, int maxWidth, int maxHeight)
		=> RasterAlgorithms.SampleThumbnail (surface, maxWidth, maxHeight);

	public void Dispose ()
	{
		if (disposed)
			return;
		disposed = true;
		canvas.LayerContentChanged -= OnLayerContentChanged;
		canvas.LayersChanged -= OnLayersChanged;
		foreach (WriteableBitmap bitmap in thumbnails.Values)
			bitmap?.Dispose ();
		thumbnails.Clear ();
		pending.Clear ();
	}
}

/// <summary>Event payload for <see cref="LayerThumbnailProvider.ThumbnailUpdated"/>.</summary>
public sealed class LayerThumbnailUpdatedEventArgs : EventArgs
{
	public LayerThumbnailUpdatedEventArgs (AvaloniaLayer layer, WriteableBitmap? thumbnail)
	{
		Layer = layer ?? throw new ArgumentNullException (nameof (layer));
		Thumbnail = thumbnail;
	}

	public AvaloniaLayer Layer { get; }

	/// <summary>
	/// The regenerated bitmap, or null when the sink could not produce one
	/// (e.g. headless tests). Consumers must skip the update in that case.
	/// </summary>
	public WriteableBitmap? Thumbnail { get; }
}

/// <summary>Default scheduler that posts work to the Avalonia dispatcher.</summary>
public sealed class DispatcherThumbnailScheduler : IThumbnailScheduler
{
	public void Schedule (Action action)
	{
		if (action is null)
			throw new ArgumentNullException (nameof (action));
		global::Avalonia.Threading.Dispatcher.UIThread.Post (action, global::Avalonia.Threading.DispatcherPriority.Background);
	}
}
