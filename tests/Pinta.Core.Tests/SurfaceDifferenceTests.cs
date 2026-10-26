using System;
using NUnit.Framework;

namespace Pinta.Core.Tests;

[TestFixture]
internal sealed class SurfaceDifferenceTests
{
	[TestCase ("visual_minimal.png", "visual_minimal_modified.png")]
	public void Changes_Swapped_Back_And_Forth (string a, string b)
	{
		string pathA = Utilities.GetAssetPath (a);
		string pathB = Utilities.GetAssetPath (b);

		using ArrayImageSurface originalA = Utilities.LoadNeutralImage (pathA);
		using ArrayImageSurface originalB = Utilities.LoadNeutralImage (pathB);

		// Cloning "B". Can't use the "Clone" extension method because it depends on PintaCore
		using ArrayImageSurface modifiable = new (originalA.Width, originalA.Height);
		ReadOnlySpan<ColorBgra> originalBData = originalB.GetReadOnlyPixelData ();
		Span<ColorBgra> modifiableData = modifiable.GetPixelData ();
		originalBData.CopyTo (modifiableData);

		SurfaceDiff difference = SurfaceDiff.Create (originalA, originalB, force: true)!;

		difference.ApplyAndSwap (modifiable);

		Utilities.CompareImages (modifiable, originalA);

		difference.ApplyAndSwap (modifiable);

		Utilities.CompareImages (modifiable, originalB);
	}

	[TestCase ("visual_minimal.png")]
	public void Returning_Null_If_Same_Surfaces (string fileName)
	{
		string path = Utilities.GetAssetPath (fileName);
		using ArrayImageSurface image = Utilities.LoadNeutralImage (path);
		SurfaceDiff? difference = SurfaceDiff.Create (image, image);
		Assert.That (difference, Is.Null);
	}

	[TestCase ("visual_minimal.png", "visual_minimal_modified.png")]
	public void Returning_Value_If_Different_Surfaces (string a, string b)
	{
		string pathA = Utilities.GetAssetPath (a);
		string pathB = Utilities.GetAssetPath (b);
		using ArrayImageSurface imageA = Utilities.LoadNeutralImage (pathA);
		using ArrayImageSurface imageB = Utilities.LoadNeutralImage (pathB);
		SurfaceDiff? difference = SurfaceDiff.Create (imageA, imageB);
		Assert.That (difference, Is.Not.Null);
	}

	[Test]
	public void Returning_Null_If_Savings_Too_Small ()
	{
		using ArrayImageSurface empty = new (16, 16);
		using ArrayImageSurface withChanges = new (16, 16);
		withChanges.GetPixelData ()[..(16 * 15)].Fill (ColorBgra.Blue); // Savings are 16, ~6.3%
		SurfaceDiff? difference = SurfaceDiff.Create (empty, withChanges, force: false);
		Assert.That (difference, Is.Null);
	}

	[Test]
	public void Returning_Non_Null_If_Savings_Big_Enough ()
	{
		using ArrayImageSurface empty = new (16, 16);
		using ArrayImageSurface withChanges = new (16, 16);
		withChanges.GetPixelData ()[..4].Fill (ColorBgra.Blue); // Savings are 252, ~98.4%
		SurfaceDiff? difference = SurfaceDiff.Create (empty, withChanges, force: false);
		Assert.That (difference, Is.Not.Null);
	}

	[Test]
	public void Interface_Overload_Uses_Image_Surface_Contract ()
	{
		using IImageSurface originalAdapter = new ArrayImageSurface (16, 16);
		using IImageSurface updatedAdapter = new ArrayImageSurface (16, 16);

		updatedAdapter.GetPixelData ()[0] = ColorBgra.Red;
		SurfaceDiff? difference = SurfaceDiff.Create (originalAdapter, updatedAdapter);

		Assert.That (difference, Is.Not.Null);
	}
}
