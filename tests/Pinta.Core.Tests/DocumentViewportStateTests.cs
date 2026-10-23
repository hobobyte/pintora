using System;
using NUnit.Framework;

namespace Pinta.Core.Tests;

[TestFixture]
internal sealed class DocumentViewportStateTests
{
	[Test]
	public void Scale_UpdatesViewSizeWithoutChangingImageSize ()
	{
		DocumentViewportState state = new (new Size (800, 600));

		state.SetScale (0.5);

		Assert.That (state.ImageSize, Is.EqualTo (new Size (800, 600)));
		Assert.That (state.ViewSize, Is.EqualTo (new Size (400, 300)));
		Assert.That (state.Scale, Is.EqualTo (0.5));
	}

	[Test]
	public void ImageSizeChange_PreservesScale ()
	{
		DocumentViewportState state = new (new Size (800, 600));
		state.SetScale (2);

		state.ImageSize = new Size (300, 200);

		Assert.That (state.ViewSize, Is.EqualTo (new Size (600, 400)));
	}

	[Test]
	public void Coordinates_RoundTripThroughViewportScale ()
	{
		DocumentViewportState state = new (new Size (800, 600));
		state.SetScale (2);

		PointD canvasPoint = new (120, 80);
		PointD viewPoint = state.CanvasPointToView (canvasPoint);

		Assert.That (viewPoint, Is.EqualTo (new PointD (240, 160)));
		Assert.That (state.ViewPointToCanvas (viewPoint), Is.EqualTo (canvasPoint));
	}

	[Test]
	public void Coordinates_RoundTripAtDownscale ()
	{
		DocumentViewportState state = new (new Size (800, 600));
		state.SetScale (0.5);

		PointD canvasPoint = new (120, 80);
		PointD viewPoint = state.CanvasPointToView (canvasPoint);

		Assert.That (viewPoint, Is.EqualTo (new PointD (60, 40)));
		Assert.That (state.ViewPointToCanvas (viewPoint), Is.EqualTo (canvasPoint));
	}

	[Test]
	public void InvalidDimensionsAreCoercedToOnePixel ()
	{
		DocumentViewportState state = new (new Size (0, -10));

		Assert.That (state.ImageSize, Is.EqualTo (new Size (1, 1)));
		Assert.That (state.ViewSize, Is.EqualTo (new Size (1, 1)));
	}

	[TestCase (0)]
	[TestCase (-1)]
	[TestCase (double.NaN)]
	[TestCase (double.PositiveInfinity)]
	public void SetScale_RejectsInvalidValues (double scale)
	{
		DocumentViewportState state = new (new Size (100, 100));

		Assert.That (() => state.SetScale (scale), Throws.TypeOf<ArgumentOutOfRangeException> ());
	}
}
