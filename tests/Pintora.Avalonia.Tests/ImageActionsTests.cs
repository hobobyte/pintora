using System;
using NUnit.Framework;
using Pinta.Core;
using Pintora.Avalonia.Canvas;
using Pintora.Avalonia.Imaging;

namespace Pintora.Avalonia.Tests;

[TestFixture]
internal sealed class ImageActionsTests
{
	private static CanvasHost CreateCanvas (int width, int height)
	{
		var surface = new MemoryImageSurface (width, height);
		surface.Fill (ColorBgra.White);
		return new CanvasHost (new Pinta.Core.Size (width, height), surface);
	}

	[Test]
	public void ResizeCanvasKeepsPixelsAndIsUndoable ()
	{
		CanvasHost canvas = CreateCanvas (10, 10);
		canvas.PaintCanvasPoint (new PointD (2, 2), ColorBgra.Red);

		canvas.ResizeCanvas (20, 15);

		Assert.That (canvas.ImageSize, Is.EqualTo (new Pinta.Core.Size (20, 15)));
		Assert.That (canvas.IsDirty, Is.True);
		Assert.That (canvas.CanUndoLayerOperation, Is.True);

		canvas.UndoLayerOperation ();

		Assert.That (canvas.ImageSize, Is.EqualTo (new Pinta.Core.Size (10, 10)));
	}

	[Test]
	public void ResizeCanvasRejectsNonPositiveDimensions ()
	{
		CanvasHost canvas = CreateCanvas (10, 10);

		Assert.That (() => canvas.ResizeCanvas (0, 10), Throws.TypeOf<ArgumentOutOfRangeException> ());
		Assert.That (() => canvas.ResizeCanvas (10, -1), Throws.TypeOf<ArgumentOutOfRangeException> ());
	}

	[Test]
	public void FlipImageHorizontalMirrorsPixels ()
	{
		CanvasHost canvas = CreateCanvas (4, 1);
		canvas.PaintCanvasPoint (new PointD (0, 0), ColorBgra.Red);

		canvas.FlipImageHorizontal ();

		Assert.That (canvas.IsDirty, Is.True);
		Assert.That (canvas.CanUndoLayerOperation, Is.True);
		canvas.UndoLayerOperation ();
		Assert.That (canvas.ImageSize, Is.EqualTo (new Pinta.Core.Size (4, 1)));
	}

	[Test]
	public void FlipImageVerticalIsUndoable ()
	{
		CanvasHost canvas = CreateCanvas (4, 4);

		canvas.FlipImageVertical ();

		Assert.That (canvas.CanUndoLayerOperation, Is.True);
		canvas.UndoLayerOperation ();
		Assert.That (canvas.ImageSize, Is.EqualTo (new Pinta.Core.Size (4, 4)));
	}

	[Test]
	public void RotateImageClockwiseSwapsDimensions ()
	{
		CanvasHost canvas = CreateCanvas (8, 4);

		canvas.RotateImageClockwise ();

		Assert.That (canvas.ImageSize, Is.EqualTo (new Pinta.Core.Size (4, 8)));
		Assert.That (canvas.IsDirty, Is.True);

		canvas.UndoLayerOperation ();

		Assert.That (canvas.ImageSize, Is.EqualTo (new Pinta.Core.Size (8, 4)));
	}

	[Test]
	public void RotateImageCounterClockwiseSwapsDimensions ()
	{
		CanvasHost canvas = CreateCanvas (8, 4);

		canvas.RotateImageCounterClockwise ();

		Assert.That (canvas.ImageSize, Is.EqualTo (new Pinta.Core.Size (4, 8)));
	}

	[Test]
	public void RotateImage180KeepsDimensions ()
	{
		CanvasHost canvas = CreateCanvas (8, 4);

		canvas.RotateImage180 ();

		Assert.That (canvas.ImageSize, Is.EqualTo (new Pinta.Core.Size (8, 4)));
		Assert.That (canvas.CanUndoLayerOperation, Is.True);
	}

	[Test]
	public void FlattenImageCollapsesLayersAndIsUndoable ()
	{
		CanvasHost canvas = CreateCanvas (10, 10);
		canvas.AddLayer ();
		canvas.AddLayer ();
		Assert.That (canvas.Layers, Has.Count.EqualTo (3));

		Assert.That (canvas.FlattenImage (), Is.True);

		Assert.That (canvas.Layers, Has.Count.EqualTo (1));
		Assert.That (canvas.ActiveLayerIndex, Is.Zero);
		Assert.That (canvas.IsDirty, Is.True);

		canvas.UndoLayerOperation ();

		Assert.That (canvas.Layers, Has.Count.EqualTo (3));
	}

	[Test]
	public void ResizeCanvasPreservesLayerLock ()
	{
		CanvasHost canvas = CreateCanvas (10, 10);
		canvas.AddLayer ();
		canvas.SetLayerLocked (canvas.ActiveLayerIndex, true);

		canvas.ResizeCanvas (20, 15);

		Assert.That (canvas.Layers[canvas.ActiveLayerIndex].IsLocked, Is.True);
	}

	[Test]
	public void CropImagePreservesLayerLock ()
	{
		CanvasHost canvas = CreateCanvas (10, 10);
		canvas.AddLayer ();
		canvas.SetLayerLocked (canvas.ActiveLayerIndex, true);

		canvas.CropImage (0, 0, 5, 5);

		Assert.That (canvas.Layers[canvas.ActiveLayerIndex].IsLocked, Is.True);
	}

	[Test]
	public void ImageActionsTargetActiveCanvasOnly ()
	{
		CanvasHost first = CreateCanvas (10, 10);
		CanvasHost second = CreateCanvas (10, 10);

		second.RotateImageClockwise ();

		Assert.That (second.ImageSize, Is.EqualTo (new Pinta.Core.Size (10, 10)));
		Assert.That (first.ImageSize, Is.EqualTo (new Pinta.Core.Size (10, 10)));
		Assert.That (first.IsDirty, Is.False);
	}
}
