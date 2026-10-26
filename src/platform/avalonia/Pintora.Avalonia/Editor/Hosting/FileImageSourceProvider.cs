using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;

namespace Pintora.Avalonia.Editor.Hosting;

public sealed class FileImageSourceProvider : IImageSourceProvider
{
	private readonly IClipboardService? clipboard;

	public FileImageSourceProvider (IClipboardService? clipboard = null)
	{
		this.clipboard = clipboard;
	}

	public Task<ImageSourceData?> GetFromClipboardAsync (CancellationToken cancellationToken = default)
		=> GetClipboardImageAsync (cancellationToken);

	private async Task<ImageSourceData?> GetClipboardImageAsync (CancellationToken cancellationToken)
	{
		if (clipboard is null)
			return null;
		Bitmap? bitmap = await clipboard.GetImageAsync (cancellationToken);
		if (bitmap is null)
			return null;
		return new ImageSourceData ("clipboard:image", bitmap, new Size (bitmap.PixelSize.Width, bitmap.PixelSize.Height));
	}

	public Task<ImageSourceData?> LoadAsync (string path, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace (path) || !File.Exists (path))
			return Task.FromResult<ImageSourceData?> (null);
		cancellationToken.ThrowIfCancellationRequested ();
		var bitmap = new Bitmap (path);
		return Task.FromResult<ImageSourceData?> (
			new ImageSourceData ($"file:{Path.GetFullPath (path)}", bitmap, new Size (bitmap.PixelSize.Width, bitmap.PixelSize.Height)));
	}
}
