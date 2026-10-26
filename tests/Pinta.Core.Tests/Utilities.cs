using System;
using System.IO;
using System.Runtime.InteropServices;
using Cairo;
using NUnit.Framework;
using SkiaSharp;

namespace Pinta.Core.Tests;

internal static class Utilities
{
	static Utilities ()
	{
		Cairo.Module.Initialize ();
	}

	/// <returns>
	/// <see langword="true"/> if the files with these file names
	/// are byte-for-byte the same, <see langword="false"/> if not
	/// </returns>
	internal static bool AreFilesEqual (string fileName1, string fileName2)
	{
		var context1 = OpenFile (fileName1);
		var context2 = OpenFile (fileName2);
		return AreFilesEqual (context1.DataStream, context2.DataStream); ;
	}

	internal static bool AreStreamsEqual (Stream first, Stream second)
	{
		first.Position = 0;
		second.Position = 0;
		Span<byte> buffer1 = stackalloc byte[4096];
		Span<byte> buffer2 = stackalloc byte[4096];
		while (true) {
			int read1 = first.Read (buffer1);
			int read2 = second.Read (buffer2);
			if (read1 != read2) return false;
			if (read1 == 0) return true;
			if (!buffer1[..read1].SequenceEqual (buffer2[..read2])) return false;
		}
	}

	internal static DataInputStreamContext OpenFile (string filePath)
	{
		return new (filePath);
	}

	internal static string GetAssetPath (string fileName)
	{
		const string ASSETS_FOLDER = "Assets";
		string assemblyPath = System.IO.Path.GetDirectoryName (typeof (Utilities).Assembly.Location)!;
		return System.IO.Path.Combine (assemblyPath, ASSETS_FOLDER, fileName);
	}

	internal sealed class DataInputStreamContext : IDisposable
	{
		private Gio.FileInputStream FileStream { get; }
		public Gio.DataInputStream DataStream { get; }

		internal DataInputStreamContext (string filePath)
		{
			Gio.File file = Gio.FileHelper.NewForPath (filePath);
			Gio.FileInputStream fs = file.Read (null);
			FileStream = fs;
			DataStream = Gio.DataInputStream.New (fs);
		}

		public void Dispose ()
		{
			DataStream.Dispose ();
			FileStream.Dispose ();
		}
	}

	internal static bool AreFilesEqual (Gio.DataInputStream dataStream1, Gio.DataInputStream dataStream2)
	{
		dataStream1.Seek (0, GLib.SeekType.Set, null);
		dataStream2.Seek (0, GLib.SeekType.Set, null);

		const int BUFFER_SIZE = 4096;

		Span<byte> buffer1 = stackalloc byte[BUFFER_SIZE];
		Span<byte> buffer2 = stackalloc byte[BUFFER_SIZE];

		while (true) {

			long bytesRead1 = dataStream1.Read (buffer1, null);
			long bytesRead2 = dataStream2.Read (buffer2, null);

			//Console.WriteLine ($"1: {bytesRead1} bytes read");
			//Console.WriteLine ($"2: {bytesRead2} bytes read");

			if (bytesRead1 != bytesRead2) // Different file sizes
			{
				//Console.WriteLine ("Different file sizes");
				return false;
			}

			if (bytesRead1 == 0) // End of file
			{
				//Console.WriteLine ("End of file");
				return true;
			}

			for (int i = 0; i < bytesRead1; i++) {
				if (buffer1[i] != buffer2[i]) // Differing byte
				{
					//Console.WriteLine ($"Differing byte at position {i} of buffer");
					return false;
				}
			}
		}
	}

	public static ImageSurface LoadImage (string imageFilePath)
	{
		using SKImage image = SKImage.FromEncodedData (imageFilePath) ?? throw new InvalidDataException (imageFilePath);
		SKImageInfo info = new (image.Width, image.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
		using var bitmap = new SKBitmap (info);
		if (!image.ReadPixels (info, bitmap.GetPixels (), bitmap.RowBytes, 0, 0))
			throw new InvalidDataException ($"Unable to decode {imageFilePath}");
		ImageSurface surface = CairoExtensions.CreateImageSurface (Format.Argb32, image.Width, image.Height);
		int rowBytes = image.Width * ColorBgra.SizeOf;
		byte[] bytes = new byte[rowBytes * image.Height];
		for (int y = 0; y < image.Height; y++)
			Marshal.Copy (bitmap.GetPixels () + y * bitmap.RowBytes, bytes, y * rowBytes, rowBytes);
		MemoryMarshal.Cast<byte, ColorBgra> (bytes).CopyTo (surface.GetPixelData ());
		return surface;
	}

	public static ArrayImageSurface LoadNeutralImage (string imageFilePath)
	{
		using SKImage image = SKImage.FromEncodedData (imageFilePath) ?? throw new InvalidDataException (imageFilePath);
		SKImageInfo info = new (image.Width, image.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
		using var bitmap = new SKBitmap (info);
		if (!image.ReadPixels (info, bitmap.GetPixels (), bitmap.RowBytes, 0, 0))
			throw new InvalidDataException ($"Unable to decode {imageFilePath}");

		var surface = new ArrayImageSurface (image.Width, image.Height);
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

	public static void CompareImages (IImageSurface expected, IImageSurface result, int tolerance = 1)
	{
		Assert.That (expected.Width, Is.EqualTo (result.Width));
		Assert.That (expected.Height, Is.EqualTo (result.Height));
		ReadOnlySpan<ColorBgra> expectedPixels = expected.GetReadOnlyPixelData ();
		ReadOnlySpan<ColorBgra> resultPixels = result.GetReadOnlyPixelData ();
		int diffs = 0;
		for (int i = 0; i < resultPixels.Length; i++) {
			if (!ColorBgra.ColorsWithinTolerance (resultPixels[i], expectedPixels[i], tolerance))
				diffs++;
		}
		Assert.That (diffs, Is.EqualTo (0));
	}

	public static void TestBlendOp (
		UserBlendOp blendOp,
		string sourceExpected,
		string? saveImageName = null,
		string sourceA = "visual_a.png",
		string sourceB = "visual_b.png")
	{
		string pathA = Utilities.GetAssetPath (sourceA);
		string pathB = Utilities.GetAssetPath (sourceB);
		string pathExpected = Utilities.GetAssetPath (sourceExpected);
		using ArrayImageSurface loadedA = Utilities.LoadNeutralImage (pathA);
		using ArrayImageSurface loadedB = Utilities.LoadNeutralImage (pathB);
		using ArrayImageSurface expectedOutput = Utilities.LoadNeutralImage (pathExpected);
		using ArrayImageSurface result = new (loadedA.Width, loadedB.Height);
		blendOp.Apply (result, loadedB, loadedA);

		Utilities.CompareImages (expectedOutput, result);
	}
}
