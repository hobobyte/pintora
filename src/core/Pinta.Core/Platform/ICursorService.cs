namespace Pinta.Core;

/// <summary>Platform-neutral cursor description used by tools.</summary>
public readonly record struct CursorDescriptor (string ResourceName, int HotspotX = 0, int HotspotY = 0);

/// <summary>Creates a platform cursor from a neutral descriptor.</summary>
public interface ICursorService
{
	object? CreateCursor (CursorDescriptor descriptor);
}
