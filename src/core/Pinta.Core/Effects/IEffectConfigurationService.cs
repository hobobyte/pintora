using System.Threading.Tasks;

namespace Pinta.Core;

/// <summary>
/// Platform boundary for interactive effect configuration.
/// Implementations own the presentation (GTK, Avalonia, or another UI toolkit).
/// </summary>
public interface IEffectConfigurationService
{
	Task<bool> ConfigureAsync (BaseEffect effect);
}
