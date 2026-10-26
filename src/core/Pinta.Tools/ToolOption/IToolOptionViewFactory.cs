using Pinta.Core;

namespace Pinta.Tools;

/// <summary>
/// Creates a platform-specific view for a tool option.
/// The core tool layer treats the returned view as opaque.
/// </summary>
public interface IToolOptionViewFactory
{
	object CreateView (ToolOption option);
}
