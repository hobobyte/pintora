using System.Linq;
using NUnit.Framework;
using Pinta.Core;
using Pintora.Avalonia.Canvas;
using Pintora.Avalonia.Editor.Annotations;
using Pintora.Avalonia.Imaging;

namespace Pintora.Avalonia.Tests;

[TestFixture]
internal sealed class MagicWandTests
{
	private static MemoryImageSurface Surface (int width, int height, ColorBgra fill)
	{
		var surface = new MemoryImageSurface (width, height);
		surface.GetPixelData ().Fill (fill);
		return surface;
	}

	[Test]
	public void StencilFromPointSelectsContiguousRegionOnly ()
	{
		using var surface = Surface (4, 4, ColorBgra.White);
		// A 1-pixel red barrier splits the surface; the seed is on the left.
		for (int y = 0; y < 4; y++)
			surface.GetPixelData ()[y * 4 + 2] = ColorBgra.FromBgra (0, 0, 255, 255);

		bool[] mask = FloodStencil.FromPoint (surface, 0, 0, 0);

		// Left half (x < 2) selected, right half not.
		for (int y = 0; y < 4; y++)
			for (int x = 0; x < 4; x++)
				Assert.That (mask[y * 4 + x], Is.EqualTo (x < 2), $"pixel ({x},{y})");
	}

	[Test]
	public void StencilByColorSelectsMatchingPixelsEverywhere ()
	{
		using var surface = Surface (4, 4, ColorBgra.White);
		surface.GetPixelData ()[0] = ColorBgra.Black; // top-left corner differs
		surface.GetPixelData ()[15] = ColorBgra.Black;

		bool[] mask = FloodStencil.ByColor (surface, 1, 1, 0)!;

		Assert.That (mask[0], Is.False);
		Assert.That (mask[15], Is.False);
		Assert.That (mask.Count (b => b), Is.EqualTo (14));
	}

	[Test]
	public void ToleranceBoundaryUsesGtkColorDifferenceMath ()
	{
		using var surface = Surface (2, 1, ColorBgra.White);
		// Red channel off by exactly 10: 10² = 100 <= 10²·4/4 → within tolerance 10.
		surface.GetPixelData ()[1] = ColorBgra.FromBgra (255, 255, 245, 255);

		bool[] strict = FloodStencil.FromPoint (surface, 0, 0, 0);
		Assert.That (strict[1], Is.False, "tolerance 0 must reject a 10-unit difference");

		// ColorsWithinTolerance: sum of squares <= tol*tol*4. For diffR=10 sum=100,
		// so tolerance 5 (25*4=100) accepts, matching the GTK magic wand math.
		bool[] loose = FloodStencil.FromPoint (surface, 0, 0, 5);
		Assert.That (loose[1], Is.True, "GTK tolerance 5 must accept a 10-unit single-channel difference");
	}

	[Test]
	public void ApplyMaskCombineModes ()
	{
		var tool = new SelectionTool ();
		int w = 4, h = 1;
		bool[] first = [true, true, false, false];
		Assert.That (tool.ApplyMask (first, w, h, SelectionCombineMode.Replace), Is.True);
		Assert.That (tool.Selection, Is.EqualTo (new RectangleI (0, 0, 2, 1)));

		bool[] add = [false, false, true, false];
		tool.ApplyMask (add, w, h, SelectionCombineMode.Add);
		Assert.That (tool.SelectionMask!.Count (b => b), Is.EqualTo (3));

		bool[] subtract = [false, true, false, false];
		tool.ApplyMask (subtract, w, h, SelectionCombineMode.Subtract);
		Assert.That (tool.SelectionMask!.Count (b => b), Is.EqualTo (2));

		bool[] intersect = [false, true, true, true];
		tool.ApplyMask (intersect, w, h, SelectionCombineMode.Intersect);
		Assert.That (tool.SelectionMask!.Count (b => b), Is.EqualTo (1));

		bool[] xor = [false, false, true, false];
		tool.ApplyMask (xor, w, h, SelectionCombineMode.Xor);
		Assert.That (tool.SelectionMask, Is.Null);
		Assert.That (tool.Selection, Is.Null, "empty combined mask clears the selection");
	}

	[Test]
	public void RectangleSelectReplacesMaskSelection ()
	{
		var tool = new SelectionTool ();
		bool[] mask = [true, true, true, true];
		tool.ApplyMask (mask, 2, 2, SelectionCombineMode.Replace);
		Assert.That (tool.HasMaskSelection, Is.True);

		tool.SelectRectangle (new PointI (0, 0), new PointI (1, 1), new Pinta.Core.Size (2, 2));
		Assert.That (tool.HasMaskSelection, Is.False, "rectangle select must clear the mask");
		Assert.That (tool.Selection, Is.EqualTo (new RectangleI (0, 0, 1, 1)));
	}
}
