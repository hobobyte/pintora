namespace Pinta.Core;

/// <summary>
/// Platform-neutral factory boundary for tool option widgets.
/// Implementations may return a native platform widget as an opaque object;
/// Core and tools must not construct the platform widget directly.
/// </summary>
public interface IToolbarWidgetFactory
{
	object CreateDropDownButton (bool showLabel = false);
}
