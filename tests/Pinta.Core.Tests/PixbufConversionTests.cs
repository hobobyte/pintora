using System.Linq;
using System;
using Cairo;
using NUnit.Framework;
using Pinta.Core;

namespace Pinta.Core.Tests;

[TestFixture]
internal sealed class PixbufConversionTests
{
	[OneTimeSetUp]
	public void SetUp ()
	{
		GdkPixbufLibrary.EnsureRegistered ();
		NativeImportResolver.RegisterLibrary ("GLib",
			windowsLibraryName: "libglib-2.0-0.dll",
			linuxLibraryName: "libglib-2.0.so.0",
			osxLibraryName: "libglib-2.0.0.dylib",
			targetAssembly: typeof (GLib.Bytes).Assembly);
		NativeImportResolver.RegisterLibrary ("cairo-graphics",
			windowsLibraryName: "libcairo-2.dll",
			linuxLibraryName: "libcairo.so.2",
			osxLibraryName: "libcairo.2.dylib",
			targetAssembly: typeof (Cairo.Context).Assembly);
		NativeImportResolver.RegisterLibrary ("GObject",
			windowsLibraryName: "libgobject-2.0-0.dll",
			linuxLibraryName: "libgobject-2.0.so.0",
			osxLibraryName: "libgobject-2.0.0.dylib",
			targetAssembly: typeof (GObject.Object).Assembly);
	}

	[Test]
	public void ToImageSurfaceCopiesRgbPixels ()
	{
		using var pixbuf = GdkPixbuf.Pixbuf.New (GdkPixbuf.Colorspace.Rgb, hasAlpha: false, bitsPerSample: 8, width: 2, height: 2)!;
		// Row 0: red, green; row 1: blue, white. Written per-row to respect rowstride padding.
		byte[] row0 = [255, 0, 0, 0, 255, 0];
		byte[] row1 = [0, 0, 255, 255, 255, 255];
		System.Runtime.InteropServices.Marshal.Copy (row0, 0, (System.IntPtr) pixbuf.Pixels, row0.Length);
		System.Runtime.InteropServices.Marshal.Copy (row1, 0, (System.IntPtr) pixbuf.Pixels + pixbuf.Rowstride, row1.Length);

		using ImageSurface surface = pixbuf.ToImageSurface ();

		Assert.That (surface.Width, Is.EqualTo (2));
		Assert.That (surface.Height, Is.EqualTo (2));
		ColorBgra[] dest = surface.GetPixelData ().ToArray ();
		Assert.Multiple (() => {
			Assert.That (dest[0], Is.EqualTo (ColorBgra.FromBgra (0, 0, 255, 255)));     // red
			Assert.That (dest[1], Is.EqualTo (ColorBgra.FromBgra (0, 255, 0, 255)));     // green
			Assert.That (dest[2], Is.EqualTo (ColorBgra.FromBgra (255, 0, 0, 255)));     // blue
			Assert.That (dest[3], Is.EqualTo (ColorBgra.FromBgra (255, 255, 255, 255))); // white
		});
	}

	[Test]
	public void ToImageSurfacePremultipliesAlpha ()
	{
		using var pixbuf = GdkPixbuf.Pixbuf.New (GdkPixbuf.Colorspace.Rgb, hasAlpha: true, bitsPerSample: 8, width: 1, height: 1)!;
		// Half-alpha red: premultiplied BGRA should be (0, 0, 128, 128).
		byte[] data = [255, 0, 0, 128];
		System.Runtime.InteropServices.Marshal.Copy (data, 0, (System.IntPtr) pixbuf.Pixels, data.Length);

		using ImageSurface surface = pixbuf.ToImageSurface ();

		ColorBgra[] dest = surface.GetPixelData ().ToArray ();
		Assert.That (dest[0], Is.EqualTo (ColorBgra.FromBgra (0, 0, 128, 128)));
	}

	[Test]
	public void ToImageSurfaceHandlesRowstridePadding ()
	{
		// 3-channel, width 3 -> rowstride 12 (padded from 9); check the second row is read correctly.
		using var pixbuf = GdkPixbuf.Pixbuf.New (GdkPixbuf.Colorspace.Rgb, hasAlpha: false, bitsPerSample: 8, width: 3, height: 2)!;
		Assert.That (pixbuf!.Rowstride, Is.GreaterThan (9));
		var buffer = new byte[pixbuf.Rowstride * 2];
		// Second row, first pixel: red.
		buffer[pixbuf.Rowstride + 0] = 255;
		System.Runtime.InteropServices.Marshal.Copy (buffer, 0, (System.IntPtr) pixbuf.Pixels, buffer.Length);

		using ImageSurface surface = pixbuf.ToImageSurface ();

		ColorBgra[] dest = surface.GetPixelData ().ToArray ();
		Assert.That (dest[3], Is.EqualTo (ColorBgra.FromBgra (0, 0, 255, 255)));
	}
}
