using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;

namespace Pintora.Avalonia.Editor.Hosting;

public sealed record ImageSourceData (string SourceId, Bitmap Bitmap, Size Size);

public interface IImageSourceProvider
{
	Task<ImageSourceData?> GetFromClipboardAsync (CancellationToken cancellationToken = default);
	Task<ImageSourceData?> LoadAsync (string path, CancellationToken cancellationToken = default);
}
