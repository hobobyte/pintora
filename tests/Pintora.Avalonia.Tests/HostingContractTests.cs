using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using NUnit.Framework;
using Pintora.Avalonia.Editor.Hosting;

namespace Pintora.Avalonia.Tests;

[TestFixture]
internal sealed class HostingContractTests
{
	[Test]
	public async Task ImageProviderContractCarriesStableSourceId ()
	{
		var provider = new FakeImageProvider ();
		ImageSourceData? result = await provider.GetFromClipboardAsync ();

		Assert.That (result, Is.Not.Null);
		Assert.That (result!.SourceId, Is.EqualTo ("clipboard:1"));
		Assert.That (result.Size, Is.EqualTo (new Size (2, 2)));
	}

	[Test]
	public void CursorProviderContractExposesStableIds ()
	{
		var provider = new FakeCursorProvider ();

		Assert.That (provider.GetAvailableCursors (), Is.EqualTo (new[] { "arrow" }));
		Assert.That (provider.GetCursor ("arrow")!.CursorId, Is.EqualTo ("arrow"));
	}

	[Test]
	public void DictionaryCursorProviderReturnsStableSortedIds ()
	{
		var provider = new DictionaryCursorProvider (new[] {
			new CursorSourceData ("zoom", null!, 0, 0),
			new CursorSourceData ("arrow", null!, 1, 2)
		});

		Assert.That (provider.GetAvailableCursors (), Is.EqualTo (new[] { "arrow", "zoom" }));
		Assert.That (provider.GetCursor ("arrow")!.HotspotX, Is.EqualTo (1));
		Assert.That (provider.GetCursor ("missing"), Is.Null);
	}

	[Test]
	public async Task FileImageProviderReturnsNullForMissingPath ()
	{
		var provider = new FileImageSourceProvider ();
		Assert.That (await provider.LoadAsync (Path.Combine (Path.GetTempPath (), "pintora-missing-image.png")), Is.Null);
	}

	[Test]
	public async Task FileImageProviderUsesClipboardContract ()
	{
		var provider = new FileImageSourceProvider (new EmptyClipboard ());
		Assert.That (await provider.GetFromClipboardAsync (), Is.Null);
	}

	[TestCase ("image.png", EditorExportFormat.Png)]
	[TestCase ("image.jpeg", EditorExportFormat.Jpeg)]
	[TestCase ("image.webp", EditorExportFormat.WebP)]
	[TestCase ("image.tiff", EditorExportFormat.Tiff)]
	[TestCase ("image.ora", EditorExportFormat.OpenRaster)]
	public void ExportServiceRecognizesSupportedFormats (string path, EditorExportFormat expected)
		=> Assert.That (EditorExportService.GetFormat (path), Is.EqualTo (expected));

	private sealed class FakeImageProvider : IImageSourceProvider
	{
		public Task<ImageSourceData?> GetFromClipboardAsync (CancellationToken cancellationToken = default)
			=> Task.FromResult<ImageSourceData?> (new ImageSourceData ("clipboard:1", null!, new Size (2, 2)));
		public Task<ImageSourceData?> LoadAsync (string path, CancellationToken cancellationToken = default) => Task.FromResult<ImageSourceData?> (null);
	}

	private sealed class FakeCursorProvider : ICursorProvider
	{
		public IReadOnlyList<string> GetAvailableCursors () => new[] { "arrow" };
		public CursorSourceData? GetCursor (string cursorId) => cursorId == "arrow" ? new CursorSourceData (cursorId, null!, 0, 0) : null;
	}

	private sealed class EmptyClipboard : IAvaloniaClipboardService
	{
		public Task SetImageAsync (Bitmap image, CancellationToken cancellationToken = default) => Task.CompletedTask;
		public Task<Bitmap?> GetImageAsync (CancellationToken cancellationToken = default) => Task.FromResult<Bitmap?> (null);
		public Task SetTextAsync (string text, CancellationToken cancellationToken = default) => Task.CompletedTask;
		public Task<string?> GetTextAsync (CancellationToken cancellationToken = default) => Task.FromResult<string?> (null);
	}
}
