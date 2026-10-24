using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using NUnit.Framework;
using Pintora.Avalonia.Canvas;
using Pintora.Avalonia.Editor.Viewport;
using Pintora.Avalonia.Editor.Annotations;
using Pintora.Avalonia.Editor.Hosting;
using Avalonia;
using Avalonia.Media;
using Pinta.Core;
using Pintora.Avalonia.Imaging;

namespace Pintora.Avalonia.Tests;

[TestFixture]
internal sealed class CanvasHostTests
{
	[Test]
	public void CoordinateMapperRoundTripsWithOriginAndScale ()
	{
		var mapper = new CanvasCoordinateMapper (new Point (20, 30), 2.5);
		PointD canvas = mapper.ViewToCanvas (new Point (95, 80));
		Point view = mapper.CanvasToView (canvas);

		Assert.That (canvas, Is.EqualTo (new PointD (30, 20)));
		Assert.That (view, Is.EqualTo (new Point (95, 80)));
	}
	[Test]
	public void StartsAtImageSize ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (800, 600));

		Assert.That (canvas.ImageSize, Is.EqualTo (new Pinta.Core.Size (800, 600)));
		Assert.That (canvas.ViewSize, Is.EqualTo (new Pinta.Core.Size (800, 600)));
		Assert.That (canvas.Scale, Is.EqualTo (1));
	}

	[Test]
	public void ScaleUpdatesViewSize ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (800, 600));

		canvas.SetScale (0.5);

		Assert.That (canvas.ViewSize, Is.EqualTo (new Pinta.Core.Size (400, 300)));
		Assert.That (canvas.Scale, Is.EqualTo (0.5));
	}

	[Test]
	public void InvalidScaleIsRejected ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));

		Assert.That (() => canvas.SetScale (0), Throws.TypeOf<ArgumentOutOfRangeException> ());
	}

	[Test]
	public void LayerCollectionRaisesLayersChanged ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));
		int changes = 0;
		canvas.LayersChanged += (_, _) => changes++;

		canvas.AddLayer ();

		Assert.That (canvas.Layers, Has.Count.EqualTo (1));
		Assert.That (changes, Is.EqualTo (1));
	}

	[Test]
	public void LayerPropertyChangeRaisesLayersChangedAndMarksCanvasDirty ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));
		canvas.AddLayer ();
		int changes = 0;
		canvas.LayersChanged += (_, _) => changes++;

		canvas.Layers[0].Opacity = 0.5;

		Assert.That (changes, Is.EqualTo (1));
		Assert.That (canvas.IsDirty, Is.True);
	}

	[Test]
	public void AnnotationChangeDoesNotRaiseLayersChanged ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));
		canvas.AddLayer ();
		int layer_changes = 0;
		canvas.LayersChanged += (_, _) => layer_changes++;

		canvas.Layers[0].Annotations.Add (new TextAnnotation { Bounds = new Rect (0, 0, 10, 10), Text = "note" });

		Assert.That (layer_changes, Is.EqualTo (0));
		Assert.That (canvas.IsDirty, Is.True);
	}

	[Test]
	public void AddingImageAnnotationRecordsItOnActiveLayer ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));
		canvas.AddLayer ();
		using var source = new MemoryImageSurface (2, 2);

		ImageAnnotation annotation = canvas.AddImageAnnotation ("image:test", source, new Rect (10, 20, 30, 40), 0.75, false);

		Assert.That (canvas.Layers[canvas.ActiveLayerIndex].Annotations.Items, Does.Contain (annotation));
		Assert.That (annotation.SourceId, Is.EqualTo ("image:test"));
		Assert.That (annotation.Opacity, Is.EqualTo (0.75));
		Assert.That (canvas.IsDirty, Is.True);
	}

	[Test]
	public void AddingCursorAnnotationStoresScaleAndTint ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));
		canvas.AddLayer ();
		using var source = new MemoryImageSurface (1, 1);

		CursorAnnotation annotation = canvas.AddCursorAnnotation ("arrow", source, new Rect (5, 6, 10, 12), 2, ColorBgra.Red);

		Assert.That (canvas.Layers[canvas.ActiveLayerIndex].Annotations.Items, Does.Contain (annotation));
		Assert.That (annotation.Scale, Is.EqualTo (2));
		Assert.That (annotation.Tint, Is.EqualTo (ColorBgra.Red));
	}

	[Test]
	public void AddTextAnnotationStoresMetadataAndRasterizesText ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (160, 80));
		canvas.AddLayer ();

		TextAnnotation annotation = canvas.AddTextAnnotation ("Hello", new PointD (8, 10), 18, ColorBgra.Red, underline: true);

		Assert.That (annotation.Text, Is.EqualTo ("Hello"));
		Assert.That (annotation.Bounds.X, Is.EqualTo (8));
		Assert.That (annotation.FontSize, Is.EqualTo (18));
		Assert.That (annotation.Underline, Is.True);
		Assert.That (canvas.Layers[canvas.ActiveLayerIndex].Annotations.Items, Does.Contain (annotation));
		Assert.That (canvas.IsDirty, Is.True);
		Assert.That (canvas.MoveAnnotation (annotation, new Vector (4, 5)), Is.True);
		Assert.That (canvas.Layers[canvas.ActiveLayerIndex].Annotations.Items.Single ().Bounds.Position,
			Is.EqualTo (new Point (12, 15)));
		TextAnnotation moved = (TextAnnotation) canvas.Layers[canvas.ActiveLayerIndex].Annotations.Items.Single ();
		Assert.That (canvas.UpdateTextAnnotation (moved, "Longer", 24, ColorBgra.Blue, FontWeight.Bold, true), Is.True);
		TextAnnotation updated = (TextAnnotation) canvas.Layers[canvas.ActiveLayerIndex].Annotations.Items.Single ();
		Assert.That (updated.Text, Is.EqualTo ("Longer"));
		Assert.That (updated.FontSize, Is.EqualTo (24));
		Assert.That (updated.FontWeight, Is.EqualTo (FontWeight.Bold));
		canvas.UndoLastStroke ();
		TextAnnotation restored = (TextAnnotation) canvas.Layers[canvas.ActiveLayerIndex].Annotations.Items.Single ();
		Assert.That (restored.Text, Is.EqualTo ("Hello"));
		Assert.That (restored.FontSize, Is.EqualTo (18));
	}

	[Test]
	public void AnnotationCanBeMovedAndResizedThroughCanvasHost ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));
		canvas.AddLayer ();
		using var source = new MemoryImageSurface (1, 1);
		ImageAnnotation annotation = canvas.AddImageAnnotation ("image:edit", source, new Rect (10, 12, 20, 30));
		canvas.SelectAnnotationAt (new PointD (15, 17));

		Assert.That (canvas.MoveAnnotation (annotation, new Vector (4, 5)), Is.True);
		Annotation moved = canvas.Layers[canvas.ActiveLayerIndex].Annotations.Items.Single ();
		Assert.That (moved.Bounds, Is.EqualTo (new Rect (14, 17, 20, 30)));
		Assert.That (canvas.SelectedAnnotation, Is.SameAs (moved));
		Assert.That (canvas.ResizeAnnotation (moved, new global::Avalonia.Size (40, 50)), Is.True);
		Assert.That (canvas.Layers[canvas.ActiveLayerIndex].Annotations.Items.Single ().Bounds, Is.EqualTo (new Rect (14, 17, 40, 50)));
		Annotation resized = canvas.Layers[canvas.ActiveLayerIndex].Annotations.Items.Single ();
		Assert.That (canvas.RotateAnnotation (resized, Math.PI / 2), Is.True);
		Assert.That (canvas.Layers[canvas.ActiveLayerIndex].Annotations.Items.Single ().Rotation, Is.EqualTo (Math.PI / 2).Within (1e-12));
	}

	[Test]
	public void HitTestAnnotationReturnsTopmostVisibleObject ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));
		canvas.AddLayer ();
		using var source = new MemoryImageSurface (1, 1);
		ImageAnnotation bottom = canvas.AddImageAnnotation ("bottom", source, new Rect (10, 10, 40, 40));
		ImageAnnotation top = canvas.AddImageAnnotation ("top", source, new Rect (20, 20, 40, 40));

		Assert.That (canvas.HitTestAnnotation (new PointD (25, 25)), Is.SameAs (top));
		canvas.Layers[canvas.ActiveLayerIndex].IsVisible = false;
		Assert.That (canvas.HitTestAnnotation (new PointD (25, 25)), Is.Null);
		Assert.That (bottom, Is.Not.Null);
	}

	[Test]
	public void HitTestAnnotationUsesRotation ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));
		canvas.AddLayer ();
		using var source = new MemoryImageSurface (1, 1);
		ImageAnnotation annotation = canvas.AddImageAnnotation ("rotated", source, new Rect (40, 40, 20, 10));
		canvas.RotateAnnotation (annotation, Math.PI / 2);

		Annotation? hit = canvas.HitTestAnnotation (new PointD (50, 36));
		Assert.That (hit, Is.Not.Null);
		Assert.That ((hit as ImageAnnotation)?.SourceId, Is.EqualTo (annotation.SourceId));
	}

	[Test]
	public void HitTestCursorUsesHotspotAndScale ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));
		canvas.AddLayer ();
		using var source = new MemoryImageSurface (1, 1);
		CursorAnnotation cursor = canvas.AddCursorAnnotation ("arrow", source, new Rect (20, 20, 10, 10), 2, hotspotX: 2);

		Annotation? hit = canvas.HitTestAnnotation (new PointD (17, 25));
		Assert.That (hit, Is.Not.Null);
		Assert.That ((hit as CursorAnnotation)?.CursorId, Is.EqualTo (cursor.CursorId));
	}

	[Test]
	public void HitTestSupportsTextAndEffectAnnotations ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));
		canvas.AddLayer ();
		var text = new TextAnnotation { Bounds = new Rect (10, 10, 20, 20), Text = "label" };
		var effect = new RegionEffectAnnotation { Bounds = new Rect (40, 10, 20, 20), Effect = RegionEffectKind.Highlight };
		canvas.Layers[canvas.ActiveLayerIndex].Annotations.Add (text);
		canvas.Layers[canvas.ActiveLayerIndex].Annotations.Add (effect);

		Assert.That (canvas.HitTestAnnotation (new PointD (15, 15)), Is.SameAs (text));
		Assert.That (canvas.HitTestAnnotation (new PointD (45, 15)), Is.SameAs (effect));
	}

	[Test]
	public void TextAnnotationCanBeSelectedAndMovedWithUndo ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));
		canvas.AddLayer ();
		var text = new TextAnnotation { Bounds = new Rect (10, 10, 20, 20), Text = "label" };
		canvas.Layers[canvas.ActiveLayerIndex].Annotations.Add (text);

		Assert.That (canvas.SelectAnnotationAt (new PointD (15, 15)), Is.True);
		Assert.That (canvas.SelectedAnnotation, Is.SameAs (text));
		Assert.That (canvas.MoveAnnotation (text, new Vector (5, 7)), Is.True);
		Annotation moved = canvas.Layers[canvas.ActiveLayerIndex].Annotations.Items.Single ();
		Assert.That (moved.Bounds, Is.EqualTo (new Rect (15, 17, 20, 20)));

		canvas.UndoLayerOperation ();

		Assert.That (canvas.Layers[canvas.ActiveLayerIndex].Annotations.Items.Single ().Bounds, Is.EqualTo (new Rect (10, 10, 20, 20)));
	}

	[Test]
	public void SelectedEffectOptionsUpdateAnnotationAndUndo ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));
		canvas.AddLayer ();
		var effect = new RegionEffectAnnotation { Bounds = new Rect (10, 10, 20, 20), Effect = RegionEffectKind.Pixelate, Strength = 0.25, PixelSize = 8 };
		canvas.Layers[canvas.ActiveLayerIndex].Annotations.Add (effect);
		Assert.That (canvas.SelectAnnotationAt (new PointD (15, 15)), Is.True);

		Assert.That (canvas.UpdateSelectedEffectOptions (0.75, 24), Is.True);
		var updated = (RegionEffectAnnotation) canvas.Layers[canvas.ActiveLayerIndex].Annotations.Items.Single ();
		Assert.That (updated.Strength, Is.EqualTo (0.75));
		Assert.That (updated.PixelSize, Is.EqualTo (24));

		canvas.UndoLayerOperation ();
		var restored = (RegionEffectAnnotation) canvas.Layers[canvas.ActiveLayerIndex].Annotations.Items.Single ();
		Assert.That (restored.Strength, Is.EqualTo (0.25));
		Assert.That (restored.PixelSize, Is.EqualTo (8));
	}

	[Test]
	public void SelectedAnnotationCanBeDuplicatedWithUndo ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));
		canvas.AddLayer ();
		var text = new TextAnnotation { Bounds = new Rect (10, 10, 20, 20), Text = "copy" };
		canvas.Layers[canvas.ActiveLayerIndex].Annotations.Add (text);
		canvas.SelectAnnotationAt (new PointD (15, 15));

		Annotation? duplicate = canvas.DuplicateSelectedAnnotation (new Vector (3, 4));

		Assert.That (duplicate, Is.Not.Null);
		Assert.That (canvas.Layers[canvas.ActiveLayerIndex].Annotations.Items, Has.Count.EqualTo (2));
		Assert.That (canvas.SelectedAnnotation, Is.SameAs (duplicate));
		Assert.That (duplicate!.Bounds, Is.EqualTo (new Rect (13, 14, 20, 20)));

		canvas.UndoLayerOperation ();
		Assert.That (canvas.Layers[canvas.ActiveLayerIndex].Annotations.Items, Has.Count.EqualTo (1));
		Assert.That (canvas.SelectedAnnotation, Is.Null);
	}

	[Test]
	public void AnnotationSelectionTracksHitTestAndCanBeCleared ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));
		canvas.AddLayer ();
		using var source = new MemoryImageSurface (1, 1);
		ImageAnnotation annotation = canvas.AddImageAnnotation ("select", source, new Rect (10, 10, 20, 20));
		int changes = 0;
		canvas.AnnotationSelectionChanged += (_, _) => changes++;

		Assert.That (canvas.SelectAnnotationAt (new PointD (15, 15)), Is.True);
		Assert.That (canvas.SelectedAnnotation, Is.SameAs (annotation));
		canvas.ClearAnnotationSelection ();
		Assert.That (canvas.SelectedAnnotation, Is.Null);
		Assert.That (changes, Is.EqualTo (2));
	}

	[Test]
	public void SwitchingLayersClearsSelectionFromPreviousLayer ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));
		canvas.AddLayer ();
		using var source = new MemoryImageSurface (1, 1);
		ImageAnnotation annotation = canvas.AddImageAnnotation ("layer-select", source, new Rect (10, 10, 20, 20));
		Assert.That (canvas.SelectAnnotationAt (new PointD (15, 15)), Is.True);

		canvas.AddLayer ();
		canvas.SelectLayer (0);

		Assert.That (canvas.SelectedAnnotation, Is.Null);
		Assert.That (annotation, Is.Not.Null);
	}

	[Test]
	public void NewDocumentClearsAnnotationSelection ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));
		canvas.AddLayer ();
		using var source = new MemoryImageSurface (1, 1);
		canvas.AddImageAnnotation ("new-document-select", source, new Rect (10, 10, 20, 20));
		Assert.That (canvas.SelectAnnotationAt (new PointD (15, 15)), Is.True);

		canvas.NewDocument (64, 64);

		Assert.That (canvas.SelectedAnnotation, Is.Null);
	}

	[Test]
	public void AnnotationCanBeRemovedFromCanvasHost ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));
		canvas.AddLayer ();
		using var source = new MemoryImageSurface (1, 1);
		ImageAnnotation annotation = canvas.AddImageAnnotation ("remove", source, new Rect (1, 1, 5, 5));
		canvas.SelectAnnotationAt (new PointD (2, 2));

		Assert.That (canvas.RemoveAnnotation (annotation), Is.True);
		Assert.That (canvas.Layers[canvas.ActiveLayerIndex].Annotations.Items, Is.Empty);
		Assert.That (canvas.SelectedAnnotation, Is.Null);
		canvas.UndoLayerOperation ();
		Assert.That (canvas.Layers[canvas.ActiveLayerIndex].Annotations.Items, Has.Count.EqualTo (1));
		Assert.That (canvas.RemoveAnnotation (annotation), Is.True);
		Assert.That (canvas.Layers[canvas.ActiveLayerIndex].Annotations.Items, Is.Empty);
	}

	[Test]
	public void UndoClearsSelectionWhenRestoredAnnotationIsAnewInstance ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));
		canvas.AddLayer ();
		using var source = new MemoryImageSurface (1, 1);
		ImageAnnotation annotation = canvas.AddImageAnnotation ("undo-selection", source, new Rect (10, 10, 20, 20));
		Assert.That (canvas.SelectAnnotationAt (new PointD (15, 15)), Is.True);

		Assert.That (canvas.MoveAnnotation (annotation, new Vector (4, 5)), Is.True);
		Assert.That (canvas.SelectedAnnotation, Is.Not.Null);
		canvas.UndoLastStroke ();

		Assert.That (canvas.SelectedAnnotation, Is.Null);
	}

	[Test]
	public void LockedLayerRejectsAnnotationTransformsAndRemoval ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));
		canvas.AddLayer ();
		using var source = new MemoryImageSurface (1, 1);
		ImageAnnotation annotation = canvas.AddImageAnnotation ("locked", source, new Rect (1, 1, 5, 5));
		canvas.Layers[canvas.ActiveLayerIndex].IsLocked = true;

		Assert.That (canvas.MoveAnnotation (annotation, new Vector (2, 2)), Is.False);
		Assert.That (canvas.RotateAnnotation (annotation, 1), Is.False);
		Assert.That (canvas.RemoveAnnotation (annotation), Is.False);
		Assert.That (canvas.Layers[canvas.ActiveLayerIndex].Annotations.Items, Has.Count.EqualTo (1));
		Assert.That (() => canvas.AddImageAnnotation ("blocked", source, new Rect (2, 2, 4, 4)), Throws.TypeOf<InvalidOperationException> ());
	}

	[Test]
	public void RegionEffectAnnotationsCannotBeRotated ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));
		canvas.AddLayer ();
		var effect = new RegionEffectAnnotation { Bounds = new Rect (10, 10, 20, 20), Effect = RegionEffectKind.Blur };
		canvas.Layers[canvas.ActiveLayerIndex].Annotations.Add (effect);

		Assert.That (canvas.RotateAnnotation (effect, Math.PI / 4), Is.False);
		Assert.That (((RegionEffectAnnotation) canvas.Layers[canvas.ActiveLayerIndex].Annotations.Items.Single ()).Rotation, Is.Zero);
	}

	[Test]
	public void DuplicateActiveLayerCopiesAnnotationsAndClearsShapeSelection ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));
		canvas.AddLayer ();
		var text = new TextAnnotation { Bounds = new Rect (5, 6, 20, 10), Text = "copy" };
		canvas.Layers[canvas.ActiveLayerIndex].Annotations.Add (text);

		Assert.That (canvas.DuplicateActiveLayer (), Is.True);
		Assert.That (canvas.Layers, Has.Count.EqualTo (2));
		Assert.That (canvas.Layers[1].Annotations.Items, Has.Count.EqualTo (1));
		Assert.That (canvas.Layers[1].Annotations.Items.Single (), Is.EqualTo (text));
	}

	[Test]
	public void MergeActiveLayerDownRasterizesUpperAnnotations ()
	{
		using var background = new MemoryImageSurface (100, 100);
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100), background);
		canvas.AddLayer ();
		canvas.Layers[0].Opacity = 0.5;
		canvas.Layers[0].IsVisible = false;
		var text = new TextAnnotation { Bounds = new Rect (5, 6, 20, 10), Text = "merged" };
		canvas.Layers[canvas.ActiveLayerIndex].Annotations.Add (text);

		Assert.That (canvas.MergeActiveLayerDown (), Is.True);
		Assert.That (canvas.Layers, Has.Count.EqualTo (1));
		Assert.That (canvas.Layers[0].Annotations.Items, Is.Empty);
		Assert.That (canvas.Layers[0].Opacity, Is.EqualTo (1));
		Assert.That (canvas.Layers[0].IsVisible, Is.True);
	}

	[Test]
	public async Task EmptyClipboardDoesNotCreateLayer ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));

		bool pasted = await canvas.PasteImageFromClipboardAsync (new EmptyImageProvider ());

		Assert.That (pasted, Is.False);
		Assert.That (canvas.Layers, Is.Empty);
		Assert.That (canvas.IsDirty, Is.False);
	}

	private sealed class EmptyImageProvider : IImageSourceProvider
	{
		public Task<ImageSourceData?> GetFromClipboardAsync (CancellationToken cancellationToken = default)
			=> Task.FromResult<ImageSourceData?> (null);
		public Task<ImageSourceData?> LoadAsync (string path, CancellationToken cancellationToken = default)
			=> Task.FromResult<ImageSourceData?> (null);
	}
}
