using System;
using System.Threading.Tasks;
using Pinta.Core;
using Pinta.Effects;

namespace Pinta;

/// <summary>GTK composition-root adapter for effect configuration dialogs.</summary>
public sealed class GtkEffectConfigurationService : IEffectConfigurationService
{
	private readonly ChromeManager chrome;

	public GtkEffectConfigurationService (IChromeService chrome)
	{
		this.chrome = (ChromeManager) chrome;
	}

	public async Task<bool> ConfigureAsync (BaseEffect effect)
	{
		if (effect is CurvesEffect curves)
			return await ConfigureCurvesAsync (curves);
		if (effect is LevelsEffect levels)
			return await ConfigureLevelsAsync (levels);
		if (effect is PosterizeEffect posterize)
			return await ConfigurePosterizeAsync (posterize);
		if (effect is not AlignObjectEffect alignment)
			return false;

		using AlignmentDialog dialog = AlignmentDialog.New (chrome);
		alignment.Data.Position = dialog.SelectedPosition;
		dialog.PositionChanged += (_, _) => alignment.Data.Position = dialog.SelectedPosition;
		Gtk.ResponseType response = await dialog.RunAsync ();
		dialog.Destroy ();
		return response == Gtk.ResponseType.Ok;
	}

	private async Task<bool> ConfigureCurvesAsync (CurvesEffect effect)
	{
		using CurvesDialog dialog = CurvesDialog.New (chrome, effect.Data);
		dialog.Title = effect.Name;
		dialog.IconName = effect.Icon;
		Gtk.ResponseType response = await dialog.RunAsync ();
		dialog.Destroy ();
		return response == Gtk.ResponseType.Ok;
	}

	private async Task<bool> ConfigureLevelsAsync (LevelsEffect effect)
	{
		IServiceProvider services = PintaCore.Services;
		using LevelsDialog dialog = LevelsDialog.New (chrome, services.GetService<IPaletteService> (), services.GetService<IWorkspaceService> (), effect.Data);
		dialog.Title = effect.Name;
		dialog.IconName = effect.Icon;
		Gtk.ResponseType response = await dialog.RunAsync ();
		dialog.Destroy ();
		return response == Gtk.ResponseType.Ok;
	}

	private async Task<bool> ConfigurePosterizeAsync (PosterizeEffect effect)
	{
		using PosterizeDialog dialog = PosterizeDialog.New (chrome);
		dialog.Title = effect.Name;
		dialog.IconName = effect.Icon;
		dialog.EffectData = effect.Data;
		Gtk.ResponseType response = await dialog.RunAsync ();
		dialog.Destroy ();
		return response == Gtk.ResponseType.Ok;
	}
}
