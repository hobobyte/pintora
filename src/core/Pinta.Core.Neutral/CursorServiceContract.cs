namespace Pinta.Core;

/// <summary>Platform-neutral cursor description used by tools.</summary>
public readonly record struct CursorDescriptor (string ResourceName, int HotspotX = 0, int HotspotY = 0);

/// <summary>Creates a platform cursor from a neutral descriptor.</summary>
public interface ICursorService
{
	object? CreateCursor (CursorDescriptor descriptor);
	object? CreateShapedCursor (CursorDescriptor descriptor, CursorShape shape, double scale, int shapeWidth);
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

