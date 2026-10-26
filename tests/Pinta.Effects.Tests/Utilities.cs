using System;
using System.Runtime.InteropServices;
using Cairo;
using NUnit.Framework;
using Pinta.Core;
using SkiaSharp;

namespace Pinta.Effects.Tests;

internal static class Utilities
{
	static Utilities ()
	{
		Cairo.Module.Initialize ();
	}

	public static IServiceProvider CreateMockServices ()
	{
		Pinta.Core.Size imageSize = new (250, 250);

		ServiceManager manager = new ();
		manager.AddService<IPaletteService> (new MockPalette ());
		manager.AddService<IChromeService> (new MockChromeManager ());
		manager.AddService<IWorkspaceService> (new MockWorkspaceService (imageSize));
		manager.AddService<ISystemService> (new MockSystemService ());
		manager.AddService<ILivePreview> (new MockLivePreview (new RectangleI (0, 0, imageSize.Width, imageSize.Height)));
		return manager;
	}

	public static ImageSurface LoadImage (string image_name)
	{
		string assembly_path = System.IO.Path.GetDirectoryName (typeof (Utilities).Assembly.Location)!;
		string path = System.IO.Path.Combine (assembly_path, "Assets", image_name);
		using SKImage image = SKImage.FromEncodedData (path)!;
		SKImageInfo info = new (image.Width, image.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
		using var bitmap = new SKBitmap (info);
		image.ReadPixels (info, bitmap.GetPixels (), bitmap.RowBytes, 0, 0);
		var surface = new ImageSurface (Format.Argb32, image.Width, image.Height);
		int rowBytes = image.Width * ColorBgra.SizeOf;
		byte[] bytes = new byte[rowBytes * image.Height];
		for (int y = 0; y < image.Height; y++)
			Marshal.Copy (bitmap.GetPixels () + y * bitmap.RowBytes, bytes, y * rowBytes, rowBytes);
		MemoryMarshal.Cast<byte, ColorBgra> (bytes).CopyTo (surface.GetPixelData ());
		return surface;
	}

	public static void CompareImages (
		ImageSurface result,
		ImageSurface expected,
		int tolerance = 1)
	{
		Assert.That (expected.GetSize (), Is.EqualTo (result.GetSize ()));

		var result_pixels = result.GetReadOnlyPixelData ();
		var expected_pixels = expected.GetReadOnlyPixelData ();

		int diffs = 0;
		for (int i = 0; i < result_pixels.Length; ++i) {

			if (ColorBgra.ColorsWithinTolerance (result_pixels[i], expected_pixels[i], tolerance))
				continue;

			++diffs;

			// Display info about the first few failures.
			if (diffs <= 10)
				Assert.Warn ($"Difference at pixel {i}, got {result_pixels[i]} vs {expected_pixels[i]}, diff. of {ColorBgra.ColorDifference (result_pixels[i], expected_pixels[i])}");
		}

		Assert.That (diffs, Is.EqualTo (0));
	}

	public static void TestEffect (
		BaseEffect effect,
		string result_image_name,
		string? save_image_name = null,
		string source_image_name = "input.png")
	{
		using ImageSurface source = Utilities.LoadImage (source_image_name);
		using ImageSurface result = CairoExtensions.CreateImageSurface (Format.Argb32, source.Width, source.Height);
		using ImageSurface expected = LoadImage (result_image_name);

		effect.Render (source, result, [source.GetBounds ()]);

		// For debugging, optionally save out the result to a file.
		if (save_image_name != null)
			result.ToPixbuf ().Savev (
				save_image_name,
				"png",
				[],
				[]);

		CompareImages (result, expected);
	}
}
