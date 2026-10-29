//
// ViewActions.cs
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
using System.Collections.Immutable;
using System.Globalization;

namespace Pinta.Core;

public sealed class ViewActions
{
	public Command ZoomIn { get; }
	public Command ZoomOut { get; }
	public Command ZoomToWindow { get; }
	public Command ZoomToSelection { get; }
	public Command ActualSize { get; }
	public ToggleCommand ToolBar { get; }
	public ToggleCommand ImageTabs { get; }
	public ToggleCommand ToolWindows { get; }
	public Command EditCanvasGrid { get; }
	public ToggleCommand StatusBar { get; }
	public ToggleCommand ToolBox { get; }
	public ToggleCommand Rulers { get; }
	public Gio.SimpleAction RulerMetric { get; }
	public Command Fullscreen { get; }

	public IToolBarComboBox ZoomComboBox { get; }
	public ImmutableArray<string> ZoomCollection { get; }

	private string old_zoom_text = "";
	private bool zoom_to_window_activated = false;

	public bool ZoomToWindowActivated {
		get => zoom_to_window_activated;
		set {
			zoom_to_window_activated = value;
			old_zoom_text = ZoomComboBox.ActiveText!;
		}
	}

	private readonly ChromeManager chrome;
	private readonly WorkspaceManager workspace;
	public ViewActions (ChromeManager chrome, WorkspaceManager workspace)
	{
		ZoomIn = new Command (
			"ZoomIn",
			Translations.GetString ("Zoom In"),
			null,
			Resources.StandardIcons.ValueIncrease,
			shortcuts: ["<Primary>plus", "<Primary>equal", "equal", "<Primary>KP_Add", "KP_Add"]);

		ZoomOut = new Command (
			"ZoomOut",
			Translations.GetString ("Zoom Out"),
			null,
			Resources.StandardIcons.ValueDecrease,
			shortcuts: ["<Primary>minus", "<Primary>underscore", "minus", "<Primary>KP_Subtract", "KP_Subtract"]);

		ZoomToWindow = new Command (
			"ZoomToWindow",
			Translations.GetString ("Best Fit"),
			null,
			Resources.StandardIcons.ZoomFitBest,
			shortcuts: ["<Primary>B"]);

		ZoomToSelection = new Command (
			"ZoomToSelection",
			Translations.GetString ("Zoom to Selection"),
			null,
			Resources.Icons.ViewZoomSelection);

		ActualSize = new Command (
			"ActualSize",
			Translations.GetString ("Normal Size"),
			null,
			Resources.StandardIcons.ZoomOriginal,
			shortcuts: ["<Primary>0", "<Primary><Shift>A"]);

		ToolBar = new ToggleCommand (
			"Toolbar",
			Translations.GetString ("Toolbar"),
			null,
			null);

		ImageTabs = new ToggleCommand (
			"ImageTabs",
			Translations.GetString ("Image Tabs"),
			null,
			null);

		ToolWindows = new ToggleCommand (
			"ToolWindows",
			Translations.GetString ("Tool Windows"),
			null,
			null,
			shortcuts: ["F12"]);

		EditCanvasGrid = new Command (
			"EditCanvasGrid",
			Translations.GetString ("Canvas Grid..."),
			null,
			Resources.Icons.ViewGrid);

		StatusBar = new ToggleCommand (
			"Statusbar",
			Translations.GetString ("Status Bar"),
			null,
			null);

		ToolBox = new ToggleCommand (
			"ToolBox",
			Translations.GetString ("Tool Box"),
			null,
			null);

		Rulers = new ToggleCommand (
			"Rulers",
			Translations.GetString ("Rulers"),
			null,
			Resources.Icons.ViewRulers);

		RulerMetric = Gio.SimpleAction.NewStateful ( // TODO: Make `Command`
			"rulermetric",
			GLib.VariantType.New ("i"),
			GLib.Variant.NewInt32 (0));

		Fullscreen = new Command (
			"Fullscreen",
			Translations.GetString ("Fullscreen"),
			null,
			Resources.StandardIcons.DocumentNew,
			shortcuts: ["F11"]);

		ZoomCollection = default_zoom_levels;
		ZoomComboBox = PintaCore.Services.GetService<IToolbarWidgetFactory> ().CreateComboBox (90, DefaultZoomIndex (), true, ZoomCollection);

		// The toolbar is shown by default.
		ToolBar.Value = true;
		ImageTabs.Value = true;
		ToolWindows.Value = true;
		StatusBar.Value = true;
		ToolBox.Value = true;

		this.chrome = chrome;
		this.workspace = workspace;
	}

	private static readonly ImmutableArray<string> default_zoom_levels = [
		ToPercent (36),
		ToPercent (24),
		ToPercent (16),
		ToPercent (12),
		ToPercent (8),
		ToPercent (7),
		ToPercent (6),
		ToPercent (5),
		ToPercent (4),
		ToPercent (3),
		ToPercent (2),
		ToPercent (1.75),
		ToPercent (1.5),
		ToPercent (1.25),
		ToPercent (1),
		ToPercent (0.66),
		ToPercent (0.5),
		ToPercent (0.33),
		ToPercent (0.25),
		ToPercent (0.16),
		ToPercent (0.12),
		ToPercent (0.08),
		ToPercent (0.05),
		Translations.GetString ("Window")
	];

	#region Initialization

	public void RegisterActions (IActionRegistrar registrar, Gio.Menu menu)
	{
		bool mainToolbarPresent = true;

		Gio.Menu zoom_section = Gio.Menu.New ();
		zoom_section.AppendItem (ActualSize.CreateMenuItem ());
		zoom_section.AppendItem (ZoomToWindow.CreateMenuItem ());
		zoom_section.AppendItem (Fullscreen.CreateMenuItem ());

		Gio.Menu grid_section = Gio.Menu.New ();
		grid_section.AppendItem (EditCanvasGrid.CreateMenuItem ());

		Gio.Menu metric_menu = Gio.Menu.New ();
		metric_menu.Append (Translations.GetString ("Pixels"), $"app.{RulerMetric.Name}(0)");
		metric_menu.Append (Translations.GetString ("Inches"), $"app.{RulerMetric.Name}(1)");
		metric_menu.Append (Translations.GetString ("Centimeters"), $"app.{RulerMetric.Name}(2)");

		Gio.Menu metric_section = Gio.Menu.New ();
		metric_section.AppendSubmenu (Translations.GetString ("Ruler Units"), metric_menu);

		Gio.Menu show_hide_menu = Gio.Menu.New ();
		show_hide_menu.AppendItem (Rulers.CreateMenuItem ());
		show_hide_menu.AppendItem (StatusBar.CreateMenuItem ());
		show_hide_menu.AppendItem (ToolBox.CreateMenuItem ());
		show_hide_menu.AppendItem (ImageTabs.CreateMenuItem ());
		show_hide_menu.AppendItem (ToolWindows.CreateMenuItem ());
		if (mainToolbarPresent) show_hide_menu.AppendItem (ToolBar.CreateMenuItem ());

		Gio.Menu show_hide_section = Gio.Menu.New ();
		show_hide_section.AppendSubmenu (Translations.GetString ("Show/Hide"), show_hide_menu);

		menu.AppendSection (null, zoom_section);
		menu.AppendSection (null, grid_section);
		menu.AppendSection (null, metric_section);
		menu.AppendSection (null, show_hide_section);

		registrar.AddCommands ([
			ZoomIn,
			ZoomOut,
			ActualSize,
			ZoomToWindow,
			Fullscreen,
			EditCanvasGrid,
			Rulers,
			StatusBar,
			ToolBox,
			ImageTabs,
			ToolWindows,
		]);

		// TODO: Make `Command`s
		registrar.AddNativeAction (RulerMetric);

		if (mainToolbarPresent)
			registrar.AddCommand (ToolBar);
	}

	public void RegisterHandlers ()
	{
		ZoomIn.Activated += HandlePintaCoreActionsViewZoomInActivated;
		ZoomOut.Activated += HandlePintaCoreActionsViewZoomOutActivated;
		ZoomComboBox.Changed += HandlePintaCoreActionsViewZoomComboBoxComboBoxChanged;

		ZoomComboBox.FocusEntered += Entry_FocusInEvent;
		ZoomComboBox.FocusLeft += Entry_FocusOutEvent;

		ActualSize.Activated += HandlePintaCoreActionsViewActualSizeActivated;

		bool isFullscreen = false;

		Fullscreen.Activated += (foo, bar) => {
			if (!isFullscreen)
				chrome.SetMainWindowFullscreen (true);
			else
				chrome.SetMainWindowFullscreen (false);

			isFullscreen = !isFullscreen;
		};
	}

	private string? temp_zoom;
	private bool suspend_zoom_change;

	private void Entry_FocusInEvent (object o, EventArgs args)
	{
		temp_zoom = ZoomComboBox.ActiveText!;
	}

	private void Entry_FocusOutEvent (object o, EventArgs args)
	{
		string text = ZoomComboBox.ActiveText!;

		if (!TryParsePercent (text, out var percent)) {
			ZoomComboBox.SetEntryText (temp_zoom!);
			return;
		}

		if (percent > 3600)
			ZoomComboBox.Active = 0;
	}
	#endregion

	/// <summary>
	/// Converts the string representation of a percent (with or without a '%' sign) to a numeric value
	/// </summary>
	public static bool TryParsePercent (string text, out double percent)
	{
		CultureInfo culture = CultureInfo.CurrentCulture;
		NumberFormatInfo format = culture.NumberFormat;

		// In order to use double.TryParse, we must:
		// - replace the decimal separator for percents with the regular separator.
		// - remove the percent sign. We remove both the locale's percent sign and
		//   the standard one (U+0025). When running under mono, the 'fr' locale
		//   uses U+066A but the translation string uses U+0025, so there may be a bug in Mono.
		// - remove the group separators, since they might be different from the regular
		//   group separator, and the group sizes could also be different.
		string processed =
			text
			.Replace (format.PercentGroupSeparator, string.Empty)
			.Replace (format.PercentSymbol, string.Empty)
			.Replace ("%", string.Empty)
			.Replace (format.PercentDecimalSeparator, format.NumberDecimalSeparator)
			.Trim ();

		return double.TryParse (
			processed,
			NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
			culture,
			out percent);
	}

	/// <summary>
	/// Convert the given number to a percentage string using the current locale.
	/// </summary>
	public static string ToPercent (double n)
	{
		string percent = (n * 100).ToString ("N0", CultureInfo.CurrentCulture);
		// Translators: This specifies the format of the zoom percentage choices
		// in the toolbar.
		return Translations.GetString ("{0}%", percent);
	}

	public void SuspendZoomUpdate ()
	{
		suspend_zoom_change = true;
	}

	public void ResumeZoomUpdate ()
	{
		suspend_zoom_change = false;
	}

	public void UpdateCanvasScale ()
	{
		string text = ZoomComboBox.ActiveText!;

		// stay in "Zoom to Window" mode if this function was called without the zoom level being changed by the user (e.g. if the
		// image was rotated or cropped) and "Zoom to Window" mode is active
		if (text == Translations.GetString ("Window") || (ZoomToWindowActivated && old_zoom_text == text)) {
			ZoomToWindow.Activate ();
			ZoomToWindowActivated = true;
			return;
		} else {
			ZoomToWindowActivated = false;
		}


		if (!TryParsePercent (text, out var percent))
			return;

		workspace.Scale = Math.Min (percent, 3600) / 100.0;
	}

	#region Action Handlers
	private void HandlePintaCoreActionsViewActualSizeActivated (object sender, EventArgs e)
	{
		int default_zoom = DefaultZoomIndex ();
		if (ZoomComboBox.Active == default_zoom) return;
		ZoomComboBox.Active = default_zoom;
		UpdateCanvasScale ();
	}

	private void HandlePintaCoreActionsViewZoomComboBoxComboBoxChanged (object? sender, EventArgs e)
	{
		if (suspend_zoom_change)
			return;

		workspace.ActiveDocument.Workspace.ZoomManually ();
	}

	private void HandlePintaCoreActionsViewZoomOutActivated (object sender, EventArgs e)
	{
		workspace.ActiveDocument.Workspace.ZoomOut ();
	}

	private void HandlePintaCoreActionsViewZoomInActivated (object sender, EventArgs e)
	{
		workspace.ActiveDocument.Workspace.ZoomIn ();
	}
	#endregion

	/// <summary>
	/// Returns the index in the ZoomCollection of the default zoom level
	/// </summary>
	private int DefaultZoomIndex ()
	{
		return ZoomCollection.IndexOf (ToPercent (1));
	}
}
