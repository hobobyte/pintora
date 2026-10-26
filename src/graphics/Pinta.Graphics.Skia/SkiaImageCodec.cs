using System.IO;
using SkiaSharp;

namespace Pinta.Graphics.Skia;

public sealed class SkiaImageCodec : Pinta.Core.IImageStreamCodec, Pinta.Core.IImageFileCodec
{
	public Pinta.Core.IImageSurface Decode (Pinta.Core.IFileReference source)
	{
		using Stream stream = source.OpenRead ();
		return Decode (stream);
	}

	public void Encode (Pinta.Core.IImageSurface source, Pinta.Core.IFileReference destination)
	{
		using Stream stream = destination.OpenWrite ();
		Encode (source, stream);
	}

	public Pinta.Core.IImageSurface Decode (Stream source)
	{
		using var data = SKData.Create (source);
		using SKImage image = SKImage.FromEncodedData (data) ?? throw new InvalidDataException ("Unsupported image stream.");
		var bitmap = new SKBitmap (image.Width, image.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
		if (!image.ReadPixels (bitmap.Info, bitmap.GetPixels (), bitmap.RowBytes, 0, 0))
			throw new InvalidDataException ("Unable to decode image stream.");
		return new SkiaImageSurface (bitmap);
	}

	public void Encode (Pinta.Core.IImageSurface source, Stream destination)
	{
		if (source is not SkiaImageSurface skiaSource)
			throw new ArgumentException ("The source must be backed by SkiaSharp.", nameof (source));

		skiaSource.SyncToBitmap ();
		using var image = SKImage.FromBitmap (skiaSource.Bitmap);
		using SKData data = image.Encode (SKEncodedImageFormat.Png, 100)
			?? throw new InvalidOperationException ("Unable to encode image.");
		data.AsStream ().CopyTo (destination);
	}
}
