using System;
using NUnit.Framework;

namespace Pinta.Core.Tests;

[TestFixture]
internal sealed class RasterAlgorithmsTests
{
	[Test]
	public void FloodFill_ChangesOnlyContiguousMatchingPixels ()
	{
		using var surface = new ArrayImageSurface (4, 1);
		ColorBgra source = ColorBgra.White;
		ColorBgra barrier = ColorBgra.Black;
		ColorBgra replacement = ColorBgra.Red;
		surface.GetPixelData ().Fill (source);
		surface.GetPixelData ()[2] = barrier;

		Assert.That (RasterAlgorithms.FloodFill (surface, new PointD (0, 0), replacement, 0), Is.True);
		ColorBgra[] pixels = surface.GetReadOnlyPixelData ().ToArray ();
		Assert.That (pixels[0], Is.EqualTo (replacement));
		Assert.That (pixels[1], Is.EqualTo (replacement));
		Assert.That (pixels[2], Is.EqualTo (barrier));
		Assert.That (pixels[3], Is.EqualTo (source));
	}

	[Test]
	public void GradientAndSampling_UseSharedCoreMath ()
	{
		using var surface = new ArrayImageSurface (2, 1);
		RasterAlgorithms.ApplyLinearGradient (surface, new PointD (0, 0), new PointD (1, 0), ColorBgra.Black, ColorBgra.White, false);

		Assert.That (RasterAlgorithms.TrySampleNearest (surface, new PointD (1, 0), out ColorBgra sampled), Is.True);
		Assert.That (sampled, Is.EqualTo (ColorBgra.White));
		Assert.That (surface.GetReadOnlyPixelData ()[0], Is.EqualTo (ColorBgra.Black));
	}

	[Test]
	public void CompositeOver_AppliesLayerOpacity ()
	{
		ColorBgra source = RasterAlgorithms.ApplyOpacity (ColorBgra.FromBgra (0, 0, 255, 255), 0.5);
		ColorBgra result = RasterAlgorithms.CompositeOver (ColorBgra.FromBgra (255, 0, 0, 255), source);

		Assert.That (result.A, Is.EqualTo (255));
		Assert.That (result.R, Is.EqualTo (128).Within (1));
		Assert.That (result.B, Is.EqualTo (127).Within (1));
	}

	[Test]
	public void AdjustColorDifference_PreservesAlphaAndClampsChannels ()
	{
		ColorBgra result = RasterAlgorithms.AdjustColorDifference (
			ColorBgra.FromBgra (20, 40, 240, 17),
			ColorBgra.FromBgra (40, 10, 250, 200),
			ColorBgra.FromBgra (5, 250, 250, 99));

		Assert.That (result, Is.EqualTo (ColorBgra.FromBgra (25, 220, 255, 99)));
	}

	[Test]
	public void TryRecolorPixel_UsesToleranceAndSharedAdjustment ()
	{
		ColorBgra basis = ColorBgra.FromBgra (20, 40, 240, 255);
		Assert.That (RasterAlgorithms.TryRecolorPixel (basis, ColorBgra.FromBgra (20, 40, 240, 255), ColorBgra.Black, ColorBgra.White, 0, out ColorBgra result), Is.True);
		Assert.That (result, Is.EqualTo (ColorBgra.Black));
		Assert.That (RasterAlgorithms.TryRecolorPixel (basis, ColorBgra.White, ColorBgra.Black, ColorBgra.White, 0, out _), Is.False);
	}

	[Test]
	public void RadialFalloffLut_HasTransparentCenterAndOpaqueOutside ()
	{
		byte[,] lut = RasterAlgorithms.CreateRadialFalloffLut (8);
		Assert.That (lut[0, 0], Is.EqualTo (0));
		Assert.That (lut[0, 8], Is.EqualTo (254));
		Assert.That (lut[8, 8], Is.EqualTo (255));
	}

	[Test]
	public void TransformAlgorithms_PreserveGestureMath ()
	{
		Assert.That (TransformAlgorithms.Translation (new PointD (1.8, 2.9), new PointD (5.2, 8.1)), Is.EqualTo (new PointD (3, 5)));
		Assert.That (TransformAlgorithms.Scale (new PointD (2, 2), new PointD (3, 4), PointD.Zero, true), Is.EqualTo (new PointD (2, 2)));
		Assert.That (TransformAlgorithms.Rotation (new PointD (1, 0), new PointD (0, 1), PointD.Zero).Radians, Is.EqualTo (Math.PI * 1.5).Within (1e-10));
		Assert.That (TransformAlgorithms.TranslationMatrix (new PointD (3, 4)).M31, Is.EqualTo (3));
		Assert.That (TransformAlgorithms.ScaleMatrix (new PointD (2, 3), new PointD (5, 7)).M31, Is.EqualTo (-5));
	}

	[Test]
	public void BrushHardnessCoverage_ClampsEdges ()
	{
		Assert.That (BrushAlgorithms.HardnessCoverage (0, 0.5), Is.EqualTo (1));
		Assert.That (BrushAlgorithms.HardnessCoverage (0.75, 0.5), Is.EqualTo (0.5).Within (1e-10));
		Assert.That (BrushAlgorithms.HardnessCoverage (2, 0.5), Is.EqualTo (0));
	}

	[Test]
	public void GridAlgorithms_UseSharedVisibilityAndClamping ()
	{
		Assert.That (GridAlgorithms.ShouldShow (true, 8, 8, 0.5), Is.False);
		Assert.That (GridAlgorithms.ShouldShow (true, 10, 10, 0.5), Is.True);
		Assert.That (GridAlgorithms.GetCellStep (1, 2, 512), Is.EqualTo (2));
		Assert.That (GridAlgorithms.GetCellStep (1000, 2, 512), Is.EqualTo (512));
	}

	[Test]
	public void SelectionAlgorithms_ClampAndRoundToImage ()
	{
		Assert.That (SelectionAlgorithms.ClampPointToImage (new PointD (-2.4, 4.6), new Size (10, 10)), Is.EqualTo (new PointD (0, 5)));
		Assert.That (SelectionAlgorithms.ClampPointToImage (new PointD (20, 20), new Size (10, 10)), Is.EqualTo (new PointD (10, 10)));
	}

	[Test]
	public void ShapeAlgorithms_ComputeBoundsAndRotation ()
	{
		RectangleD bounds = ShapeAlgorithms.Bounds (new PointD (8, 4), new PointD (2, 1));
		Assert.That (bounds, Is.EqualTo (new RectangleD (2, 1, 6, 3)));
		RectangleD rotated = ShapeAlgorithms.RotatedBounds (bounds, 90);
		Assert.That (rotated.Width, Is.EqualTo (3).Within (1e-10));
		Assert.That (rotated.Height, Is.EqualTo (6).Within (1e-10));
		Assert.That (ShapeAlgorithms.ArcSampleCount (0), Is.EqualTo (8));
		Assert.That (ShapeAlgorithms.LineSampleCount (2.1), Is.EqualTo (5));
		Assert.That (ShapeAlgorithms.ContainsEllipse (new PointD (5, 3), bounds), Is.True);
		Assert.That (ShapeAlgorithms.ContainsRoundedRectangle (new PointD (2, 1), bounds, 1), Is.False);
		Assert.That (ShapeAlgorithms.IsDashVisible (0, ShapeDashPattern.Dash, 1), Is.True);
		Assert.That (ShapeAlgorithms.IsDashVisible (7, ShapeDashPattern.Dash, 1), Is.False);
		var mask = new bool[16];
		ShapeAlgorithms.Stamp (mask, 4, 4, 0, 0, 3);
		Assert.That (mask[0], Is.True);
		Assert.That (mask[1], Is.True);
	}

	[Test]
	public void BrushAlgorithms_InterpolateLineIncludesEndpoints ()
	{
		var points = BrushAlgorithms.InterpolateLine (new PointD (0, 0), new PointD (2, 0));
		Assert.That (points.Count, Is.EqualTo (3));
		Assert.That (points[0], Is.EqualTo (new PointD (0, 0)));
		Assert.That (points[^1], Is.EqualTo (new PointD (2, 0)));
		PointD rotated = ShapeAlgorithms.RotatePoint (new PointD (1, 0), PointD.Zero, Math.PI / 2);
		Assert.That (rotated.X, Is.EqualTo (0).Within (1e-10));
		Assert.That (rotated.Y, Is.EqualTo (1).Within (1e-10));
	}

	[Test]
	public void ShapeAlgorithms_RasterizeFillAndOutline ()
	{
		using var surface = new ArrayImageSurface (8, 8);
		ShapeAlgorithms.Fill (surface, ShapePathKind.Rectangle, new RectangleD (1, 1, 4, 4), ColorBgra.Red, 0);
		ShapeAlgorithms.Outline (surface, ShapeAlgorithms.BuildPath (ShapePathKind.Rectangle, new PointD (1, 1), new PointD (5, 5), 0), ColorBgra.White, 1, ShapeDashPattern.Solid);
		Assert.That (surface.GetReadOnlyPixelData ()[1 * 8 + 1].A, Is.EqualTo (255));
		Assert.That (surface.GetReadOnlyPixelData ()[3 * 8 + 3].R, Is.EqualTo (255));
	}
}
