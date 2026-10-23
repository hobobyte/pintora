using System;
using NUnit.Framework;
using Pintora.Avalonia.Canvas;

namespace Pintora.Avalonia.Tests;

[TestFixture]
internal sealed class CanvasHostTests
{
	[Test]
	public void StartsAtImageSize ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (800, 600));

		Assert.That (canvas.ImageSize, Is.EqualTo (new Pinta.Core.Size (800, 600)));
		Assert.That (canvas.ViewSize, Is.EqualTo (new Pinta.Core.Size (800, 600)));
		Assert.That (canvas.Scale, Is.EqualTo (1));
	}

	[Test]
	public void ScaleUpdatesViewSize ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (800, 600));

		canvas.SetScale (0.5);

		Assert.That (canvas.ViewSize, Is.EqualTo (new Pinta.Core.Size (400, 300)));
		Assert.That (canvas.Scale, Is.EqualTo (0.5));
	}

	[Test]
	public void InvalidScaleIsRejected ()
	{
		CanvasHost canvas = new (new Pinta.Core.Size (100, 100));

		Assert.That (() => canvas.SetScale (0), Throws.TypeOf<ArgumentOutOfRangeException> ());
	}
}
