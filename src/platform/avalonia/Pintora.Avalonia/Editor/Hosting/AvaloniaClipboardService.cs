using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;

namespace Pintora.Avalonia.Editor.Hosting;

/// <summary>System clipboard adapter for Avalonia desktop hosts.</summary>
public sealed class AvaloniaClipboardService (IClipboard clipboard) : IAvaloniaClipboardService
{
	public async Task SetImageAsync (Bitmap image, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested ();
		await clipboard.SetBitmapAsync (image);
	}

	public async Task<Bitmap?> GetImageAsync (CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested ();
		return await clipboard.TryGetBitmapAsync ();
	}

	public async Task SetTextAsync (string text, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested ();
		await clipboard.SetTextAsync (text);
	}

	public async Task<string?> GetTextAsync (CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested ();
		return await clipboard.TryGetTextAsync ();
	}
}
