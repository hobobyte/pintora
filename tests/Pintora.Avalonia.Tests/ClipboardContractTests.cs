using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using NUnit.Framework;
using Pintora.Avalonia.Editor.Hosting;

namespace Pintora.Avalonia.Tests;

[TestFixture]
internal sealed class ClipboardContractTests
{
	[Test]
	public async Task ClipboardContractSupportsTextRoundTrip ()
	{
		var clipboard = new FakeClipboardService ();
		await clipboard.SetTextAsync ("Pintora");

		Assert.That (await clipboard.GetTextAsync (), Is.EqualTo ("Pintora"));
	}

	private sealed class FakeClipboardService : IClipboardService
	{
		private string? text;
		public Task SetImageAsync (Bitmap image, CancellationToken cancellationToken = default) => Task.CompletedTask;
		public Task<Bitmap?> GetImageAsync (CancellationToken cancellationToken = default) => Task.FromResult<Bitmap?> (null);
		public Task SetTextAsync (string value, CancellationToken cancellationToken = default) { text = value; return Task.CompletedTask; }
		public Task<string?> GetTextAsync (CancellationToken cancellationToken = default) => Task.FromResult (text);
	}
}
