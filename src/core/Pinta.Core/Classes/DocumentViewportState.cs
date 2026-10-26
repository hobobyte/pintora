using System;

namespace Pinta.Core;

/// <summary>
/// UI-independent viewport state for a document.
/// </summary>
/// <remarks>
/// GTK viewport adjustments remain in <see cref="DocumentWorkspace"/>. This type owns only the
/// image/view dimensions and scale calculation so another UI can reuse the same behavior.
/// </remarks>
public sealed class DocumentViewportState
{
	private Size image_size;
	private Size view_size;

	public DocumentViewportState (Size imageSize)
	{
		image_size = CoercedToPositive (imageSize);
		view_size = image_size;
	}

	public Size ImageSize {
		get => image_size;
		set {
			double scale = image_size.Width > 0
				? ViewSize.Width / (double) image_size.Width
				: 1;

			image_size = CoercedToPositive (value);
			ViewSize = GetViewSize (image_size, scale);
		}
	}

	public Size ViewSize {
		get => view_size;
		private set => view_size = CoercedToPositive (value);
	}

	public double Scale => ViewSize.Width / (double) ImageSize.Width;

	public void SetScale (double scale)
	{
		if (double.IsNaN (scale) || double.IsInfinity (scale) || scale <= 0)
			throw new ArgumentOutOfRangeException (nameof (scale), "Scale must be finite and positive.");

		ViewSize = GetViewSize (ImageSize, scale);
	}

	public PointD ViewPointToCanvas (PointD viewPoint)
	{
		Fraction<int> scaleFactor = ScaleFactor.CreateClamped (ViewSize.Width, ImageSize.Width);
		return scaleFactor.UnscalePoint (viewPoint);
	}

	public PointD CanvasPointToView (PointD canvasPoint)
	{
		Fraction<int> scaleFactor = ScaleFactor.CreateClamped (ViewSize.Width, ImageSize.Width);
		return scaleFactor.ScalePoint (canvasPoint);
	}

	private static Size CoercedToPositive (Size size)
		=> new (Math.Max (size.Width, 1), Math.Max (size.Height, 1));

	private static Size GetViewSize (Size imageSize, double scale)
	{
		int width = Math.Max ((int) (imageSize.Width * scale), 1);
		int height = Math.Max ((int) ((long) width * imageSize.Height / imageSize.Width), 1);
		return new (width, height);
	}
}
