using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace Pintora.Avalonia.Editor.Hosting;

public interface IClipboardService
{
	Task SetImageAsync (Bitmap image, CancellationToken cancellationToken = default);
	Task<Bitmap?> GetImageAsync (CancellationToken cancellationToken = default);
	Task SetTextAsync (string text, CancellationToken cancellationToken = default);
	Task<string?> GetTextAsync (CancellationToken cancellationToken = default);
}
