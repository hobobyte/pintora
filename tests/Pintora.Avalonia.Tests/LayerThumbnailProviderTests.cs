using System;
using System.Collections.Generic;
using Avalonia.Media.Imaging;
using NUnit.Framework;
using Pinta.Core;
using Pintora.Avalonia.Canvas;
using Pintora.Avalonia.Document;
using Pintora.Avalonia.Imaging;

namespace Pintora.Avalonia.Tests;

[TestFixture]
internal sealed class LayerThumbnailProviderTests
{
	/// <summary>
	/// Records what the provider asked to render and returns a null bitmap so
	/// no platform render interface is required. The provider and event
	/// payload tolerate a null bitmap for this headless path.
	/// </summary>
	private sealed class FakeSink : IThumbnailSink
	{
		public int CreateCount { get; private set; }
		public int Width { get; private set; }
		public int Height { get; private set; }
		public ColorBgra[] Pixels { get; private set; } = Array.Empty<ColorBgra> ();

		public WriteableBitmap CreateBitmap (int width, int height, ReadOnlySpan<ColorBgra> pixels)
		{
			CreateCount++;
			Width = width;
			Height = height;
			Pixels = pixels.ToArray ();
			return null!;
		}
	}

	private sealed class ManualScheduler : IThumbnailScheduler
	{
		private readonly Queue<Action> pending = new ();
		public int ScheduleCount { get; private set; }
		public void Schedule (Action action)
		{
			ScheduleCount++;
			pending.Enqueue (action);
		}
		public void RunPending ()
		{
			while (pending.Count > 0)
				pending.Dequeue () ();
		}
	}

	[Test]
	public void GetThumbnailReturnsBitmapForLayer ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));
		canvas.AddLayer ();
		var sink = new FakeSink ();
		using var provider = new LayerThumbnailProvider (canvas, new ManualScheduler (), sink);

		var bitmap = provider.GetThumbnail (canvas.Layers[0]);

		Assert.That (bitmap, Is.Null, "Fake sink returns null; the provider must pass it through.");
		Assert.That (sink.CreateCount, Is.EqualTo (1));
		Assert.That (sink.Width, Is.GreaterThan (0));
		Assert.That (sink.Height, Is.GreaterThan (0));
	}

	[Test]
	public void GetThumbnailReturnsNullForUnknownLayer ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));
		canvas.AddLayer ();
		CanvasHost other = new (new Pinta.Core.Size (100, 100));
		other.AddLayer ();
		var sink = new FakeSink ();
		using var provider = new LayerThumbnailProvider (canvas, new ManualScheduler (), sink);

		Assert.That (provider.GetThumbnail (other.Layers[0]), Is.Null);
		Assert.That (sink.CreateCount, Is.Zero, "Unknown layers must not be rendered.");
	}

	[Test]
	public void PaintInvalidatesAndFlushRaisesThumbnailUpdated ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));
		canvas.AddLayer ();
		AvaloniaLayer layer = canvas.Layers[0];
		var scheduler = new ManualScheduler ();
		var sink = new FakeSink ();
		using var provider = new LayerThumbnailProvider (canvas, scheduler, sink);
		var updates = new List<AvaloniaLayer> ();
		provider.ThumbnailUpdated += (_, e) => updates.Add (e.Layer);

		canvas.PaintCanvasPoint (new PointD (10, 10), ColorBgra.Red);
		Assert.That (updates, Is.Empty, "Thumbnail must not regenerate synchronously during paint.");

		scheduler.RunPending ();

		Assert.That (updates, Does.Contain (layer));
		Assert.That (sink.CreateCount, Is.EqualTo (1));
	}

	[Test]
	public void ThumbnailUpdatedAllowsNullBitmapFromSink ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));
		canvas.AddLayer ();
		var scheduler = new ManualScheduler ();
		var sink = new FakeSink ();
		using var provider = new LayerThumbnailProvider (canvas, scheduler, sink);
		LayerThumbnailUpdatedEventArgs? received = null;
		provider.ThumbnailUpdated += (_, e) => received = e;

		canvas.PaintCanvasPoint (new PointD (10, 10), ColorBgra.Red);
		scheduler.RunPending ();

		Assert.That (received, Is.Not.Null);
		Assert.That (received!.Thumbnail, Is.Null, "Headless sinks may return null; the payload must accept it.");
	}

	[Test]
	public void ContinuousPaintingCoalescesIntoOneFlush ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));
		canvas.AddLayer ();
		var scheduler = new ManualScheduler ();
		var sink = new FakeSink ();
		using var provider = new LayerThumbnailProvider (canvas, scheduler, sink);
		int updates = 0;
		provider.ThumbnailUpdated += (_, _) => updates++;

		canvas.BeginStroke ();
		for (int i = 0; i < 50; i++)
			canvas.PaintCanvasPoint (new PointD (i, i), ColorBgra.Red);
		canvas.CommitStroke ();

		Assert.That (scheduler.ScheduleCount, Is.EqualTo (1), "A stroke must schedule exactly one flush.");
		Assert.That (updates, Is.Zero);

		scheduler.RunPending ();

		Assert.That (updates, Is.EqualTo (1));
		Assert.That (sink.CreateCount, Is.EqualTo (1), "Coalesced flush must render once.");
	}

	[Test]
	public void TransparentLayerProducesTransparentThumbnail ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));
		canvas.AddLayer ();
		var sink = new FakeSink ();
		using var provider = new LayerThumbnailProvider (canvas, new ManualScheduler (), sink);

		provider.GetThumbnail (canvas.Layers[0]);

		Assert.That (sink.CreateCount, Is.EqualTo (1));
		Assert.That (sink.Pixels, Is.Not.Empty);
		// A freshly added layer is fully transparent; the thumbnail must not
		// invent opaque pixels.
		foreach (ColorBgra pixel in sink.Pixels)
			Assert.That (pixel.A, Is.EqualTo (0));
	}

	[Test]
	public void EmptyLayerProducesValidBitmap ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));
		canvas.AddLayer ();
		var sink = new FakeSink ();
		using var provider = new LayerThumbnailProvider (canvas, new ManualScheduler (), sink);

		provider.GetThumbnail (canvas.Layers[0]);

		Assert.That (sink.CreateCount, Is.EqualTo (1));
		Assert.That (sink.Width, Is.GreaterThan (0));
		Assert.That (sink.Height, Is.GreaterThan (0));
	}

	[Test]
	public void SampleThumbnailDownscalesWithinBounds ()
	{
		using var surface = new MemoryImageSurface (200, 100);
		Span<ColorBgra> data = surface.GetPixelData ();
		data.Fill (ColorBgra.Blue);

		(int width, int height, ColorBgra[] pixels) = LayerThumbnailProvider.SampleThumbnail (surface, 64, 40);

		Assert.That (width, Is.LessThanOrEqualTo (64));
		Assert.That (height, Is.LessThanOrEqualTo (40));
		Assert.That (pixels, Has.Length.EqualTo (width * height));
		foreach (ColorBgra pixel in pixels)
			Assert.That (pixel, Is.EqualTo (ColorBgra.Blue));
	}

	[Test]
	public void DisposeUnsubscribesFromCanvas ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));
		canvas.AddLayer ();
		var scheduler = new ManualScheduler ();
		var sink = new FakeSink ();
		var provider = new LayerThumbnailProvider (canvas, scheduler, sink);
		int updates = 0;
		provider.ThumbnailUpdated += (_, _) => updates++;
		provider.Dispose ();

		canvas.PaintCanvasPoint (new PointD (10, 10), ColorBgra.Red);
		scheduler.RunPending ();

		Assert.That (updates, Is.Zero);
		Assert.That (scheduler.ScheduleCount, Is.Zero);
		Assert.That (sink.CreateCount, Is.Zero);
	}

	[Test]
	public void RemovedLayerIsDroppedFromCache ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));
		canvas.AddLayer ();
		canvas.AddLayer ();
		AvaloniaLayer removed = canvas.Layers[canvas.ActiveLayerIndex];
		var sink = new FakeSink ();
		using var provider = new LayerThumbnailProvider (canvas, new ManualScheduler (), sink);
		provider.GetThumbnail (removed);
		Assert.That (sink.CreateCount, Is.EqualTo (1));

		canvas.RemoveLayer ();

		Assert.That (provider.GetThumbnail (removed), Is.Null);
		Assert.That (sink.CreateCount, Is.EqualTo (1), "Removed layers must not be re-rendered.");
	}

	[Test]
	public void SingleSubscriptionSurvivesRebuildsAndUpdatesCurrentControl ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));
		canvas.AddLayer ();
		AvaloniaLayer layer = canvas.Layers[0];
		var scheduler = new ManualScheduler ();
		var sink = new FakeSink ();
		using var provider = new LayerThumbnailProvider (canvas, scheduler, sink);

		// Mirrors MainWindow: one handler, a dictionary of live controls that
		// is replaced on every rebuild.
		var controls = new Dictionary<AvaloniaLayer, object> ();
		int handler_invocations = 0;
		EventHandler<LayerThumbnailUpdatedEventArgs> handler = (_, e) => {
			handler_invocations++;
			if (e.Thumbnail is not null && controls.TryGetValue (e.Layer, out object? control))
				control.ToString ();
		};
		provider.ThumbnailUpdated += handler;

		for (int rebuild = 0; rebuild < 5; rebuild++) {
			controls.Clear ();
			controls[layer] = new object ();
			canvas.PaintCanvasPoint (new PointD (rebuild, rebuild), ColorBgra.Red);
			scheduler.RunPending ();
		}

		Assert.That (handler_invocations, Is.EqualTo (5), "One handler must fire once per flush, not once per rebuild.");
		provider.ThumbnailUpdated -= handler;
	}

	[Test]
	public void RemovedLayerIsDroppedFromCacheAfterFlush ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));
		canvas.AddLayer ();
		canvas.AddLayer ();
		AvaloniaLayer removed = canvas.Layers[canvas.ActiveLayerIndex];
		var scheduler = new ManualScheduler ();
		var sink = new FakeSink ();
		using var provider = new LayerThumbnailProvider (canvas, scheduler, sink);
		provider.GetThumbnail (removed);
		Assert.That (sink.CreateCount, Is.EqualTo (1));

		canvas.PaintCanvasPoint (new PointD (10, 10), ColorBgra.Red);
		canvas.RemoveLayer ();
		scheduler.RunPending ();

		Assert.That (provider.GetThumbnail (removed), Is.Null);
		Assert.That (sink.CreateCount, Is.EqualTo (1), "Removed layers must not be re-rendered during flush.");
	}
}
