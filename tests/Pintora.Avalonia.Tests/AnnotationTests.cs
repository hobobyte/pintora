using Avalonia;
using System.Linq;
using NUnit.Framework;
using Avalonia.Media;
using Pinta.Core;
using Pintora.Avalonia.Canvas;
using Pintora.Avalonia.Editor.Annotations;
using Pintora.Avalonia.Imaging;

namespace Pintora.Avalonia.Tests;

[TestFixture]
internal sealed class AnnotationTests
{
	[Test]
	public void ShapeAnnotationPreservesEditableGeometry ()
	{
		var annotation = new ShapeAnnotation {
			Bounds = new Rect (10, 20, 40, 30),
			Kind = CanvasHost.ShapeKind.Rectangle,
			From = new PointD (10, 20),
			To = new PointD (50, 50),
			Outline = ColorBgra.Red,
			Fill = ColorBgra.Transparent,
			FillStyle = CanvasHost.ShapeFillStyle.Outline,
			OutlineWidth = 2,
			Dash = CanvasHost.ShapeDashStyle.Solid,
			CornerRadius = 0,
			Antialias = true
		};

		Assert.That (annotation.From, Is.EqualTo (new PointD (10, 20)));
		Assert.That (annotation.To, Is.EqualTo (new PointD (50, 50)));
		Assert.That (annotation.Bounds, Is.EqualTo (new Rect (10, 20, 40, 30)));
	}

	[Test]
	public void AnnotationCollectionMaintainsOrder ()
	{
		var annotations = new AnnotationCollection ();
		var first = new ShapeAnnotation { Bounds = new Rect (0, 0, 1, 1), Kind = CanvasHost.ShapeKind.Line, From = new PointD (), To = new PointD (1, 1), Outline = ColorBgra.Black, Fill = ColorBgra.Transparent, FillStyle = CanvasHost.ShapeFillStyle.Outline, OutlineWidth = 1, Dash = CanvasHost.ShapeDashStyle.Solid };
		var second = first with { Bounds = new Rect (1, 1, 1, 1) };
		annotations.Add (first);
		annotations.Add (second);

		Assert.That (annotations.Count, Is.EqualTo (2));
		Assert.That (annotations.Items[0], Is.SameAs (first));
		Assert.That (annotations.Items[1], Is.SameAs (second));
	}

	[Test]
	public void TextAnnotationKeepsEditableTextProperties ()
	{
		var annotation = new TextAnnotation {
			Bounds = new Rect (4, 8, 120, 24),
			Text = "Hello Pintora",
			FontSize = 18,
			Color = ColorBgra.Blue,
			Underline = true
		};

		Assert.That (annotation.Text, Is.EqualTo ("Hello Pintora"));
		Assert.That (annotation.FontSize, Is.EqualTo (18));
		Assert.That (annotation.Color, Is.EqualTo (ColorBgra.Blue));
		Assert.That (annotation.Underline, Is.True);
	}

	[Test]
	public void RegionEffectAnnotationKeepsEffectParameters ()
	{
		var annotation = new RegionEffectAnnotation {
			Bounds = new Rect (12, 18, 80, 40),
			Effect = RegionEffectKind.Pixelate,
			PixelSize = 12,
			Strength = 0.75,
			PreserveAlpha = false
		};

		Assert.That (annotation.Effect, Is.EqualTo (RegionEffectKind.Pixelate));
		Assert.That (annotation.PixelSize, Is.EqualTo (12));
		Assert.That (annotation.Strength, Is.EqualTo (0.75));
		Assert.That (annotation.PreserveAlpha, Is.False);
	}

	[Test]
	public void HighlightRendererChangesOnlyTheSelectedRegion ()
	{
		using var surface = new MemoryImageSurface (4, 2);
		surface.GetPixelData ().Fill (ColorBgra.Black);
		AnnotationRenderer.Render (surface, new RegionEffectAnnotation {
			Bounds = new Rect (1, 0, 2, 2),
			Effect = RegionEffectKind.Highlight,
			HighlightColor = ColorBgra.Red,
			Strength = 1
		});

		Assert.That (surface.GetReadOnlyPixelData ()[0], Is.EqualTo (ColorBgra.Black));
		Assert.That (surface.GetReadOnlyPixelData ()[1], Is.EqualTo (ColorBgra.Red));
		Assert.That (surface.GetReadOnlyPixelData ()[3], Is.EqualTo (ColorBgra.Black));
	}

	[Test]
	public void AnnotationRendererDispatchesSupportedTypes ()
	{
		using var surface = new MemoryImageSurface (4, 2);
		var effect = new RegionEffectAnnotation {
			Bounds = new Rect (0, 0, 2, 2),
			Effect = RegionEffectKind.Highlight,
			HighlightColor = ColorBgra.Red,
			Strength = 1
		};

		Assert.That (AnnotationRenderer.TryRender (surface, effect), Is.True);
		Assert.That (surface.GetReadOnlyPixelData ()[0], Is.EqualTo (ColorBgra.Red));
		Assert.That (AnnotationRenderer.TryRender (surface, new ImageAnnotation { Bounds = new Rect (0, 0, 1, 1), SourceId = "image:1" }), Is.False);
	}

	[Test]
	public void MagnifyAndSpotlightEffectsAreRendered ()
	{
		using var magnifySurface = new MemoryImageSurface (4, 1);
		magnifySurface.GetPixelData ()[0] = ColorBgra.Red;
		magnifySurface.GetPixelData ()[1] = ColorBgra.Green;
		magnifySurface.GetPixelData ()[2] = ColorBgra.Blue;
		magnifySurface.GetPixelData ()[3] = ColorBgra.Yellow;
		AnnotationRenderer.Render (magnifySurface, new RegionEffectAnnotation {
			Bounds = new Rect (0, 0, 4, 1), Effect = RegionEffectKind.Magnify, Strength = 1
		});
		Assert.That (magnifySurface.GetReadOnlyPixelData ()[1], Is.EqualTo (ColorBgra.Green));

		using var spotlightSurface = new MemoryImageSurface (3, 1);
		spotlightSurface.GetPixelData ().Fill (ColorBgra.White);
		AnnotationRenderer.Render (spotlightSurface, new RegionEffectAnnotation {
			Bounds = new Rect (1, 0, 1, 1), Effect = RegionEffectKind.Spotlight, Strength = 1
		});
		Assert.That (spotlightSurface.GetReadOnlyPixelData ()[1], Is.EqualTo (ColorBgra.White));
		Assert.That (spotlightSurface.GetReadOnlyPixelData ()[0].R, Is.LessThan (255));
	}

	[Test]
	public void BlurAveragesWithinRegionAndLeavesOutsidePixelsUntouched ()
	{
		using var surface = new MemoryImageSurface (5, 1);
		surface.GetPixelData ()[0] = ColorBgra.Black;
		surface.GetPixelData ()[1] = ColorBgra.Red;
		surface.GetPixelData ()[2] = ColorBgra.Green;
		surface.GetPixelData ()[3] = ColorBgra.Blue;
		surface.GetPixelData ()[4] = ColorBgra.White;

		AnnotationRenderer.Render (surface, new RegionEffectAnnotation {
			Bounds = new Rect (1, 0, 3, 1),
			Effect = RegionEffectKind.Blur,
			Strength = 0
		});

		ColorBgra[] pixels = surface.GetReadOnlyPixelData ().ToArray ();
		Assert.That (pixels[0], Is.EqualTo (ColorBgra.Black));
		Assert.That (pixels[4], Is.EqualTo (ColorBgra.White));
		Assert.That (pixels[2].R, Is.EqualTo (85).Within (1));
		Assert.That (pixels[2].G, Is.EqualTo (42).Within (1));
		Assert.That (pixels[2].B, Is.EqualTo (85).Within (1));
	}

	[Test]
	public void TextMeasurementSupportsMultipleLinesAndFontFallback ()
	{
		global::Avalonia.Size oneLine = AnnotationRenderer.MeasureText ("one", 16, "font-that-does-not-exist", FontWeight.Normal);
		global::Avalonia.Size twoLines = AnnotationRenderer.MeasureText ("one\ntwo", 16, "font-that-does-not-exist", FontWeight.Normal);

		Assert.That (twoLines.Height, Is.EqualTo (oneLine.Height * 2).Within (0.01));
		Assert.That (twoLines.Width, Is.GreaterThanOrEqualTo (oneLine.Width));
	}

	[Test]
	public void TextRendererWritesVisiblePixels ()
	{
		using var surface = new MemoryImageSurface (80, 32);
		AnnotationRenderer.Render (surface, new TextAnnotation {
			Bounds = new Rect (2, 2, 60, 24),
			Text = "Pintora",
			FontSize = 18,
			Color = ColorBgra.Red
		});

		Assert.That (surface.GetReadOnlyPixelData ().ToArray ().Any (pixel => pixel.A > 0), Is.True);
	}

	[Test]
	public void ImageAndCursorAnnotationsKeepInsertionMetadata ()
	{
		var image = new ImageAnnotation { Bounds = new Rect (2, 3, 40, 30), SourceId = "clipboard:image-1", Opacity = 0.8, PreserveAspectRatio = false };
		var cursor = new CursorAnnotation { Bounds = new Rect (12, 14, 24, 24), CursorId = "arrow", Scale = 1.5, Tint = ColorBgra.Yellow };

		Assert.That (image.SourceId, Is.EqualTo ("clipboard:image-1"));
		Assert.That (image.Opacity, Is.EqualTo (0.8));
		Assert.That (image.PreserveAspectRatio, Is.False);
		Assert.That (cursor.CursorId, Is.EqualTo ("arrow"));
		Assert.That (cursor.Scale, Is.EqualTo (1.5));
		Assert.That (cursor.Tint, Is.EqualTo (ColorBgra.Yellow));
	}

	[Test]
	public void ImageAndCursorAnnotationsRenderResolvedSources ()
	{
		using var source = new MemoryImageSurface (2, 1);
		source.GetPixelData ()[0] = ColorBgra.Red;
		source.GetPixelData ()[1] = ColorBgra.Blue;
		using var surface = new MemoryImageSurface (6, 4);

		var image = new ImageAnnotation {
			Bounds = new Rect (0, 0, 6, 4), SourceId = "image:1"
		};
		Assert.That (AnnotationRenderer.TryRender (surface, image, source), Is.True);
		Assert.That (surface.GetReadOnlyPixelData ()[7], Is.EqualTo (ColorBgra.Red));
		Assert.That (surface.GetReadOnlyPixelData ()[10], Is.EqualTo (ColorBgra.Blue));

		surface.Fill (ColorBgra.Transparent);
		var cursor = new CursorAnnotation {
			Bounds = new Rect (1, 1, 4, 2), CursorId = "arrow"
		};
		Assert.That (AnnotationRenderer.TryRender (surface, cursor, source), Is.True);
		Assert.That (surface.GetReadOnlyPixelData ()[7], Is.EqualTo (ColorBgra.Red));
	}

	[Test]
	public void CursorAnnotationAppliesScaleAroundCenter ()
	{
		using var source = new MemoryImageSurface (1, 1);
		source.SetPixel (0, 0, ColorBgra.Red);
		using var surface = new MemoryImageSurface (5, 5);
		var cursor = new CursorAnnotation {
			Bounds = new Rect (2, 2, 1, 1), CursorId = "arrow", Scale = 3
		};

		AnnotationRenderer.Render (surface, cursor, source);

		Assert.That (surface.GetReadOnlyPixelData ()[2 * 5 + 2], Is.EqualTo (ColorBgra.Red));
		Assert.That (surface.GetReadOnlyPixelData ()[0], Is.EqualTo (ColorBgra.Transparent));
	}

	[Test]
	public void CursorAnnotationOffsetsBoundsByHotspot ()
	{
		using var source = new MemoryImageSurface (1, 1);
		source.SetPixel (0, 0, ColorBgra.Red);
		using var surface = new MemoryImageSurface (5, 5);
		var cursor = new CursorAnnotation {
			Bounds = new Rect (2, 2, 1, 1), CursorId = "arrow", HotspotX = 1, HotspotY = 0
		};

		AnnotationRenderer.Render (surface, cursor, source);

		Assert.That (surface.GetReadOnlyPixelData ()[2 * 5 + 1], Is.EqualTo (ColorBgra.Red));
		Assert.That (surface.GetReadOnlyPixelData ()[2 * 5 + 2], Is.EqualTo (ColorBgra.Transparent));
	}

	[Test]
	public void SelectionAnnotationKeepsCombineModeAndPoints ()
	{
		var selection = new SelectionAnnotation {
			Bounds = new Rect (3, 4, 30, 20),
			Kind = SelectionAnnotationKind.Polygon,
			CombineMode = SelectionCombineMode.Add,
			FeatherRadius = 2,
			Points = [new Point (3, 4), new Point (33, 4), new Point (20, 24)]
		};

		Assert.That (selection.CombineMode, Is.EqualTo (SelectionCombineMode.Add));
		Assert.That (selection.Points, Has.Count.EqualTo (3));
		Assert.That (selection.FeatherRadius, Is.EqualTo (2));
	}

	[Test]
	public void AnnotationCollectionRaisesChangedForMutations ()
	{
		var annotations = new AnnotationCollection ();
		int changes = 0;
		annotations.Changed += (_, _) => changes++;
		var item = new ShapeAnnotation { Bounds = new Rect (0, 0, 1, 1), Kind = CanvasHost.ShapeKind.Line, From = new PointD (), To = new PointD (1, 1), Outline = ColorBgra.Black, Fill = ColorBgra.Transparent, FillStyle = CanvasHost.ShapeFillStyle.Outline, OutlineWidth = 1, Dash = CanvasHost.ShapeDashStyle.Solid };

		annotations.Add (item);
		annotations.Replace (item, item with { Rotation = 15 });
		annotations.Remove (annotations.Items[0]);

		Assert.That (changes, Is.EqualTo (3));
	}

	[Test]
	public void AnnotationCollectionBatchesRangeChanges ()
	{
		var annotations = new AnnotationCollection ();
		int changes = 0;
		annotations.Changed += (_, _) => changes++;

		annotations.AddRange ([
			new TextAnnotation { Bounds = new Rect (0, 0, 10, 10), Text = "one" },
			new TextAnnotation { Bounds = new Rect (10, 0, 10, 10), Text = "two" }
		]);

		Assert.That (annotations.Count, Is.EqualTo (2));
		Assert.That (changes, Is.EqualTo (1));
	}
}
