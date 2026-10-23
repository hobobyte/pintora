using NUnit.Framework;
using System;
using System.IO;
using Pintora.Avalonia.Canvas;
using Pintora.Avalonia.Imaging;
using Pinta.Core;

namespace Pintora.Avalonia.Tests;

[TestFixture]
internal sealed class CanvasPaintingTests
{
	[Test]
	public void PaintCanvasPointWritesToSurface ()
	{
		using MemoryImageSurface surface = new (4, 4);
		CanvasHost canvas = new (new Pinta.Core.Size (4, 4), surface);

		canvas.PaintCanvasPoint (new PointD (2, 1), ColorBgra.Red);

		Assert.That (surface.GetReadOnlyPixelData ()[6], Is.EqualTo (ColorBgra.Red));
	}

	[Test]
	public void PaintCanvasPointIgnoresOutsidePixels ()
	{
		using MemoryImageSurface surface = new (4, 4);
		CanvasHost canvas = new (new Pinta.Core.Size (4, 4), surface);

		canvas.PaintCanvasPoint (new PointD (-1, 1), ColorBgra.Red);

		Assert.That (surface.GetReadOnlyPixelData ().ToArray (), Is.All.EqualTo (ColorBgra.Zero));
	}

	[Test]
	public void PaintViewPointUsesViewportScale ()
	{
		using MemoryImageSurface surface = new (4, 4);
		CanvasHost canvas = new (new Pinta.Core.Size (4, 4), surface);
		canvas.SetScale (2);
		canvas.Width = 8;
		canvas.Height = 8;

		canvas.PaintViewPoint (new global::Avalonia.Point (2, 2), ColorBgra.Red);

		Assert.That (surface.GetReadOnlyPixelData ()[5], Is.EqualTo (ColorBgra.Red));
	}

	[Test]
	public void PaintCanvasLineFillsGapsBetweenPoints ()
	{
		using MemoryImageSurface surface = new (5, 1);
		CanvasHost canvas = new (new Pinta.Core.Size (5, 1), surface);

		canvas.PaintCanvasLine (new PointD (0, 0), new PointD (4, 0), ColorBgra.Blue);

		Assert.That (surface.GetReadOnlyPixelData ().ToArray (), Is.All.EqualTo (ColorBgra.Blue));
	}

	[Test]
	public void BrushSizePaintsSquareStamp ()
	{
		using MemoryImageSurface surface = new (5, 5);
		CanvasHost canvas = new (new Pinta.Core.Size (5, 5), surface) { BrushSize = 3 };

		canvas.PaintCanvasPoint (new PointD (2, 2), ColorBgra.Red);

		Assert.That (surface.GetReadOnlyPixelData ()[1 * 5 + 1], Is.EqualTo (ColorBgra.Red));
		Assert.That (surface.GetReadOnlyPixelData ()[3 * 5 + 3], Is.EqualTo (ColorBgra.Red));
		Assert.That (surface.GetReadOnlyPixelData ()[0], Is.EqualTo (ColorBgra.Zero));
	}

	[Test]
	public void PaintViewPointAccountsForCenteredCanvas ()
	{
		using MemoryImageSurface surface = new (4, 4);
		CanvasHost canvas = new (new Pinta.Core.Size (4, 4), surface);
		canvas.SetScale (0.5);
		canvas.Width = 8;
		canvas.Height = 8;

		canvas.PaintViewPoint (new global::Avalonia.Point (3.5, 3.5), ColorBgra.Red);

		Assert.That (surface.GetReadOnlyPixelData ()[1 * 4 + 1], Is.EqualTo (ColorBgra.Red));
	}

	[Test]
	public void UndoRestoresSurfaceSnapshot ()
	{
		using MemoryImageSurface surface = new (4, 1);
		CanvasHost canvas = new (new Pinta.Core.Size (4, 1), surface);
		surface.Fill (ColorBgra.White);
		canvas.BeginStroke ();

		// The public API mirrors the stroke result; snapshot is taken by pointer input.
		canvas.PaintCanvasLine (new PointD (0, 0), new PointD (3, 0), ColorBgra.Red);
		Assert.That (surface.GetReadOnlyPixelData ()[0], Is.EqualTo (ColorBgra.Red));

		canvas.UndoLastStroke ();
		Assert.That (surface.GetReadOnlyPixelData ().ToArray (), Is.EqualTo (new[] { ColorBgra.White, ColorBgra.White, ColorBgra.White, ColorBgra.White }));
	}

	[Test]
	public void UndoAndRedoSupportMultipleStrokes ()
	{
		using MemoryImageSurface surface = new (3, 1);
		CanvasHost canvas = new (new Pinta.Core.Size (3, 1), surface);
		surface.Clear ();

		canvas.BeginStroke ();
		canvas.PaintCanvasPoint (new PointD (0, 0), ColorBgra.Red);
		canvas.BeginStroke ();
		canvas.PaintCanvasPoint (new PointD (1, 0), ColorBgra.Blue);

		canvas.UndoLastStroke ();
		Assert.That (surface.GetReadOnlyPixelData ()[0], Is.EqualTo (ColorBgra.Red));
		Assert.That (surface.GetReadOnlyPixelData ()[1], Is.EqualTo (ColorBgra.Zero));

		canvas.UndoLastStroke ();
		Assert.That (surface.GetReadOnlyPixelData ().ToArray (), Is.All.EqualTo (ColorBgra.Zero));

		canvas.RedoLastStroke ();
		Assert.That (surface.GetReadOnlyPixelData ()[0], Is.EqualTo (ColorBgra.Red));
	}

	[Test]
	public void BrushSizeAdjustmentsAreClamped ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (2, 2));

		canvas.AdjustBrushSize (-10);
		Assert.That (canvas.BrushSize, Is.EqualTo (1));

		canvas.AdjustBrushSize (4);
		Assert.That (canvas.BrushSize, Is.EqualTo (5));
	}

	[Test]
	public void FitToWindowComputesScaleFromAvailableBounds ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (800, 600)) {
			Width = 400,
			Height = 300
		};

		canvas.FitToWindow ();

		Assert.That (canvas.Scale, Is.EqualTo (0.5).Within (0.001));
	}

	[Test]
	public void EraserToolUsesTransparentPixelsForLeftButtonStrokes ()
	{
		using MemoryImageSurface surface = new (3, 1);
		surface.Fill (ColorBgra.Red);
		CanvasHost canvas = new (new Pinta.Core.Size (3, 1), surface) {
			ActiveTool = CanvasHost.CanvasTool.Eraser
		};

		canvas.PaintCanvasPoint (new PointD (1, 0), ColorBgra.Zero);

		Assert.That (surface.GetReadOnlyPixelData ()[0], Is.EqualTo (ColorBgra.Red));
		Assert.That (surface.GetReadOnlyPixelData ()[1], Is.EqualTo (ColorBgra.Zero));
	}

	[Test]
	public void BrushOpacityChangesPaintAlpha ()
	{
		using MemoryImageSurface surface = new (1, 1);
		CanvasHost canvas = new (new Pinta.Core.Size (1, 1), surface) { BrushOpacity = 0.5 };

		canvas.PaintCanvasPoint (new PointD (0, 0), ColorBgra.Red);

		Assert.That (surface.GetReadOnlyPixelData ()[0].A, Is.EqualTo (128).Within (1));
	}

	[Test]
	public void NewDocument_ClearsPixelsHistoryAndResetsScale ()
	{
		using MemoryImageSurface surface = new (10, 10);
		CanvasHost canvas = new (new Pinta.Core.Size (10, 10), surface);
		canvas.SetScale (2.0);
		canvas.BeginStroke ();
		canvas.PaintCanvasPoint (new PointD (4, 4), ColorBgra.Red);

		canvas.NewDocument ();

		Assert.That (canvas.Scale, Is.EqualTo (1.0));
		Assert.That (canvas.CanUndoStroke, Is.False);
		Assert.That (canvas.CanRedoStroke, Is.False);
		Assert.That (surface.GetReadOnlyPixelData ().ToArray (), Is.All.EqualTo (ColorBgra.White));
	}

	[Test]
	public void SavePng_RequiresSurface ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (2, 2));

		Assert.That (() => canvas.SavePng (Path.Combine (Path.GetTempPath (), "pintora-test.png")),
			Throws.InvalidOperationException);
	}
}
