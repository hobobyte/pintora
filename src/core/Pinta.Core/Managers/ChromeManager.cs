// 
// ChromeManager.cs
//  
// Author:
//       Jonathan Pobst <monkey@jpobst.com>
// 
// Copyright (c) 2010 Jonathan Pobst
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Threading.Tasks;
using Mono.Addins.Localization;

namespace Pinta.Core;

public interface IChromeService
{
	Gtk.Window MainWindow { get; }

	Task<bool> LaunchSimpleEffectDialog (
		Gtk.Window parent,
		BaseEffect effect,
		IAddinLocalizer localizer,
		IWorkspaceService workspace,
		Action<string?> onChanged);
	void SetStatusBarVisible (bool visible);
	void SetMainWindowTitle (string title);
	void SetMainWindowFullscreen (bool fullscreen);
	Task LaunchUri (string uri);
}

public static class ChromeExtensions
{
	/// <remarks>Notifies of property changes, so not equivalent to an empty handler</remarks>
	[Obsolete ("Use overload with onChanged parameter instead")]
	public static Task<bool> LaunchSimpleEffectDialog (
		this IChromeService chrome,
		Gtk.Window parent,
		BaseEffect effect,
		IAddinLocalizer localizer,
		IWorkspaceService workspace)
	{
		return chrome.LaunchSimpleEffectDialog (
			parent,
			effect,
			localizer,
			workspace,
			onChanged: p => effect.EffectData?.FirePropertyChanged (p));
	}
}

public sealed class ChromeManager : IChromeService
{
	private PointI last_canvas_cursor_point;
	private bool main_window_busy;

	// NRT - These are all initialized via the Initialize* functions
	// but it would be nice to rewrite it to provably non-null.
	public Gtk.Window MainWindow { get; private set; } = null!;
	private IProgressDialog progress_dialog = null!;
	private ErrorDialogHandler error_dialog_handler = null!;
	private MessageDialogHandler message_dialog_handler = null!;
	private SimpleEffectDialogHandler simple_effect_dialog_handler = null!;

	private Action<bool>? set_status_bar_visible;
	private Action<bool>? set_toolbox_visible;
	private Action<bool>? set_tool_windows_visible;
	private Action<bool>? set_image_tabs_visible;
	private Action<bool>? set_main_toolbar_visible;
	private Action<Command>? add_application_command;
	private Action? quit_application;
	private Action<string>? set_window_title;
	private Action<bool>? set_window_fullscreen;
	private Func<string, Task>? launch_uri;

	public IProgressDialog ProgressDialog => progress_dialog;
	public Gio.Menu AdjustmentsMenu { get; private set; } = null!;
	public Gio.Menu EffectsMenu { get; private set; } = null!;

	public PointI LastCanvasCursorPoint {
		get => last_canvas_cursor_point;
		set {
			if (last_canvas_cursor_point != value) {
				last_canvas_cursor_point = value;
				OnLastCanvasCursorPointChanged ();
			}
		}
	}

	public bool MainWindowBusy {
		get => main_window_busy;
		set {
			main_window_busy = value;

			if (main_window_busy)
				MainWindow.Cursor = Gdk.Cursor.NewFromName (Pinta.Resources.StandardCursors.Progress, null);
			else
				MainWindow.Cursor = Gdk.Cursor.NewFromName (Pinta.Resources.StandardCursors.Default, null);
		}
	}

	public void InitializeWindowShell (Gtk.Window shell)
	{
		MainWindow = shell;
	}

	public void InitializeStatusBar (Action<bool> setVisible) => set_status_bar_visible = setVisible;
	public void InitializeToolBoxVisibility (Action<bool> setVisible) => set_toolbox_visible = setVisible;
	public void InitializeToolWindowsVisibility (Action<bool> setVisible) => set_tool_windows_visible = setVisible;
	public void InitializeImageTabsVisibility (Action<bool> setVisible) => set_image_tabs_visible = setVisible;
	public void InitializeMainToolBarVisibility (Action<bool> setVisible) => set_main_toolbar_visible = setVisible;
	public void InitializeApplicationCommandSink (Action<Command> addCommand) => add_application_command = addCommand;
	public void InitializeApplicationQuit (Action quit) => quit_application = quit;
	public void InitializeWindowHost (Action<string> setTitle, Action<bool> setFullscreen, Func<string, Task> launchUri)
	{
		set_window_title = setTitle;
		set_window_fullscreen = setFullscreen;
		launch_uri = launchUri;
	}

	public void InitializeMainMenu (Gio.Menu adj_menu, Gio.Menu effects_menu)
	{
		AdjustmentsMenu = adj_menu;
		EffectsMenu = effects_menu;
	}

	public void InitializeProgessDialog (IProgressDialog progressDialog)
	{
		progress_dialog = progressDialog;
	}

	public void InitializeErrorDialogHandler (ErrorDialogHandler handler)
	{
		error_dialog_handler = handler;
	}

	public void InitializeMessageDialog (MessageDialogHandler handler)
	{
		message_dialog_handler = handler;
	}

	public void InitializeSimpleEffectDialog (SimpleEffectDialogHandler handler)
	{
		simple_effect_dialog_handler = handler;
	}

	public async Task ShowErrorDialog (
		Gtk.Window parent,
		string message,
		string body,
		string details)
	{
		ErrorDialogResponse response = await error_dialog_handler (parent, message, body, details);
		switch (response) {
			case ErrorDialogResponse.Bug:
				PintaCore.Actions.Help.Bugs.Activate ();
				break;
		}
	}

	public Task ShowMessageDialog (Gtk.Window parent, string message, string body)
	{
		return message_dialog_handler (parent, message, body);
	}

	public void SetStatusBarText (string text)
	{
		OnStatusBarTextChanged (text);
	}

	public void SetStatusBarVisible (bool visible) => set_status_bar_visible?.Invoke (visible);
	public void SetToolBoxVisible (bool visible) => set_toolbox_visible?.Invoke (visible);
	public void SetToolWindowsVisible (bool visible) => set_tool_windows_visible?.Invoke (visible);
	public void SetImageTabsVisible (bool visible) => set_image_tabs_visible?.Invoke (visible);
	public void SetMainToolBarVisible (bool visible) => set_main_toolbar_visible?.Invoke (visible);
	public void AddApplicationCommand (Command command) => add_application_command?.Invoke (command);
	public void QuitApplication () => quit_application?.Invoke ();
	public void SetMainWindowTitle (string title) => set_window_title?.Invoke (title);
	public void SetMainWindowFullscreen (bool fullscreen) => set_window_fullscreen?.Invoke (fullscreen);
	public Task LaunchUri (string uri) => launch_uri?.Invoke (uri) ?? Task.CompletedTask;

	public Task<bool> LaunchSimpleEffectDialog (
		Gtk.Window parent,
		BaseEffect effect,
		IAddinLocalizer localizer,
		IWorkspaceService workspace,
		Action<string?> onChanged)

	{
		return simple_effect_dialog_handler (
			parent,
			effect,
			localizer,
			workspace,
			onChanged);
	}

	private void OnLastCanvasCursorPointChanged ()
	{
		LastCanvasCursorPointChanged?.Invoke (this, EventArgs.Empty);
	}

	private void OnStatusBarTextChanged (string text)
	{
		StatusBarTextChanged?.Invoke (this, new TextChangedEventArgs (text));
	}

	public event EventHandler? LastCanvasCursorPointChanged;
	public event EventHandler<TextChangedEventArgs>? StatusBarTextChanged;
}

public interface IProgressDialog
{
	void Show ();
	void Hide ();
	string Title { get; set; }
	string Text { get; set; }
	double Progress { get; set; }
	event EventHandler Canceled;
}

public delegate Task<ErrorDialogResponse> ErrorDialogHandler (Gtk.Window parent, string message, string body, string details);
public delegate Task MessageDialogHandler (Gtk.Window parent, string message, string body);
public delegate Task<bool> SimpleEffectDialogHandler (
	Gtk.Window parent,
	BaseEffect effect,
	IAddinLocalizer localizer,
	IWorkspaceService workspace,
	Action<string?> onChanged);
