using System;
using System.Threading.Tasks;
using Mono.Addins.Localization;
using Pinta.Core;

namespace Pinta.Effects;

internal sealed class MockChromeManager : IChromeService
{
	public Gtk.Window MainWindow => throw new NotImplementedException ();

	public Task<bool> LaunchSimpleEffectDialog (
		Gtk.Window parent,
		BaseEffect effect,
		IAddinLocalizer localizer,
		IWorkspaceService workspace,
		Action<string?> onChanged)
	{
		throw new NotImplementedException ();
	}

	public Task<bool> LaunchSimpleEffectDialog (BaseEffect effect, IAddinLocalizer localizer, IWorkspaceService workspace, Action<string?> onChanged) => throw new NotImplementedException ();
	public void SetStatusBarVisible (bool visible) => throw new NotImplementedException ();
	public void SetMainWindowTitle (string title) => throw new NotImplementedException ();
	public void SetMainWindowFullscreen (bool fullscreen) => throw new NotImplementedException ();
	public Task LaunchUri (string uri) => throw new NotImplementedException ();
<<<<<<< HEAD
	public object TextContext => throw new NotImplementedException ();
=======
	public Pango.Context TextContext => throw new NotImplementedException ();
>>>>>>> origin/master
	public Task ShowErrorDialog (string message, string body, string details) => throw new NotImplementedException ();
	public Task ShowMessageDialog (string message, string body) => throw new NotImplementedException ();
}
