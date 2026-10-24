using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Pinta.Core;
using Pintora.Avalonia.Canvas;
using Pintora.Avalonia.Imaging;

namespace Pintora.Avalonia.Tests;

[TestFixture]
internal sealed class CanvasPaintingTests
{
	[Test]
	public void AvaloniaApplicationHasAlphaReleaseVersion ()
	{
		string? version = typeof (CanvasHost).Assembly
			.GetCustomAttribute<AssemblyInformationalVersionAttribute> ()?.InformationalVersion;

		Assert.That (version, Does.StartWith ("0.0.1-alpha"));
	}

	[Test]
	public void PaintCanvasPointWritesToSurface ()
	{
		using MemoryImageSurface surface = new (4, 4);
		CanvasHost canvas = new (new Pinta.Core.Size (4, 4), surface);

		canvas.PaintCanvasPoint (new PointD (2, 1), ColorBgra.Red);

		Assert.That (surface.GetReadOnlyPixelData ()[6], Is.EqualTo (ColorBgra.Red));
	}

	[Test]
	public void ColorPickerReadsPixelIntoPrimaryColor ()
	{
		using MemoryImageSurface surface = new (2, 1);
		surface.GetPixelData ()[1] = ColorBgra.Red;
		CanvasHost canvas = new (new Pinta.Core.Size (2, 1), surface);

		canvas.PickColorAt (new PointD (1, 0));

		Assert.That (canvas.BrushColor, Is.EqualTo (ColorBgra.Red));
	}

	[Test]
	public void FillReplacesConnectedRegionOnly ()
	{
		using MemoryImageSurface surface = new (3, 2);
		surface.Fill (ColorBgra.White);
		surface.GetPixelData ()[1] = ColorBgra.Black;
		CanvasHost canvas = new (new Pinta.Core.Size (3, 2), surface);

		canvas.FillAt (new PointD (0, 0), ColorBgra.Blue);

		Assert.That (surface.GetReadOnlyPixelData ()[0], Is.EqualTo (ColorBgra.Blue));
		Assert.That (surface.GetReadOnlyPixelData ()[1], Is.EqualTo (ColorBgra.Black));
		Assert.That (surface.GetReadOnlyPixelData ()[3], Is.EqualTo (ColorBgra.Blue));
	}

	[Test]
	public void FillCanBeUndoneAsOneOperation ()
	{
		using MemoryImageSurface surface = new (2, 2);
		surface.Fill (ColorBgra.White);
		CanvasHost canvas = new (new Pinta.Core.Size (2, 2), surface);

		Assert.That (canvas.FillAt (new PointD (0, 0), ColorBgra.Blue), Is.True);
		canvas.UndoLastStroke ();

		Assert.That (surface.GetReadOnlyPixelData ().ToArray (), Is.All.EqualTo (ColorBgra.White));
	}

	[Test]
	public void FillAtMaximumToleranceVisitsEachPixelOnce ()
	{
		using MemoryImageSurface surface = new (3, 2);
		surface.Fill (ColorBgra.White);
		CanvasHost canvas = new (new Pinta.Core.Size (3, 2), surface) { FillTolerance = 255 };

		Assert.That (canvas.FillAt (new PointD (0, 0), ColorBgra.Blue), Is.True);
		Assert.That (surface.GetReadOnlyPixelData ().ToArray (), Is.All.EqualTo (ColorBgra.Blue));
		canvas.UndoLastStroke ();
		Assert.That (surface.GetReadOnlyPixelData ().ToArray (), Is.All.EqualTo (ColorBgra.White));
	}

	[Test]
	public void UndoAndRedoChangeThePaintedLayerAfterSelectionChanges ()
	{
		using MemoryImageSurface background = new (2, 1);
		CanvasHost canvas = new (new Pinta.Core.Size (2, 1), background);
		canvas.BeginStroke ();
		canvas.PaintCanvasPoint (new PointD (0, 0), ColorBgra.Red);
		canvas.CommitStroke ();
		canvas.AddLayer ();
		canvas.Surface!.GetPixelData ()[1] = ColorBgra.Blue;

		canvas.UndoLastStroke ();
		Assert.That (background.GetReadOnlyPixelData ()[0], Is.EqualTo (ColorBgra.Zero));
		Assert.That (canvas.Surface.GetReadOnlyPixelData ()[1], Is.EqualTo (ColorBgra.Blue));
		canvas.RedoLastStroke ();
		Assert.That (background.GetReadOnlyPixelData ()[0], Is.EqualTo (ColorBgra.Red));
		Assert.That (canvas.Surface.GetReadOnlyPixelData ()[1], Is.EqualTo (ColorBgra.Blue));
	}

	[Test]
	public void NewDocumentClearsLayerUndoAndHistory ()
	{
		using MemoryImageSurface surface = new (2, 1);
		CanvasHost canvas = new (new Pinta.Core.Size (2, 1), surface);
		canvas.AddLayer ();
		canvas.BeginStroke ();
		canvas.PaintCanvasPoint (new PointD (0, 0), ColorBgra.Red);
		canvas.CommitStroke ();

		canvas.NewDocument (3, 1);

		Assert.That (canvas.CanUndoLayerOperation, Is.False);
		Assert.That (canvas.CanUndoStroke, Is.False);
		Assert.That (canvas.History, Is.Empty);
	}

	[Test]
	public void ResizeAndCropClearSnapshotsFromEarlierDimensions ()
	{
		using MemoryImageSurface surface = new (3, 2);
		CanvasHost canvas = new (new Pinta.Core.Size (3, 2), surface);
		canvas.AddLayer ();
		canvas.ResizeImage (2, 2);
		Assert.That (canvas.CanUndoLayerOperation, Is.False);

		canvas.AddLayer ();
		canvas.CropImage (0, 0, 1, 1);
		Assert.That (canvas.CanUndoLayerOperation, Is.False);
		canvas.UndoLayerOperation ();
		Assert.That (canvas.Layers, Has.Count.EqualTo (3));
	}

	[Test]
	public void RasterUndoKeepsLayerIdentityAfterLayerUndo ()
	{
		using MemoryImageSurface background = new (1, 1);
		CanvasHost canvas = new (new Pinta.Core.Size (1, 1), background);
		canvas.AddLayer ();
		canvas.UndoLayerOperation ();
		canvas.BeginStroke ();
		canvas.PaintCanvasPoint (new PointD (0, 0), ColorBgra.Red);
		canvas.CommitStroke ();

		canvas.RedoLayerOperation ();
		canvas.UndoLastStroke ();

		Assert.That (background.GetReadOnlyPixelData ()[0], Is.EqualTo (ColorBgra.Zero));
	}

	[Test]
	public void RectangleCanBeUndoneAsOneOperation ()
	{
		using MemoryImageSurface surface = new (8, 8);
		CanvasHost canvas = new (new Pinta.Core.Size (8, 8), surface);

		canvas.BeginStroke ();
		canvas.PaintCanvasRectangle (new PointD (1, 1), new PointD (6, 6), ColorBgra.Red);
		canvas.UndoLastStroke ();

		Assert.That (surface.GetReadOnlyPixelData ().ToArray (), Is.All.EqualTo (ColorBgra.Zero));
	}

	[Test]
	public void EllipseCanBeUndoneAsOneOperation ()
	{
		using MemoryImageSurface surface = new (12, 12);
		CanvasHost canvas = new (new Pinta.Core.Size (12, 12), surface);

		canvas.BeginStroke ();
		canvas.PaintCanvasEllipse (new PointD (2, 2), new PointD (9, 9), ColorBgra.Blue);
		canvas.UndoLastStroke ();

		Assert.That (surface.GetReadOnlyPixelData ().ToArray (), Is.All.EqualTo (ColorBgra.Zero));
	}

	[TestCase (CanvasHost.ShapeKind.Line)]
	[TestCase (CanvasHost.ShapeKind.Rectangle)]
	[TestCase (CanvasHost.ShapeKind.Ellipse)]
	public void ShapesToolDrawsSelectedKind (CanvasHost.ShapeKind kind)
	{
		using MemoryImageSurface surface = new (7, 7);
		CanvasHost canvas = new (new Pinta.Core.Size (7, 7), surface) {
			ActiveTool = CanvasHost.CanvasTool.Shapes,
			SelectedShape = kind,
			ShapeOutlineWidth = 1,
			ShapeAntialias = false
		};

		canvas.BeginStroke ();
		canvas.PaintCanvasShape (canvas.SelectedShape, new PointD (1, 1), new PointD (5, 5), ColorBgra.Red);
		canvas.CommitStroke ();

		int paintedIndex = kind switch {
			CanvasHost.ShapeKind.Line => 1 * 7 + 1,
			CanvasHost.ShapeKind.Rectangle => 1 * 7 + 5,
			_ => 1 * 7 + 3
		};
		Assert.That (surface.GetReadOnlyPixelData ()[paintedIndex], Is.EqualTo (ColorBgra.Red));
		Assert.That (surface.GetReadOnlyPixelData ()[1 * 7 + 5], Is.EqualTo (kind == CanvasHost.ShapeKind.Rectangle ? ColorBgra.Red : ColorBgra.Zero));
		canvas.UndoLastStroke ();
		Assert.That (surface.GetReadOnlyPixelData ().ToArray (), Is.All.EqualTo (ColorBgra.Zero));
	}

	[Test]
	public void FillAndOutlineUsesBothPaletteColors ()
	{
		using MemoryImageSurface surface = new (11, 11);
		CanvasHost canvas = new (new Pinta.Core.Size (11, 11), surface) {
			FillStyle = CanvasHost.ShapeFillStyle.FillAndOutline,
			ShapeOutlineWidth = 1,
			SecondaryBrushColor = ColorBgra.Blue,
			ShapeAntialias = false
		};

		canvas.BeginStroke ();
		canvas.PaintCanvasShape (CanvasHost.ShapeKind.Rectangle, new PointD (2, 2), new PointD (8, 8), ColorBgra.Red);
		canvas.CommitStroke ();

		Assert.That (surface.GetReadOnlyPixelData ()[2 * 11 + 5], Is.EqualTo (ColorBgra.Red));
		Assert.That (surface.GetReadOnlyPixelData ()[5 * 11 + 5], Is.EqualTo (ColorBgra.Blue));
		canvas.UndoLastStroke ();
		Assert.That (surface.GetReadOnlyPixelData ().ToArray (), Is.All.EqualTo (ColorBgra.Zero));
	}

	[Test]
	public void ChangingShapeOptionsUpdatesTheSelectedShape ()
	{
		using MemoryImageSurface surface = new (10, 10);
		CanvasHost canvas = new (new Pinta.Core.Size (10, 10), surface) {
			ActiveTool = CanvasHost.CanvasTool.Shapes,
			ShapeOutlineWidth = 1,
			ShapeAntialias = false
		};
		canvas.PaintCanvasShape (CanvasHost.ShapeKind.Rectangle,
			new PointD (1, 1), new PointD (8, 8), ColorBgra.Red);

		Assert.That (surface.GetReadOnlyPixelData ()[5 * 10 + 5], Is.EqualTo (ColorBgra.Zero));
		canvas.FillStyle = CanvasHost.ShapeFillStyle.Fill;
		Assert.That (surface.GetReadOnlyPixelData ()[5 * 10 + 5], Is.EqualTo (ColorBgra.Red));
		canvas.UndoLastStroke ();
		Assert.That (surface.GetReadOnlyPixelData ()[5 * 10 + 5], Is.EqualTo (ColorBgra.Zero));
		canvas.RedoLastStroke ();
		Assert.That (surface.GetReadOnlyPixelData ()[5 * 10 + 5], Is.EqualTo (ColorBgra.Red));

		canvas.FillStyle = CanvasHost.ShapeFillStyle.Outline;
		canvas.ShapeOutlineWidth = 3;
		Assert.That (surface.GetReadOnlyPixelData ()[2 * 10 + 4], Is.EqualTo (ColorBgra.Red));

		canvas.ShapeOutlineWidth = 1;
		canvas.SelectedShape = CanvasHost.ShapeKind.Ellipse;
		Assert.That (surface.GetReadOnlyPixelData ()[1 * 10 + 1], Is.EqualTo (ColorBgra.Zero));
		Assert.That (surface.GetReadOnlyPixelData ()[1 * 10 + 4], Is.EqualTo (ColorBgra.Red));
	}

	[Test]
	public void OutlineWidthChangesLineThickness ()
	{
		using MemoryImageSurface surface = new (12, 9);
		CanvasHost canvas = new (new Pinta.Core.Size (12, 9), surface) { ShapeOutlineWidth = 3, ShapeAntialias = false };

		canvas.PaintCanvasShape (CanvasHost.ShapeKind.Line, new PointD (1, 4), new PointD (10, 4), ColorBgra.Red);

		Assert.That (surface.GetReadOnlyPixelData ()[3 * 12 + 5], Is.EqualTo (ColorBgra.Red));
		Assert.That (surface.GetReadOnlyPixelData ()[5 * 12 + 5], Is.EqualTo (ColorBgra.Red));
		Assert.That (surface.GetReadOnlyPixelData ()[2 * 12 + 5], Is.EqualTo (ColorBgra.Zero));
	}

	[Test]
	public void DashedOutlineLeavesGaps ()
	{
		using MemoryImageSurface surface = new (30, 3);
		CanvasHost canvas = new (new Pinta.Core.Size (30, 3), surface) {
			ShapeOutlineWidth = 1,
			DashStyle = CanvasHost.ShapeDashStyle.Dash,
			ShapeAntialias = false
		};

		canvas.PaintCanvasShape (CanvasHost.ShapeKind.Line, new PointD (1, 1), new PointD (25, 1), ColorBgra.Red);

		Assert.That (surface.GetReadOnlyPixelData ()[1 * 30 + 3], Is.EqualTo (ColorBgra.Red));
		Assert.That (surface.GetReadOnlyPixelData ()[1 * 30 + 9], Is.EqualTo (ColorBgra.Zero));
		Assert.That (surface.GetReadOnlyPixelData ()[1 * 30 + 13], Is.EqualTo (ColorBgra.Red));
	}

	[Test]
	public void RoundedRectangleDoesNotPaintSquareCorners ()
	{
		using MemoryImageSurface surface = new (20, 20);
		CanvasHost canvas = new (new Pinta.Core.Size (20, 20), surface) {
			ShapeOutlineWidth = 1,
			ShapeCornerRadius = 5,
			FillStyle = CanvasHost.ShapeFillStyle.Fill,
			ShapeAntialias = false
		};

		canvas.PaintCanvasShape (CanvasHost.ShapeKind.RoundedRectangle, new PointD (2, 2), new PointD (16, 16), ColorBgra.Red);

		Assert.That (surface.GetReadOnlyPixelData ()[2 * 20 + 2], Is.EqualTo (ColorBgra.Zero));
		Assert.That (surface.GetReadOnlyPixelData ()[2 * 20 + 9], Is.EqualTo (ColorBgra.Red));
		Assert.That (surface.GetReadOnlyPixelData ()[9 * 20 + 9], Is.EqualTo (ColorBgra.Red));
	}

	[Test]
	public void SmoothShapeProducesPartialEdgeCoverage ()
	{
		using MemoryImageSurface surface = new (12, 12);
		CanvasHost canvas = new (new Pinta.Core.Size (12, 12), surface) {
			FillStyle = CanvasHost.ShapeFillStyle.Fill,
			ShapeAntialias = true
		};

		canvas.PaintCanvasShape (CanvasHost.ShapeKind.Rectangle, new PointD (2.25, 2.25), new PointD (8.75, 8.75), ColorBgra.Red);

		Assert.That (surface.GetReadOnlyPixelData ()[5 * 12 + 5].A, Is.EqualTo (255));
		Assert.That (surface.GetReadOnlyPixelData ()[5 * 12 + 2].A, Is.InRange (1, 254));
	}

	[Test]
	public void SmoothDashedOutlineKeepsItsGaps ()
	{
		using MemoryImageSurface surface = new (45, 6);
		CanvasHost canvas = new (new Pinta.Core.Size (45, 6), surface) {
			ShapeOutlineWidth = 2,
			DashStyle = CanvasHost.ShapeDashStyle.Dash,
			ShapeAntialias = true
		};

		canvas.PaintCanvasShape (CanvasHost.ShapeKind.Line, new PointD (1, 3), new PointD (41, 3), ColorBgra.Red);

		Assert.That (surface.GetReadOnlyPixelData ()[3 * 45 + 4].A, Is.GreaterThan (0));
		Assert.That (surface.GetReadOnlyPixelData ()[3 * 45 + 17].A, Is.EqualTo (0));
		Assert.That (surface.GetReadOnlyPixelData ()[3 * 45 + 24].A, Is.GreaterThan (0));
	}

	[Test]
	public void GradientUsesPrimaryAndSecondaryColors ()
	{
		using MemoryImageSurface surface = new (3, 1);
		CanvasHost canvas = new (new Pinta.Core.Size (3, 1), surface) {
			BrushColor = ColorBgra.Black,
			SecondaryBrushColor = ColorBgra.White
		};

		canvas.PaintGradient (new PointD (0, 0), new PointD (2, 0));

		Assert.That (surface.GetReadOnlyPixelData ()[0], Is.EqualTo (ColorBgra.Black));
		Assert.That (surface.GetReadOnlyPixelData ()[2], Is.EqualTo (ColorBgra.White));
	}

	[Test]
	public void GradientCanReverseDirection ()
	{
		using MemoryImageSurface surface = new (2, 1);
		CanvasHost canvas = new (new Pinta.Core.Size (2, 1), surface) {
			BrushColor = ColorBgra.Black,
			SecondaryBrushColor = ColorBgra.White,
			GradientReversed = true
		};

		canvas.PaintGradient (new PointD (0, 0), new PointD (1, 0));

		Assert.That (surface.GetReadOnlyPixelData ()[0], Is.EqualTo (ColorBgra.White));
	}

	[Test]
	public void ZoomAtKeepsCanvasPointUnderCursor ()
	{
		using MemoryImageSurface surface = new (100, 100);
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100), surface) { Width = 200, Height = 200 };
		global::Avalonia.Point viewPoint = new (100, 100);

		canvas.ZoomAt (viewPoint, 2);

		PointD canvasPoint = new (50, 50);
		Assert.That (canvas.ViewSize.Width, Is.EqualTo (200));
		Assert.That (canvasPoint.X * canvas.Scale, Is.EqualTo (viewPoint.X));
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
	public void BrushHardnessSoftensOuterPixels ()
	{
		using MemoryImageSurface surface = new (5, 5);
		CanvasHost canvas = new (new Pinta.Core.Size (5, 5), surface) { BrushSize = 5, BrushHardness = 0.5 };

		canvas.PaintCanvasPoint (new PointD (2, 2), ColorBgra.Red);

		Assert.That (surface.GetReadOnlyPixelData ()[2 * 5 + 2], Is.EqualTo (ColorBgra.Red));
		Assert.That (surface.GetReadOnlyPixelData ()[0], Is.EqualTo (ColorBgra.Zero));
	}

	[Test]
	public void BrushOpacityBlendsWithExistingPixel ()
	{
		using MemoryImageSurface surface = new (1, 1);
		surface.GetPixelData ()[0] = ColorBgra.Blue;
		CanvasHost canvas = new (new Pinta.Core.Size (1, 1), surface) { BrushOpacity = 0.5 };

		canvas.PaintCanvasPoint (new PointD (0, 0), ColorBgra.Red);

		ColorBgra result = surface.GetReadOnlyPixelData ()[0];
		Assert.That (result.A, Is.EqualTo (255));
		Assert.That (result.R, Is.EqualTo (128).Within (1));
		Assert.That (result.B, Is.EqualTo (127).Within (1));
	}

	[Test]
	public void PencilUsesOpacityWhileRemainingOnePixelWide ()
	{
		using MemoryImageSurface surface = new (1, 1);
		CanvasHost canvas = new (new Pinta.Core.Size (1, 1), surface) { ActiveTool = CanvasHost.CanvasTool.Pencil, BrushOpacity = 0.5 };

		canvas.PaintPencilLine (new PointD (0, 0), new PointD (0, 0), ColorBgra.Red);

		Assert.That (surface.GetReadOnlyPixelData ()[0].A, Is.EqualTo (128).Within (1));
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
	public void FitToWindowResetsViewportOffsetAfterZoom ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (800, 600)) {
			Width = 1000,
			Height = 800
		};
		global::Avalonia.Point center = new (500, 400);

		canvas.ZoomAt (center, 2.0);
		Assert.That (canvas.CanvasOrigin, Is.Not.EqualTo (new global::Avalonia.Point (100, 100)));

		canvas.FitToWindow ();

		Assert.That (canvas.CanvasOrigin.X, Is.EqualTo (0).Within (0.001));
		Assert.That (canvas.CanvasOrigin.Y, Is.EqualTo (25).Within (0.001));
	}

	[Test]
	public void PanByMovesCanvasOriginWithoutChangingScale ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 80)) {
			Width = 300,
			Height = 240
		};
		global::Avalonia.Point initialOrigin = canvas.CanvasOrigin;
		double initialScale = canvas.Scale;

		canvas.PanBy (new global::Avalonia.Vector (18, -7));

		Assert.That (canvas.Scale, Is.EqualTo (initialScale));
		Assert.That (canvas.CanvasOrigin.X, Is.EqualTo (initialOrigin.X + 18).Within (0.001));
		Assert.That (canvas.CanvasOrigin.Y, Is.EqualTo (initialOrigin.Y - 7).Within (0.001));
	}

	[Test]
	public void FitToWindowRestoresCenteredScaleAfterPan ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (800, 600)) {
			Width = 400,
			Height = 300
		};
		canvas.SetScale (2.0);
		canvas.PanBy (new global::Avalonia.Vector (70, -25));

		canvas.FitToWindow ();

		Assert.That (canvas.Scale, Is.EqualTo (0.5).Within (0.001));
		Assert.That (canvas.CanvasOrigin.X, Is.EqualTo (0).Within (0.001));
		Assert.That (canvas.CanvasOrigin.Y, Is.EqualTo (0).Within (0.001));
	}

	[Test]
	public void SetActualSizeResetsScaleAndPan ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 80)) {
			Width = 300,
			Height = 240
		};
		canvas.SetScale (2.0);
		canvas.PanBy (new global::Avalonia.Vector (30, 12));

		canvas.SetActualSize ();

		Assert.That (canvas.Scale, Is.EqualTo (1.0));
		Assert.That (canvas.CanvasOrigin.X, Is.EqualTo (100).Within (0.001));
		Assert.That (canvas.CanvasOrigin.Y, Is.EqualTo (80).Within (0.001));
	}

	[Test]
	public void SetScaleRaisesScaleChanged ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (10, 10));
		int notifications = 0;
		canvas.ScaleChanged += (_, _) => notifications++;

		canvas.SetScale (2.0);

		Assert.That (notifications, Is.EqualTo (1));
		Assert.That (canvas.Scale, Is.EqualTo (2.0));
	}

	[Test]
	public void BrushSizeRaisesChangeNotificationAndClamps ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (10, 10));
		int notifications = 0;
		canvas.BrushSizeChanged += (_, _) => notifications++;

		canvas.BrushSize = 12;
		canvas.BrushSize = 999;

		Assert.That (canvas.BrushSize, Is.EqualTo (256));
		Assert.That (notifications, Is.EqualTo (2));
	}

	[Test]
	public void ToolNumericOptionsAreClamped ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (10, 10)) {
			BrushOpacity = 4,
			BrushHardness = -1,
			FillTolerance = 999
		};

		Assert.That (canvas.BrushOpacity, Is.EqualTo (1));
		Assert.That (canvas.BrushHardness, Is.EqualTo (0));
		Assert.That (canvas.FillTolerance, Is.EqualTo (255));
	}

	[Test]
	public void ActiveLayerOpacityIsClampedThroughCanvasHost ()
	{
		using MemoryImageSurface surface = new (10, 10);
		CanvasHost canvas = new (new Pinta.Core.Size (10, 10), surface);

		canvas.SetActiveLayerOpacity (2);

		Assert.That (canvas.Layers[canvas.ActiveLayerIndex].Opacity, Is.EqualTo (1));
	}

	[Test]
	public void ActiveLayerVisibilityIsChangedThroughCanvasHost ()
	{
		using MemoryImageSurface surface = new (10, 10);
		CanvasHost canvas = new (new Pinta.Core.Size (10, 10), surface);

		canvas.SetActiveLayerVisibility (false);

		Assert.That (canvas.Layers[canvas.ActiveLayerIndex].IsVisible, Is.False);
	}

	[Test]
	public void LayerVisibilityDoesNotChangeActiveLayer ()
	{
		using MemoryImageSurface surface = new (10, 10);
		CanvasHost canvas = new (new Pinta.Core.Size (10, 10), surface);
		canvas.AddLayer ();
		int active = canvas.ActiveLayerIndex;

		canvas.SetLayerVisibility (0, false);

		Assert.That (canvas.ActiveLayerIndex, Is.EqualTo (active));
		Assert.That (canvas.Layers[0].IsVisible, Is.False);
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
	public void EraserAlwaysClearsEvenWhenGivenBrushColor ()
	{
		using MemoryImageSurface surface = new (1, 1);
		surface.GetPixelData ()[0] = ColorBgra.Red;
		CanvasHost canvas = new (new Pinta.Core.Size (1, 1), surface) { ActiveTool = CanvasHost.CanvasTool.Eraser };

		canvas.PaintCanvasPoint (new PointD (0, 0), ColorBgra.Blue);

		Assert.That (surface.GetReadOnlyPixelData ()[0], Is.EqualTo (ColorBgra.Zero));
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
		CanvasHost canvas = new (new Pinta.Core.Size (10, 10), surface) {
			Width = 100,
			Height = 100
		};
		canvas.SetScale (2.0);
		canvas.ZoomAt (new global::Avalonia.Point (80, 20), 2.0);
		canvas.BeginStroke ();
		canvas.PaintCanvasPoint (new PointD (4, 4), ColorBgra.Red);

		canvas.NewDocument ();

		Assert.That (canvas.Scale, Is.EqualTo (1.0));
		Assert.That (canvas.CanvasOrigin.X, Is.EqualTo (45).Within (0.001));
		Assert.That (canvas.CanvasOrigin.Y, Is.EqualTo (45).Within (0.001));
		Assert.That (canvas.CanUndoStroke, Is.False);
		Assert.That (canvas.CanRedoStroke, Is.False);
		Assert.That (surface.GetReadOnlyPixelData ().ToArray (), Is.All.EqualTo (ColorBgra.White));
	}

	[Test]
	public void NewDocumentResetsLayerStack ()
	{
		using MemoryImageSurface surface = new (10, 10);
		CanvasHost canvas = new (new Pinta.Core.Size (10, 10), surface);
		canvas.AddLayer ();
		canvas.AddLayer ();

		canvas.NewDocument ();

		Assert.That (canvas.Layers, Has.Count.EqualTo (1));
		Assert.That (canvas.Layers[0].Name, Is.EqualTo ("Background"));
		Assert.That (canvas.ActiveLayerIndex, Is.EqualTo (0));
	}

	[Test]
	public void NewDocumentWithSizeCreatesWhiteBackground ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (2, 2));

		canvas.NewDocument (4, 3);

		Assert.That (canvas.ImageSize, Is.EqualTo (new Pinta.Core.Size (4, 3)));
		Assert.That (canvas.Layers, Has.Count.EqualTo (1));
		Assert.That (canvas.Surface!.GetReadOnlyPixelData ().ToArray (), Is.All.EqualTo (ColorBgra.White));
	}

	[Test]
	public void ResizeImagePreservesLayersAndClipsPixels ()
	{
		using MemoryImageSurface surface = new (3, 2);
		surface.Fill (ColorBgra.Red);
		CanvasHost canvas = new (new Pinta.Core.Size (3, 2), surface);
		canvas.AddLayer ();
		canvas.Surface!.GetPixelData ()[0] = ColorBgra.Blue;

		canvas.ResizeImage (2, 1);

		Assert.That (canvas.ImageSize, Is.EqualTo (new Pinta.Core.Size (2, 1)));
		Assert.That (canvas.Layers, Has.Count.EqualTo (2));
		Assert.That (canvas.Surface!.GetReadOnlyPixelData ().ToArray (), Is.EqualTo (new[] { ColorBgra.Blue, ColorBgra.Zero }));
	}

	[Test]
	public void CropImageCopiesRequestedRegionForAllLayers ()
	{
		using MemoryImageSurface surface = new (3, 2);
		Span<ColorBgra> pixels = surface.GetPixelData ();
		pixels[0] = ColorBgra.Red;
		pixels[1] = ColorBgra.Green;
		pixels[3] = ColorBgra.Blue;
		CanvasHost canvas = new (new Pinta.Core.Size (3, 2), surface);

		canvas.CropImage (1, 0, 2, 1);

		Assert.That (canvas.ImageSize, Is.EqualTo (new Pinta.Core.Size (2, 1)));
		Assert.That (canvas.Surface!.GetReadOnlyPixelData ().ToArray (), Is.EqualTo (new[] { ColorBgra.Green, ColorBgra.Zero }));
	}

	[Test]
	public void SelectRectangleNormalizesAndClipsToCanvas ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (10, 8));

		Assert.That (canvas.SelectRectangle (new PointI (8, 6), new PointI (-2, -1)), Is.True);
		Assert.That (canvas.Selection, Is.EqualTo (new RectangleI (0, 0, 8, 6)));

		canvas.ClearSelection ();
		Assert.That (canvas.Selection, Is.Null);
	}

	[Test]
	public void CopyAndPasteSelectionCreatesNewLayer ()
	{
		using MemoryImageSurface surface = new (4, 2);
		surface.GetPixelData ()[0] = ColorBgra.Red;
		CanvasHost canvas = new (new Pinta.Core.Size (4, 2), surface);
		canvas.SelectRectangle (new PointI (0, 0), new PointI (1, 1));

		Assert.That (canvas.CopySelection (), Is.True);
		Assert.That (canvas.PasteSelection (new PointI (2, 1)), Is.True);
		Assert.That (canvas.Layers, Has.Count.EqualTo (2));
		Assert.That (canvas.Surface!.GetReadOnlyPixelData ()[1 * 4 + 2], Is.EqualTo (ColorBgra.Red));
	}

	[Test]
	public void MoveSelectionMovesPixelsAndClearsSource ()
	{
		using MemoryImageSurface surface = new (4, 1);
		surface.GetPixelData ()[0] = ColorBgra.Red;
		CanvasHost canvas = new (new Pinta.Core.Size (4, 1), surface);
		canvas.SelectRectangle (new PointI (0, 0), new PointI (1, 1));

		Assert.That (canvas.MoveSelection (new PointI (2, 0)), Is.True);

		Assert.That (surface.GetReadOnlyPixelData ().ToArray (), Is.EqualTo (new[] { ColorBgra.Zero, ColorBgra.Zero, ColorBgra.Red, ColorBgra.Zero }));
		Assert.That (canvas.Selection, Is.EqualTo (new RectangleI (2, 0, 1, 1)));
	}

	[Test]
	public void FlipSelectionHorizontalMirrorsPixels ()
	{
		using MemoryImageSurface surface = new (3, 1);
		surface.GetPixelData ()[0] = ColorBgra.Red;
		surface.GetPixelData ()[2] = ColorBgra.Blue;
		CanvasHost canvas = new (new Pinta.Core.Size (3, 1), surface);
		canvas.SelectRectangle (new PointI (0, 0), new PointI (3, 1));

		Assert.That (canvas.FlipSelectionHorizontal (), Is.True);

		Assert.That (surface.GetReadOnlyPixelData ().ToArray (), Is.EqualTo (new[] { ColorBgra.Blue, ColorBgra.Zero, ColorBgra.Red }));
	}

	[Test]
	public void FlipSelectionVerticalMirrorsPixels ()
	{
		using MemoryImageSurface surface = new (1, 2);
		surface.GetPixelData ()[0] = ColorBgra.Red;
		surface.GetPixelData ()[1] = ColorBgra.Blue;
		CanvasHost canvas = new (new Pinta.Core.Size (1, 2), surface);
		canvas.SelectRectangle (new PointI (0, 0), new PointI (1, 2));

		Assert.That (canvas.FlipSelectionVertical (), Is.True);

		Assert.That (surface.GetReadOnlyPixelData ().ToArray (), Is.EqualTo (new[] { ColorBgra.Blue, ColorBgra.Red }));
	}

	[Test]
	public void RotateSelectionClockwiseRotatesSquarePixels ()
	{
		using MemoryImageSurface surface = new (2, 2);
		surface.GetPixelData ()[0] = ColorBgra.Red;
		surface.GetPixelData ()[1] = ColorBgra.Green;
		surface.GetPixelData ()[2] = ColorBgra.Blue;
		CanvasHost canvas = new (new Pinta.Core.Size (2, 2), surface);
		canvas.SelectRectangle (new PointI (0, 0), new PointI (2, 2));

		Assert.That (canvas.RotateSelectionClockwise (), Is.True);
		Assert.That (surface.GetReadOnlyPixelData ().ToArray (), Is.EqualTo (new[] { ColorBgra.Blue, ColorBgra.Red, ColorBgra.Zero, ColorBgra.Green }));
	}

	[Test]
	public void FillToleranceComparesEachChannelIndependently ()
	{
		using MemoryImageSurface surface = new (3, 1);
		surface.GetPixelData ()[0] = ColorBgra.FromBgra (0, 0, 0, 255);
		surface.GetPixelData ()[1] = ColorBgra.FromBgra (100, 0, 0, 255);
		surface.GetPixelData ()[2] = ColorBgra.FromBgra (0, 0, 0, 255);
		CanvasHost canvas = new (new Pinta.Core.Size (3, 1), surface) { FillTolerance = 10 };

		canvas.FillAt (new PointD (0, 0), ColorBgra.Red);

		Assert.That (surface.GetReadOnlyPixelData ()[0], Is.EqualTo (ColorBgra.Red));
		Assert.That (surface.GetReadOnlyPixelData ()[1].R, Is.EqualTo (0));
		Assert.That (surface.GetReadOnlyPixelData ()[2], Is.EqualTo (ColorBgra.FromBgra (0, 0, 0, 255)));
	}

	[Test]
	public void ColorSpaceRoundTripsPrimaryColors ()
	{
		var hsv = ColorSpace.ToHsv (global::Avalonia.Media.Colors.Red);
		global::Avalonia.Media.Color result = ColorSpace.FromHsv (hsv.H, hsv.S, hsv.V);

		Assert.That (result.R, Is.EqualTo (255));
		Assert.That (result.G, Is.EqualTo (0));
		Assert.That (result.B, Is.EqualTo (0));
	}

	[Test]
	public void HistoryTracksUndoAndRedo ()
	{
		using MemoryImageSurface surface = new (1, 1);
		CanvasHost canvas = new (new Pinta.Core.Size (1, 1), surface);
		canvas.BeginStroke ();
		canvas.PaintCanvasPoint (new PointD (0, 0), ColorBgra.Red);
		canvas.CommitStroke ();

		Assert.That (canvas.History, Has.Count.EqualTo (1));
		canvas.UndoLastStroke ();
		Assert.That (canvas.History, Is.Empty);
		canvas.RedoLastStroke ();
		Assert.That (canvas.History, Has.Count.EqualTo (1));
	}

	[Test]
	public void LockedLayerRejectsPaintingAndClear ()
	{
		using MemoryImageSurface surface = new (2, 1);
		CanvasHost canvas = new (new Pinta.Core.Size (2, 1), surface);
		canvas.Layers[0].IsLocked = true;

		canvas.PaintCanvasPoint (new PointD (0, 0), ColorBgra.Red);

		Assert.That (canvas.ClearActiveLayer (), Is.False);
		Assert.That (surface.GetReadOnlyPixelData ()[0], Is.EqualTo (ColorBgra.Zero));
	}

	[Test]
	public void MergeActiveLayerDownCompositesAndRemovesLayer ()
	{
		using MemoryImageSurface background = new (1, 1);
		background.GetPixelData ()[0] = ColorBgra.Blue;
		CanvasHost canvas = new (new Pinta.Core.Size (1, 1), background);
		canvas.AddLayer ();
		canvas.Surface!.GetPixelData ()[0] = ColorBgra.FromBgra (0, 0, 255, 128);

		Assert.That (canvas.MergeActiveLayerDown (), Is.True);
		Assert.That (canvas.Layers, Has.Count.EqualTo (1));
		Assert.That (canvas.Surface!.GetReadOnlyPixelData ()[0].A, Is.EqualTo (255));
	}

	[Test]
	public void MergeLayerCanBeUndoneAndRedone ()
	{
		using MemoryImageSurface surface = new (1, 1);
		CanvasHost canvas = new (new Pinta.Core.Size (1, 1), surface);
		canvas.AddLayer ();
		canvas.MergeActiveLayerDown ();

		canvas.UndoLayerOperation ();
		Assert.That (canvas.Layers, Has.Count.EqualTo (2));
		canvas.RedoLayerOperation ();
		Assert.That (canvas.Layers, Has.Count.EqualTo (1));
	}

	[Test]
	public void StandardUndoAndRedoAlsoHandleLayerOperations ()
	{
		using MemoryImageSurface surface = new (1, 1);
		CanvasHost canvas = new (new Pinta.Core.Size (1, 1), surface);
		canvas.AddLayer ();

		canvas.UndoLastStroke ();
		Assert.That (canvas.Layers, Has.Count.EqualTo (1));

		canvas.RedoLastStroke ();
		Assert.That (canvas.Layers, Has.Count.EqualTo (2));
	}

	[Test]
	public void SelectAllAndDeselectUseDocumentBounds ()
	{
		using MemoryImageSurface surface = new (4, 3);
		CanvasHost canvas = new (new Pinta.Core.Size (4, 3), surface);

		Assert.That (canvas.SelectAll (), Is.True);
		Assert.That (canvas.Selection, Is.EqualTo (new RectangleI (0, 0, 4, 3)));
		canvas.ClearSelection ();
		Assert.That (canvas.Selection, Is.Null);
	}

	[Test]
	public void LayerMetadataChangesCanBeUndone ()
	{
		using MemoryImageSurface surface = new (1, 1);
		CanvasHost canvas = new (new Pinta.Core.Size (1, 1), surface);
		canvas.SetActiveLayerOpacity (0.4);
		canvas.SetLayerVisibility (0, false);
		canvas.SetLayerLocked (0, true);

		canvas.UndoLayerOperation ();
		Assert.That (canvas.Layers[0].IsLocked, Is.False);
		canvas.UndoLayerOperation ();
		Assert.That (canvas.Layers[0].IsVisible, Is.True);
		canvas.UndoLayerOperation ();
		Assert.That (canvas.Layers[0].Opacity, Is.EqualTo (1));
	}

	[Test]
	public void RenamingLayerCanBeUndoneAndRedone ()
	{
		using MemoryImageSurface surface = new (1, 1);
		CanvasHost canvas = new (new Pinta.Core.Size (1, 1), surface);

		canvas.RenameLayer ("Artwork");
		canvas.UndoLayerOperation ();
		Assert.That (canvas.Layers[0].Name, Is.EqualTo ("Background"));

		canvas.RedoLayerOperation ();
		Assert.That (canvas.Layers[0].Name, Is.EqualTo ("Artwork"));
	}


	[Test]
	public void CancelCurrentStrokeRestoresSnapshot ()
	{
		using MemoryImageSurface surface = new (3, 1);
		CanvasHost canvas = new (new Pinta.Core.Size (3, 1), surface);
		canvas.BeginStroke ();
		canvas.PaintCanvasPoint (new PointD (1, 0), ColorBgra.Red);

		canvas.CancelCurrentStroke ();

		Assert.That (surface.GetReadOnlyPixelData ().ToArray (), Is.All.EqualTo (ColorBgra.Zero));
		Assert.That (canvas.CanUndoStroke, Is.False);
	}

	[Test]
	public void CancelWithoutActiveStrokeDoesNotUndoCompletedWork ()
	{
		using MemoryImageSurface surface = new (2, 1);
		CanvasHost canvas = new (new Pinta.Core.Size (2, 1), surface);
		canvas.BeginStroke ();
		canvas.PaintCanvasPoint (new PointD (0, 0), ColorBgra.Red);
		canvas.CommitStroke ();

		canvas.CancelCurrentStroke ();

		Assert.That (surface.GetReadOnlyPixelData ()[0], Is.EqualTo (ColorBgra.Red));
		Assert.That (canvas.CanUndoStroke, Is.True);
	}

	[Test]
	public void ClearActiveLayerIsOneUndoableOperation ()
	{
		using MemoryImageSurface surface = new (2, 1);
		surface.Fill (ColorBgra.Red);
		CanvasHost canvas = new (new Pinta.Core.Size (2, 1), surface);

		Assert.That (canvas.ClearActiveLayer (), Is.True);
		Assert.That (surface.GetReadOnlyPixelData ().ToArray (), Is.All.EqualTo (ColorBgra.Zero));

		canvas.UndoLastStroke ();

		Assert.That (surface.GetReadOnlyPixelData ().ToArray (), Is.All.EqualTo (ColorBgra.Red));
	}

	[Test]
	public void ClearEmptyLayerDoesNotCreateUndoStep ()
	{
		using MemoryImageSurface surface = new (2, 1);
		CanvasHost canvas = new (new Pinta.Core.Size (2, 1), surface);

		Assert.That (canvas.ClearActiveLayer (), Is.False);
		Assert.That (canvas.CanUndoStroke, Is.False);
	}

	[Test]
	public void SwapColorsExchangesPrimaryAndSecondary ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (1, 1)) {
			BrushColor = ColorBgra.Red,
			SecondaryBrushColor = ColorBgra.Blue
		};

		canvas.SwapColors ();

		Assert.That (canvas.BrushColor, Is.EqualTo (ColorBgra.Blue));
		Assert.That (canvas.SecondaryBrushColor, Is.EqualTo (ColorBgra.Red));
	}

	[Test]
	public void SwapColorsRaisesColorsChanged ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (1, 1));
		int notifications = 0;
		canvas.ColorsChanged += (_, _) => notifications++;

		canvas.SwapColors ();

		Assert.That (notifications, Is.EqualTo (1));
	}

	[Test]
	public void SavePng_RequiresSurface ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (2, 2));

		Assert.That (() => canvas.SavePng (Path.Combine (Path.GetTempPath (), "pintora-test.png")),
			Throws.InvalidOperationException);
	}

	[Test]
	public void SaveWebp_WritesWebpSignature ()
	{
		using var surface = new MemoryImageSurface (2, 2);
		CanvasHost canvas = new (new Pinta.Core.Size (2, 2), surface);
		string path = Path.Combine (Path.GetTempPath (), $"pintora-{Guid.NewGuid ():N}.webp");
		try {
			canvas.SaveWebp (path);
			byte[] header = File.ReadAllBytes (path);
			Assert.That (header.Length, Is.GreaterThan (12));
			Assert.That (header[0..4], Is.EqualTo (new byte[] { (byte) 'R', (byte) 'I', (byte) 'F', (byte) 'F' }));
			Assert.That (header[8..12], Is.EqualTo (new byte[] { (byte) 'W', (byte) 'E', (byte) 'B', (byte) 'P' }));
		} finally { File.Delete (path); }
	}

	[Test]
	public void SaveTiff_WritesLittleEndianHeader ()
	{
		using var surface = new MemoryImageSurface (2, 2);
		CanvasHost canvas = new (new Pinta.Core.Size (2, 2), surface);
		string path = Path.Combine (Path.GetTempPath (), $"pintora-{Guid.NewGuid ():N}.tiff");
		try {
			canvas.SaveTiff (path);
			byte[] header = File.ReadAllBytes (path);
			Assert.That (header[0..4], Is.EqualTo (new byte[] { (byte) 'I', (byte) 'I', 42, 0 }));
		} finally { File.Delete (path); }
	}

	[Test]
	public void ImportSvgAsLayer_RasterizesRect ()
	{
		using var surface = new MemoryImageSurface (4, 4);
		CanvasHost canvas = new (new Pinta.Core.Size (4, 4), surface);
		string path = Path.Combine (Path.GetTempPath (), $"pintora-{Guid.NewGuid ():N}.svg");
		try {
			File.WriteAllText (path, "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"4\" height=\"4\"><rect x=\"1\" y=\"1\" width=\"2\" height=\"2\" fill=\"#ff0000\" /></svg>");
			canvas.ImportSvgAsLayer (path);
			Assert.That (canvas.Layers.Count, Is.EqualTo (2));
			Assert.That (canvas.Layers[1].Surface.GetReadOnlyPixelData ()[5], Is.EqualTo (ColorBgra.Red));
		} finally { File.Delete (path); }
	}
}
