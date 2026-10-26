using System;
using System.Collections.Generic;
using GdkPixbuf;

namespace Pinta.Core;

/// <summary>Current GTK/GdkPixbuf image format provider.</summary>
/// <remarks>This class is an adapter seam and can move to the GTK platform project.</remarks>
public sealed class GdkPixbufImageFormatProvider : IImageFormatProvider
{
	public IEnumerable<FormatDescriptor> GetFormats ()
	{
		foreach (var format in GdkPixbufExtensions.GetFormats ())
			yield return CreateFormatDescriptor (format);
	}

	private static FormatDescriptor CreateFormatDescriptor (PixbufFormat format)
	{
		string formatName = format.GetName ()?.ToLowerInvariant ()
			?? throw new ArgumentException ($"{nameof (format)} has an empty name");
		string[] extensions = formatName switch {
			"jpeg" => ["jpg", "jpeg", "JPG", "JPEG"],
			"tiff" => ["tif", "tiff", "TIF", "TIFF"],
			_ => [formatName, formatName.ToUpperInvariant ()],
		};
		GdkPixbufFormat importer = new (formatName);
		IImageExporter? exporter = formatName switch {
			"jpeg" => importer = new JpegFormat (),
			"tga" => new TgaExporter (),
			_ when format.IsWritable () => importer,
			_ => null,
		};
		return new FormatDescriptor (formatName.ToUpperInvariant (), extensions, format.GetMimeTypes () ?? [], importer, exporter, false);
	}
}
