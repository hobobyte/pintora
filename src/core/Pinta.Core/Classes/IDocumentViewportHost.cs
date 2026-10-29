namespace Pinta.Core;

/// <summary>
/// UI host operations required by a document workspace.
/// </summary>
/// <remarks>
/// The contract contains no GTK types. The current GTK canvas remains an implementation detail of
/// the application shell and can be replaced by another UI host later.
/// </remarks>
public interface IDocumentViewportHost
{
	bool ImageViewFitsInWindow { get; }
	bool ImageFitsInWindow { get; }
	PointD ViewCenter { get; }
	PointD ScrollOffset { get; }

	/// <summary>Size of the visible viewport area (inside the scrollbars).</summary>
	Size ViewportSize { get; }

	void GrabFocus ();
	void Recenter (PointD point, double scale);
	void Scroll (PointI delta);
	void SetScrollOffset (PointD offset);
	void UpdateContentSize (Size size);
}
