using System;
using System.IO;

namespace Pintora.Avalonia.Editor.Hosting;

public enum EditorExportFormat { Png, Jpeg, WebP, Tiff, OpenRaster }

public static class EditorExportService
{
	public static EditorExportFormat GetFormat (string path) => Path.GetExtension (path).ToLowerInvariant () switch {
		".jpg" or ".jpeg" => EditorExportFormat.Jpeg,
		".webp" => EditorExportFormat.WebP,
		".tif" or ".tiff" => EditorExportFormat.Tiff,
		".ora" => EditorExportFormat.OpenRaster,
		".png" => EditorExportFormat.Png,
		_ => throw new ArgumentException ($"Unsupported export extension: {path}", nameof (path))
	};

	public static string DefaultExtension (EditorExportFormat format) => format switch {
		EditorExportFormat.Png => ".png", EditorExportFormat.Jpeg => ".jpg", EditorExportFormat.WebP => ".webp",
		EditorExportFormat.Tiff => ".tiff", EditorExportFormat.OpenRaster => ".ora",
		_ => throw new ArgumentOutOfRangeException (nameof (format))
	};
}
