using System;
using System.Threading.Tasks;
using Mono.Addins.Localization;

namespace Pinta.Core;

/// <summary>GTK-only chrome helpers; the neutral <see cref="IChromeService"/> knows no window type.</summary>
public static class GtkChromeExtensions
{
	private static Gtk.Window main_window = null!; // Set by the GTK composition root on startup.

	public static Gtk.Window GetMainWindow (this IChromeService _) => main_window;

	public static void SetMainWindow (this IChromeService _, Gtk.Window window) => main_window = window;

	/// <remarks>Notifies of property changes, so not equivalent to an empty handler</remarks>
	[Obsolete ("Use overload with onChanged parameter instead")]
	public static Task<bool> LaunchSimpleEffectDialog (
		this IChromeService chrome,
		Gtk.Window parent,
		BaseEffect effect,
		IAddinLocalizer localizer,
		IWorkspaceService workspace)
		=> chrome.LaunchSimpleEffectDialog (
			effect,
			localizer,
			workspace,
			onChanged: p => effect.EffectData?.FirePropertyChanged (p));
}
