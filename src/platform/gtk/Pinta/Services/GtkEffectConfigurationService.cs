using System.Threading.Tasks;
using Pinta.Core;
using Pinta.Effects;

namespace Pinta;

/// <summary>GTK composition-root adapter for effect configuration dialogs.</summary>
public sealed class GtkEffectConfigurationService : IEffectConfigurationService
{
	private readonly IChromeService chrome;

	public GtkEffectConfigurationService (IChromeService chrome)
	{
		this.chrome = chrome;
	}

	public async Task<bool> ConfigureAsync (BaseEffect effect)
	{
		if (effect is not AlignObjectEffect alignment)
			return false;

		using AlignmentDialog dialog = AlignmentDialog.New (chrome);
		alignment.Data.Position = dialog.SelectedPosition;
		dialog.PositionChanged += (_, _) => alignment.Data.Position = dialog.SelectedPosition;
		Gtk.ResponseType response = await dialog.RunAsync ();
		dialog.Destroy ();
		return response == Gtk.ResponseType.Ok;
	}
}
