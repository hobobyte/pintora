namespace Pinta.Core;

/// <summary>
/// Renderer-neutral drawing API used by <see cref="IToolHandle"/> implementations.
/// All geometry is in window (view) space, at a constant on-screen size
/// (independent of the image resolution and canvas zoom).
/// Platform renderers (e.g. GTK) implement this interface and are passed to
/// <see cref="IToolHandle.Draw"/>.
/// </summary>
public interface IToolHandleRenderer
{
	/// <summary>
	/// Fill a circle with the given color.
	/// </summary>
	void FillCircle (PointD center, double radius, ColorD color);

	/// <summary>
	/// Stroke the outline of a circle with the given line width and color.
	/// </summary>
	void StrokeCircle (PointD center, double radius, double lineWidth, ColorD color);
}
