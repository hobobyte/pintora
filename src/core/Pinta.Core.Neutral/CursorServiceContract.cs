namespace Pinta.Core;

/// <summary>Platform-neutral cursor description used by tools.</summary>
public readonly record struct CursorDescriptor (string ResourceName, int HotspotX = 0, int HotspotY = 0);

/// <summary>Creates a platform cursor from a neutral descriptor.</summary>
public interface ICursorService
{
	object? CreateCursor (CursorDescriptor descriptor);
	object? CreateShapedCursor (CursorDescriptor descriptor, CursorShape shape, double scale, int shapeWidth);

	/// <summary>
	/// Creates a cursor from the base cursor icon overlaid with a rotated neutral shape
	/// (used e.g. by the slash brush cursor). Returns null when unavailable.
	/// </summary>
	object? CreateShapedIconCursor (CursorDescriptor baseCursor, CursorShape shape, double scale, int shapeWidth, int angle);

	/// <summary>Gets the cursor currently applied to a native widget.</summary>
	object? GetWidgetCursor (object nativeWidget);

	/// <summary>Applies a native cursor (or null for the default) to a native widget.</summary>
	void SetWidgetCursor (object nativeWidget, object? cursor);
}

/// <summary>
/// A cursor rendered as a neutral shape (e.g. a circle or rectangle sized to the brush width)
/// combined with a base cursor image.
/// </summary>
public readonly record struct ShapedCursorDescriptor (
	CursorDescriptor Cursor,
	CursorShape Shape,
	double Scale,
	int ShapeWidth);

