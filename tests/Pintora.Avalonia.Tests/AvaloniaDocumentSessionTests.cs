using NUnit.Framework;
using Pintora.Avalonia.Document;

namespace Pintora.Avalonia.Tests;

[TestFixture]
internal sealed class AvaloniaDocumentSessionTests
{
	[Test]
	public void SessionOwnsViewportState ()
	{
		AvaloniaDocumentSession session = new (new Pinta.Core.Size (800, 600));

		session.SetScale (2);

		Assert.That (session.ImageSize, Is.EqualTo (new Pinta.Core.Size (800, 600)));
		Assert.That (session.ViewSize, Is.EqualTo (new Pinta.Core.Size (1600, 1200)));
		Assert.That (session.Scale, Is.EqualTo (2));
	}

	[Test]
	public void LayersCanBeAddedSelectedAndRemoved ()
	{
		var surface = new Pintora.Avalonia.Imaging.MemoryImageSurface (800, 600);
		AvaloniaDocumentSession session = new (new Pinta.Core.Size (800, 600), surface);

		AvaloniaLayer layer = session.AddLayer ("Paint");
		session.SelectLayer (0);

		Assert.That (session.Layers, Has.Count.EqualTo (2));
		Assert.That (session.ActiveLayer, Is.SameAs (session.Layers[0]));
		Assert.That (session.RemoveActiveLayer (), Is.True);
		Assert.That (session.Layers, Has.Count.EqualTo (1));
		Assert.That (layer.Surface.Width, Is.EqualTo (800));
	}

	[Test]
	public void LayerOpacityCanBeChanged ()
	{
		var session = new AvaloniaDocumentSession (new Pinta.Core.Size (2, 2), new Pintora.Avalonia.Imaging.MemoryImageSurface (2, 2));
		AvaloniaLayer layer = session.AddLayer ("Overlay");

		layer.Opacity = 0.35;

		Assert.That (layer.Opacity, Is.EqualTo (0.35));
	}
}
