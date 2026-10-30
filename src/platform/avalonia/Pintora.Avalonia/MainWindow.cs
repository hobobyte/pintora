using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Pinta.Core;
using Pinta.Application;
using Pintora.Avalonia.Canvas;
using Pintora.Avalonia.Document;
using Pintora.Avalonia.Editor.Commands;
using Pintora.Avalonia.Editor.Hosting;
using Pintora.Avalonia.Editor.Annotations;
using Pintora.Avalonia.Imaging;
using Pintora.Avalonia.Input;

namespace Pintora.Avalonia;

public sealed class MainWindow : Window
{
	private readonly PintoraKeyboardSettings keyboard;
	private readonly SettingsManager settings;
	private readonly PintoraViewSettings view_settings;
	private readonly string version;
	private readonly IImageSourceProvider image_source_provider;
	private readonly IAvaloniaClipboardService? clipboard_service;
	private readonly List<ColorBgra> recent_colors = new ();
	private readonly List<ColorBgra> palette_colors = new ();
	private LayerThumbnailProvider? thumbnail_provider;
	private EventHandler<LayerThumbnailUpdatedEventArgs>? thumbnail_updated_handler;
	private Action? refresh_layers;
	private Action? rebuild_layers_panel;
	private StackPanel? tool_options;
	private global::Avalonia.Controls.Primitives.ToggleButton? antialias_toggle;
	private readonly EditorCommandRegistry commands = new ();
	private readonly DocumentTabManager tab_manager;
	private readonly DocumentCloseCoordinator close_coordinator;
	private readonly DocumentTabStrip tab_strip = new ();
	private readonly ContentControl canvas_host = new ();
	private Control? toolbar_control;
	private Control? toolbox_control;
	private Control? status_bar_control;
	private Control? palette_control;
	private Control? layers_control;
	private Control? tab_strip_host;
	private Border? canvas_panel_control;
	private bool closing_after_confirmation;
	private bool close_in_progress;
	private CanvasHost? subscribed_canvas;
	private Action? refresh_status;
	private Action? refresh_history_commands;
	private Action? refresh_zoom;
	private CanvasHost.CanvasTool active_tool = CanvasHost.CanvasTool.Brush;
	private List<(Button Button, CanvasHost.CanvasTool Tool)>? tool_buttons;
	private static readonly CanvasHost.CanvasTool[] EffectTools = [CanvasHost.CanvasTool.Blur, CanvasHost.CanvasTool.Pixelate, CanvasHost.CanvasTool.Highlight, CanvasHost.CanvasTool.Magnify, CanvasHost.CanvasTool.Spotlight];

	public MainWindow (PintaApplication application)
	{
		version = typeof (MainWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute> ()?.InformationalVersion
			?? "0.0.1-alpha";
		int buildMetadata = version.IndexOf ('+');
		if (buildMetadata >= 0)
			version = version[..buildMetadata];
		Title = $"Pintora {version} — Simple. Powerful. Creative.";
		Width = 1200;
		Height = 760;
		MinWidth = 900;
		MinHeight = 600;

		settings = application.GetService<SettingsManager> ();
		App.InitializeTheme (settings);
		keyboard = new PintoraKeyboardSettings (settings);
		view_settings = new PintoraViewSettings (settings);
		CanvasHost canvas = CreateCanvasHost (keyboard);
		// Close prompts perform the save asynchronously before asking the manager
		// to remove the tab; the synchronous delegate is therefore a final guard.
		tab_manager = new DocumentTabManager (_ => true, tab => tab.Canvas.Dispose ());
		close_coordinator = new DocumentCloseCoordinator (tab_manager, PromptCloseDecisionAsync, SaveTabAsync);
		tab_manager.AddTab (canvas, canvas.DocumentSession);
		tab_manager.ActiveTabChanged += (_, _) => {
			CanvasHost active = ActiveCanvas;
			ApplyGridViewSettings (active);
			active.ShowRulers = view_settings.ShowRulers;
			active.ActiveTool = active_tool;
			if (tool_buttons is not null)
				UpdateToolButtons (tool_buttons, active_tool);
			RefreshToolOptions (active);
			SyncActiveCanvas ();
			RebindActiveCanvas ();
			rebuild_layers_panel?.Invoke ();
		};
		SyncActiveCanvas ();
		thumbnail_provider = new LayerThumbnailProvider (canvas);
		clipboard_service = Clipboard is null ? null : new AvaloniaClipboardService (Clipboard);
		if (Clipboard is not null)
			application.Services.ReplaceService<Pinta.Core.IClipboardService> (new AvaloniaNeutralClipboardService (Clipboard));
		application.Services.ReplaceService<Pinta.Core.IFileDialogService> (new AvaloniaFileDialogService (this));
		image_source_provider = new FileImageSourceProvider (clipboard_service);
		UpdateDocumentTitle (canvas, version);
		canvas.BrushColor = LoadColor ("Pintora.Palette.Primary", ColorBgra.Black);
		canvas.SecondaryBrushColor = LoadColor ("Pintora.Palette.Secondary", ColorBgra.White);
		LoadRecentColors ();
		LoadPaletteColors ();
		canvas.BrushSize = settings.GetSetting ("Pintora.Brush.Size", 1);
		canvas.BrushHardness = settings.GetSetting ("Pintora.Brush.Hardness", 1.0);
		canvas.BrushOpacity = settings.GetSetting ("Pintora.Brush.Opacity", 1.0);
		canvas.FillTolerance = settings.GetSetting ("Pintora.Fill.Tolerance", 0);
		canvas.ShapeFilled = settings.GetSetting ("Pintora.Shape.Filled", false);
		if (Enum.TryParse (settings.GetSetting ("Pintora.Shape.FillStyle", canvas.ShapeFilled ? "Fill" : "Outline"), out CanvasHost.ShapeFillStyle saved_fill_style)
			&& Enum.IsDefined (saved_fill_style))
			canvas.FillStyle = saved_fill_style;
		canvas.ShapeOutlineWidth = settings.GetSetting ("Pintora.Shape.OutlineWidth", 2);
		canvas.ShapeCornerRadius = settings.GetSetting ("Pintora.Shape.CornerRadius", 8);
		canvas.ShapeAntialias = settings.GetSetting ("Pintora.Shape.Antialias", true);
		if (Enum.TryParse (settings.GetSetting ("Pintora.Shape.DashStyle", "Solid"), out CanvasHost.ShapeDashStyle saved_dash_style)
			&& Enum.IsDefined (saved_dash_style))
			canvas.DashStyle = saved_dash_style;
		if (Enum.TryParse (settings.GetSetting ("Pintora.Shape.Kind", "Rectangle"), out CanvasHost.ShapeKind saved_shape)
			&& Enum.IsDefined (saved_shape))
			canvas.SelectedShape = saved_shape;
		canvas.GradientReversed = settings.GetSetting ("Pintora.Gradient.Reversed", false);
		foreach (CanvasHost.CanvasTool effect in EffectTools) {
			EffectToolOptions options = canvas.GetEffectOptions (effect);
			options.Strength = settings.GetSetting ($"Pintora.Effect.{effect}.Strength", options.Strength);
			options.PixelSize = settings.GetSetting ($"Pintora.Effect.{effect}.PixelSize", options.PixelSize);
		}
		commands.Register (PintoraKeyCommands.Undo, canvas.UndoLastStroke, () => canvas.CanUndoStroke);
		commands.Register (PintoraKeyCommands.Redo, canvas.RedoLastStroke, () => canvas.CanRedoStroke);
		commands.Register (PintoraKeyCommands.ClearHistory, canvas.ClearHistory);
		commands.Register (PintoraKeyCommands.Save, () => _ = SaveActiveTabAsync ());
		commands.Register (PintoraKeyCommands.SaveAll, () => _ = SaveAllDocumentsAsync ());
		commands.Register (PintoraKeyCommands.CloseDocument, () => _ = CloseActiveTabAsync ());
		commands.Register (PintoraKeyCommands.Exit, () => _ = ExitAsync ());
		string saved_tool_name = settings.GetSetting ("Pintora.Tool", "Brush");
		if (Enum.TryParse (saved_tool_name, out CanvasHost.ShapeKind legacy_shape)) {
			canvas.SelectedShape = legacy_shape;
			active_tool = CanvasHost.CanvasTool.Shapes;
		} else if (Enum.TryParse (saved_tool_name, out CanvasHost.CanvasTool saved_tool) && Enum.IsDefined (saved_tool))
			active_tool = saved_tool;
		canvas.ActiveTool = active_tool;
		Closing += OnWindowClosing;
		Closed += (_, _) => {
			if (subscribed_canvas is not null) {
				subscribed_canvas.HistoryChanged -= OnActiveCanvasHistoryChanged;
				subscribed_canvas.ScaleChanged -= OnActiveCanvasScaleChanged;
				subscribed_canvas.ActiveToolChanged -= OnActiveCanvasToolOptionsChanged;
				subscribed_canvas.SelectedShapeChanged -= OnActiveCanvasToolOptionsChanged;
				subscribed_canvas.BrushSizeChanged -= OnActiveCanvasToolOptionsChanged;
				subscribed_canvas.PointerPositionChanged -= OnActiveCanvasToolChanged;
				subscribed_canvas.ColorsChanged -= OnActiveCanvasColorsChanged;
				subscribed_canvas.LayersChanged -= OnActiveCanvasLayersChanged;
				subscribed_canvas.ActiveLayerChanged -= OnActiveCanvasLayersChanged;
				subscribed_canvas.ToolOptionsChanged -= OnActiveCanvasToolOptionsChanged;
				subscribed_canvas.AnnotationSelectionChanged -= OnActiveCanvasToolOptionsChanged;
				subscribed_canvas.DocumentStateChanged -= OnActiveCanvasDocumentStateChanged;
				subscribed_canvas = null;
			}
			if (thumbnail_provider is not null && thumbnail_updated_handler is not null)
				thumbnail_provider.ThumbnailUpdated -= thumbnail_updated_handler;
			thumbnail_updated_handler = null;
			thumbnail_provider?.Dispose ();
			thumbnail_provider = null;
			settings.PutSetting ("Pintora.Brush.Size", canvas.BrushSize);
			settings.PutSetting ("Pintora.Brush.Hardness", canvas.BrushHardness);
			settings.PutSetting ("Pintora.Brush.Opacity", canvas.BrushOpacity);
			settings.PutSetting ("Pintora.Fill.Tolerance", canvas.FillTolerance);
			settings.PutSetting ("Pintora.Shape.Filled", canvas.ShapeFilled);
			settings.PutSetting ("Pintora.Shape.FillStyle", canvas.FillStyle.ToString ());
			settings.PutSetting ("Pintora.Shape.OutlineWidth", canvas.ShapeOutlineWidth);
			settings.PutSetting ("Pintora.Shape.CornerRadius", canvas.ShapeCornerRadius);
			settings.PutSetting ("Pintora.Shape.Antialias", canvas.ShapeAntialias);
			settings.PutSetting ("Pintora.Shape.DashStyle", canvas.DashStyle.ToString ());
			settings.PutSetting ("Pintora.Shape.Kind", canvas.SelectedShape.ToString ());
			settings.PutSetting ("Pintora.Gradient.Reversed", canvas.GradientReversed);
			foreach (CanvasHost.CanvasTool effect in EffectTools) {
				EffectToolOptions options = canvas.GetEffectOptions (effect);
				settings.PutSetting ($"Pintora.Effect.{effect}.Strength", options.Strength);
				settings.PutSetting ($"Pintora.Effect.{effect}.PixelSize", options.PixelSize);
			}
			settings.PutSetting ("Pintora.Tool", canvas.ActiveTool.ToString ());
			settings.DoSaveSettingsBeforeQuit ();
		};
		Content = new Grid {
			RowDefinitions = new RowDefinitions ("Auto,Auto,Auto,*,Auto"),
			Children = {
				CreateMainMenu (canvas),
				CreateCommandBar (canvas),
				CreateToolbar (canvas),
				CreateWorkspace (canvas),
				CreateStatusBar (canvas)
			}
		};
		ApplyChromeViewSettings ();
	}

	private Control CreateCommandBar (CanvasHost canvas)
	{
		var commands = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
		commands.Children.Add (CreateCompactCommand ("New", "New image", () => tab_manager.AddNewDocument ()));
		commands.Children.Add (CreateCompactCommand ("Open", "Open image", () => _ = OpenImageAsync (ActiveCanvas)));
		commands.Children.Add (CreateCompactCommand ("Save", "Save image as", () => _ = SaveAsAsync (ActiveCanvas)));
		commands.Children.Add (new Separator { Width = 1, Margin = new global::Avalonia.Thickness (6, 4) });
		var undo = CreateCompactCommand ("↶", "Undo", () => this.commands.TryExecute (PintoraKeyCommands.Undo.Id));
		var redo = CreateCompactCommand ("↷", "Redo", () => this.commands.TryExecute (PintoraKeyCommands.Redo.Id));
		void RefreshHistory () {
			CanvasHost active = ActiveCanvas;
			undo.IsEnabled = active.CanUndoStroke;
			redo.IsEnabled = active.CanRedoStroke;
		}
		refresh_history_commands = RefreshHistory;
		RefreshHistory ();
		commands.Children.Add (undo);
		commands.Children.Add (redo);
		var bar = new Border { Padding = new global::Avalonia.Thickness (8, 3), Child = commands };
		bar.Bind (Border.BackgroundProperty, new DynamicResourceExtension ("Pintora.Toolbar.Background"));
		Grid.SetRow (bar, 1);
		return bar;
	}

	private static Button CreateCompactCommand (string label, string tooltip, Action action)
	{
		var button = new Button { Content = label, MinWidth = 34, Height = 30, Padding = new global::Avalonia.Thickness (6, 2) };
		button.Click += (_, _) => action ();
		ToolTip.SetTip (button, tooltip);
		return button;
	}

	private Control CreateToolbar (CanvasHost canvas)
	{
		tool_options = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
		// Paint.NET keeps rasterization (antialiasing), Finish and zoom as global
		// toolbar controls rather than per-tool options, so they live outside the
		// options ScrollViewer at the right end of the bar.
		var antialias = new global::Avalonia.Controls.Primitives.ToggleButton {
			Content = "AA",
			MinWidth = 34,
			Height = 30,
			IsChecked = canvas.ShapeAntialias,
			VerticalAlignment = VerticalAlignment.Center
		};
		antialias.IsCheckedChanged += (_, _) => {
			if (ActiveCanvas is { } active)
				active.ShapeAntialias = antialias.IsChecked == true;
		};
		antialias_toggle = antialias;
		ToolTip.SetTip (antialias, "Antialiasing (smooth / pixelated edges)");
		var finish = new Button {
			Content = "Finish",
			MinWidth = 34,
			Height = 30,
			Padding = new global::Avalonia.Thickness (8, 2),
			VerticalAlignment = VerticalAlignment.Center
		};
		finish.Click += (_, _) => {
			if (ActiveCanvas is { } active)
				active.FinishShape ();
		};
		ToolTip.SetTip (finish, "Finish shape editing (Enter)");
		var global_controls = new StackPanel {
			Orientation = Orientation.Horizontal,
			Spacing = 4,
			Margin = new global::Avalonia.Thickness (8, 0, 0, 0),
			VerticalAlignment = VerticalAlignment.Center,
			Children = { antialias, finish }
		};
		DockPanel.SetDock (global_controls, Dock.Right);
		var toolbar = new Border {
			Padding = new global::Avalonia.Thickness (8, 4),
			Child = new DockPanel {
				LastChildFill = true,
				Children = {
					global_controls,
					new ScrollViewer {
						HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
						VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
						Content = tool_options
					}
				}
			}
		};
		RefreshToolOptions (canvas);
		toolbar.Bind (Border.BackgroundProperty, new DynamicResourceExtension ("Pintora.Toolbar.Background"));
		Grid.SetRow (toolbar, 2);
		toolbar_control = toolbar;
		return toolbar;
	}

	private static Button CreateColorSwatch (string color, Action primaryAction, Action? secondaryAction = null, Action? ctrlAction = null)
	{
		var button = new Button {
			Content = "",
			Width = 28,
			Height = 28,
			Background = new SolidColorBrush (Color.Parse (color)),
			Padding = new global::Avalonia.Thickness (0)
		};
		ToolTip.SetTip (button, $"Color {color} · left: primary · right: secondary");
		button.Click += (_, _) => primaryAction ();
		button.PointerPressed += (_, args) => {
			if (secondaryAction is not null && args.GetCurrentPoint (button).Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed) {
				secondaryAction ();
				args.Handled = true;
			} else if (ctrlAction is not null && (args.KeyModifiers & KeyModifiers.Control) != 0) {
				ctrlAction ();
				args.Handled = true;
			}
		};
		return button;
	}

	private Control CreateStatusBar (CanvasHost canvas)
	{
		var dimensions = new TextBlock ();
		var details = new TextBlock ();
		void RefreshDetails () {
			CanvasHost active = ActiveCanvas;
			dimensions.Text = $"{active.ImageSize.Width} × {active.ImageSize.Height} px";
			string position = active.PointerPosition is PointD point ? $"  •  X: {point.X:0}  Y: {point.Y:0}" : string.Empty;
			details.Text = $"{(active.ActiveTool == CanvasHost.CanvasTool.Shapes ? active.SelectedShape.ToString () : active.ActiveTool.ToString ())}  •  Brush {active.BrushSize}px{position}";
		}
		refresh_status = RefreshDetails;
		RefreshDetails ();
		var zoom = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
		void RefreshZoom () => zoom.Text = ActiveCanvas.Scale.ToString ("P0");
		refresh_zoom = RefreshZoom;
		RefreshZoom ();
		var zoom_controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Children = {
			dimensions,
			CreateCompactCommand ("−", "Zoom out", () => ActiveCanvas.SetScale (Math.Max (0.1, ActiveCanvas.Scale / 1.25))),
			zoom,
			CreateCompactCommand ("+", "Zoom in", () => ActiveCanvas.SetScale (Math.Min (32, ActiveCanvas.Scale * 1.25))),
			CreateCompactCommand ("Fit", "Fit canvas to window", () => ActiveCanvas.FitToWindow ())
		} };
		DockPanel.SetDock (zoom_controls, Dock.Right);
		var status = new Border {
			BorderThickness = new global::Avalonia.Thickness (0, 1, 0, 0),
			Padding = new global::Avalonia.Thickness (8, 2),
			Child = new DockPanel {
				Children = {
					zoom_controls,
					details
				}
			}
		};
		status.Bind (Border.BorderBrushProperty, new DynamicResourceExtension ("Pintora.Control.Border"));
		status.Bind (Border.BackgroundProperty, new DynamicResourceExtension ("Pintora.Status.Background"));
		Grid.SetRow (status, 4);
		status_bar_control = status;
		return status;
	}

	private Menu CreateMainMenu (CanvasHost canvas)
	{
		var preferences = new MenuItem { Header = "Preferences" };
		preferences.Click += (_, _) => new SettingsWindow (canvas, settings, keyboard).Show (this);
		MenuItem undo = CreateMenuItem ("_Undo", () => commands.TryExecute (PintoraKeyCommands.Undo.Id), keyboard, PintoraKeyCommands.Undo);
		MenuItem redo = CreateMenuItem ("_Redo", () => commands.TryExecute (PintoraKeyCommands.Redo.Id), keyboard, PintoraKeyCommands.Redo);
		MenuItem layer_undo = CreateMenuItem ("Undo layer", () => ActiveCanvas.UndoLayerOperation ());
		MenuItem layer_redo = CreateMenuItem ("Redo layer", () => ActiveCanvas.RedoLayerOperation ());
		MenuItem clear_history = CreateMenuItem ("Clear history", () => commands.TryExecute (PintoraKeyCommands.ClearHistory.Id), keyboard, PintoraKeyCommands.ClearHistory);
		void RefreshHistoryCommands () {
			CanvasHost active = ActiveCanvas;
			undo.IsEnabled = active.CanUndoStroke;
			redo.IsEnabled = active.CanRedoStroke;
			layer_undo.IsEnabled = active.CanUndoLayerOperation;
			layer_redo.IsEnabled = active.CanRedoLayerOperation;
			clear_history.IsEnabled = active.History.Count > 0;
		}
		refresh_history_commands = RefreshHistoryCommands;
		RefreshHistoryCommands ();
		var menu = new Menu {
			Items = {
				new MenuItem { Header = "_File", Items = {
					CreateMenuItem ("_New", () => tab_manager.AddNewDocument ()),
					CreateMenuItem ("_Open", () => _ = OpenImageAsync (ActiveCanvas)),
					CreateMenuItem ("Import image as layer", () => _ = ImportImageLayerAsync (ActiveCanvas)),
					CreateMenuItem ("Insert image annotation", () => _ = InsertImageAnnotationAsync (ActiveCanvas)),
					new Separator (),
					CreateMenuItem ("_Save", () => _ = SaveActiveTabAsync (), keyboard, PintoraKeyCommands.Save),
					CreateMenuItem ("Save _All", () => _ = SaveAllDocumentsAsync (), keyboard, PintoraKeyCommands.SaveAll),
					CreateMenuItem ("_Close", () => _ = CloseActiveTabAsync (), keyboard, PintoraKeyCommands.CloseDocument),
					CreateMenuItem ("Close _All", () => _ = CloseAllDocumentsAsync ()),
					new Separator (),
					CreateMenuItem ("Save _As", () => _ = SaveAsAsync (ActiveCanvas)),
					CreateMenuItem ("Save _PNG", () => _ = SavePngAsync (ActiveCanvas)),
					CreateMenuItem ("Save _JPEG", () => _ = SaveJpegAsync (ActiveCanvas)),
					CreateMenuItem ("Save _WebP", () => _ = SaveWebpAsync (ActiveCanvas)),
					CreateMenuItem ("Save _TIFF", () => _ = SaveTiffAsync (ActiveCanvas)),
					CreateMenuItem ("Save _ORA", () => _ = SaveOraAsync (ActiveCanvas)),
					new Separator (),
					CreateMenuItem ("E_xit", () => _ = ExitAsync (), keyboard, PintoraKeyCommands.Exit)
				} },
				new MenuItem { Header = "_Edit", Items = {
					undo,
					redo,
					clear_history,
					layer_undo,
					layer_redo,
					CreateMenuItem ("Clear layer", () => ActiveCanvas.ClearActiveLayer (), keyboard, PintoraKeyCommands.ClearLayer),
					new Separator (),
					CreateMenuItem ("Copy selection", () => ActiveCanvas.CopySelection (), keyboard, PintoraKeyCommands.Copy),
					CreateMenuItem ("Copy selection to clipboard", () => _ = CopySelectionToClipboardAsync (ActiveCanvas), keyboard, PintoraKeyCommands.CopySelectionImage),
					CreateMenuItem ("Copy image to clipboard", () => _ = CopyImageToClipboardAsync (ActiveCanvas), keyboard, PintoraKeyCommands.CopyImage),
					CreateMenuItem ("Paste selection", () => ActiveCanvas.PasteSelection (), keyboard, PintoraKeyCommands.Paste),
					CreateMenuItem ("Paste image as layer", () => _ = ActiveCanvas.PasteImageFromClipboardAsync (image_source_provider), keyboard, PintoraKeyCommands.PasteImageAsLayer),
					CreateMenuItem ("Paste image as annotation", () => _ = ActiveCanvas.AddImageAnnotationFromClipboardAsync (image_source_provider), keyboard, PintoraKeyCommands.PasteImageAnnotation),
					CreateMenuItem ("Rotate selected annotation 90° clockwise", () => {
						CanvasHost active = ActiveCanvas;
						if (active.SelectedAnnotation is { } annotation)
							active.RotateAnnotation (annotation, Math.PI / 2);
					}),
					CreateMenuItem ("Delete selected annotation", () => {
						CanvasHost active = ActiveCanvas;
						if (active.SelectedAnnotation is { } annotation && active.RemoveAnnotation (annotation))
							active.ClearAnnotationSelection ();
					}),
					CreateMenuItem ("Duplicate selected annotation", () => ActiveCanvas.DuplicateSelectedAnnotation (), keyboard, PintoraKeyCommands.DuplicateAnnotation),
					CreateMenuItem ("Select _all", () => ActiveCanvas.SelectAll (), keyboard, PintoraKeyCommands.SelectAll),
					CreateMenuItem ("Deselect", () => ActiveCanvas.ClearSelection (), keyboard, PintoraKeyCommands.Deselect),
					CreateMenuItem ("Flip selection horizontally", () => ActiveCanvas.FlipSelectionHorizontal ()),
					CreateMenuItem ("Flip selection vertically", () => ActiveCanvas.FlipSelectionVertical ()),
					CreateMenuItem ("Rotate selection 90° clockwise", () => ActiveCanvas.RotateSelectionClockwise ()),
					new Separator (),
					preferences
				} },
				new MenuItem { Header = "_View", Items = {
					CreateMenuItem ("Fit canvas", () => ActiveCanvas.FitToWindow (), keyboard, PintoraKeyCommands.FitCanvas),
					CreateMenuItem ("100%", () => ActiveCanvas.SetActualSize (), keyboard, PintoraKeyCommands.ActualSize),
					new Separator (),
					CreateViewGridToggle (),
					CreateViewRulersToggle (),
					new Separator (),
					CreateViewToggle ("Show tool_bar", () => view_settings.ShowToolbar, value => view_settings.ShowToolbar = value),
					CreateViewToggle ("Show _toolbox", () => view_settings.ShowToolbox, value => view_settings.ShowToolbox = value),
					CreateViewToggle ("Show _status bar", () => view_settings.ShowStatusBar, value => view_settings.ShowStatusBar = value),
					CreateViewToggle ("Show tool _windows", () => view_settings.ShowToolWindows, value => view_settings.ShowToolWindows = value),
					CreateViewToggle ("Show image _tabs", () => view_settings.ShowImageTabs, value => view_settings.ShowImageTabs = value),
					new Separator (),
					CreateMenuItem ("Light theme", () => App.SetTheme (global::Avalonia.Styling.ThemeVariant.Light)),
					CreateMenuItem ("Dark theme", () => App.SetTheme (global::Avalonia.Styling.ThemeVariant.Dark)),
					CreateMenuItem ("System theme", () => App.SetTheme (global::Avalonia.Styling.ThemeVariant.Default))
				} },
				new MenuItem { Header = "_Image", Items = {
					CreateMenuItem ("Resize image", () => new ResizeImageWindow (ActiveCanvas).Show (this)),
					CreateMenuItem ("Resize canvas", () => new ResizeCanvasWindow (ActiveCanvas).Show (this)),
					CreateMenuItem ("Crop image", () => new CropImageWindow (ActiveCanvas).Show (this)),
					new Separator (),
					CreateMenuItem ("Flip image horizontally", () => ActiveCanvas.FlipImageHorizontal ()),
					CreateMenuItem ("Flip image vertically", () => ActiveCanvas.FlipImageVertical ()),
					new Separator (),
					CreateMenuItem ("Rotate image 90° clockwise", () => ActiveCanvas.RotateImageClockwise ()),
					CreateMenuItem ("Rotate image 90° counter-clockwise", () => ActiveCanvas.RotateImageCounterClockwise ()),
					CreateMenuItem ("Rotate image 180°", () => ActiveCanvas.RotateImage180 ()),
					new Separator (),
					CreateMenuItem ("Flatten image", () => ActiveCanvas.FlattenImage ())
				} },
				new MenuItem { Header = "_Layers", Items = {
					CreateMenuItem ("Add layer", () => ActiveCanvas.AddLayer ()),
					CreateMenuItem ("Duplicate layer", () => ActiveCanvas.DuplicateActiveLayer (), keyboard, PintoraKeyCommands.DuplicateLayer),
					CreateMenuItem ("Move layer up", () => ActiveCanvas.MoveLayer (1)),
					CreateMenuItem ("Move layer down", () => ActiveCanvas.MoveLayer (-1)),
					CreateMenuItem ("Merge layer down", () => ActiveCanvas.MergeActiveLayerDown ()),
					CreateMenuItem ("Delete layer", () => ActiveCanvas.RemoveLayer ()),
					CreateMenuItem ("Rename layer", () => {
						CanvasHost active = ActiveCanvas;
						new LayerRenameWindow (active, active.Layers[active.ActiveLayerIndex].Name, () => { }).Show (this);
					})
				} },
				new MenuItem { Header = "_Window", Items = {
					CreateMenuItem ("History", () => new HistoryWindow (ActiveCanvas).Show (this))
				} },
				new MenuItem { Header = "_Help", Items = {
					CreateMenuItem ("Keyboard shortcuts", () => new KeyboardSettingsWindow (keyboard).Show (this)),
					CreateMenuItem ("About Pintora", () => new AboutWindow ().ShowDialog (this))
				} }
			}
		};
		Grid.SetRow (menu, 0);
		return menu;
	}

	private CanvasHost ActiveCanvas => tab_manager.ActiveTab?.Canvas ?? throw new InvalidOperationException ("No active document tab.");

	private MenuItem CreateViewGridToggle ()
	{
		var item = new MenuItem {
			Header = "Show _grid",
			ToggleType = MenuItemToggleType.CheckBox,
			IsChecked = view_settings.ShowGrid
		};
		item.Click += (_, _) => {
			view_settings.ShowGrid = item.IsChecked == true;
			view_settings.Save ();
			ApplyGridViewSettingsToAllTabs ();
		};
		return item;
	}

	private MenuItem CreateViewRulersToggle ()
	{
		var item = new MenuItem {
			Header = "Show _rulers",
			ToggleType = MenuItemToggleType.CheckBox,
			IsChecked = view_settings.ShowRulers
		};
		item.Click += (_, _) => {
			view_settings.ShowRulers = item.IsChecked == true;
			view_settings.Save ();
			foreach (DocumentTab tab in tab_manager.Tabs)
				tab.Canvas.ShowRulers = view_settings.ShowRulers;
		};
		return item;
	}

	private MenuItem CreateViewToggle (string header, Func<bool> get, Action<bool> set)
	{
		var item = new MenuItem {
			Header = header,
			ToggleType = MenuItemToggleType.CheckBox,
			IsChecked = get ()
		};
		item.Click += (_, _) => {
			set (item.IsChecked == true);
			view_settings.Save ();
			ApplyChromeViewSettings ();
		};
		return item;
	}

	/// <summary>
	/// Shows or hides the existing chrome elements according to the persisted
	/// view settings. Hiding never touches documents, tabs or canvas state.
	/// </summary>
	private void ApplyChromeViewSettings ()
	{
		if (toolbar_control is not null) toolbar_control.IsVisible = view_settings.ShowToolbar;
		if (toolbox_control is not null) toolbox_control.IsVisible = view_settings.ShowToolbox;
		if (status_bar_control is not null) status_bar_control.IsVisible = view_settings.ShowStatusBar;
		if (palette_control is not null) palette_control.IsVisible = view_settings.ShowToolWindows;
		if (layers_control is not null) layers_control.IsVisible = view_settings.ShowToolWindows;
		if (tab_strip_host is not null) tab_strip_host.IsVisible = view_settings.ShowImageTabs;
		if (canvas_panel_control is not null) {
			canvas_panel_control.Margin = new global::Avalonia.Thickness (
				view_settings.ShowToolbox ? 96 : 10,
				10,
				view_settings.ShowToolWindows ? 252 : 10,
				view_settings.ShowToolWindows ? 190 : 10);
		}
	}

	/// <summary>
	/// Applies the persisted grid settings to a single canvas. Called for the
	/// initial document and whenever a tab becomes active so every canvas
	/// reflects the shared view settings.
	/// </summary>
	private void ApplyGridViewSettings (CanvasHost canvas)
	{
		canvas.GridSize = view_settings.GridSize;
		canvas.ShowGrid = view_settings.ShowGrid;
	}

	private void ApplyGridViewSettingsToAllTabs ()
	{
		foreach (DocumentTab tab in tab_manager.Tabs)
			ApplyGridViewSettings (tab.Canvas);
	}

	private void UpdateDocumentTitle (CanvasHost canvas, string version)
	{
		DocumentSession document = canvas.DocumentSession;
		string dirty_marker = document.IsDirty ? "* " : string.Empty;
		string document_name = document.FilePath is null ? string.Empty : $" — {document.Title}";
		Title = $"{dirty_marker}Pintora {version}{document_name} — Simple. Powerful. Creative.";
	}

	private static Button CreateBrushButton (string label, Action action)
	{
		var button = new Button { Content = label };
		button.Click += (_, _) => action ();
		return button;
	}

	private static NumericUpDown CreateBrushSizeInput (CanvasHost canvas)
	{
		var input = new NumericUpDown { Minimum = 1, Maximum = 256, Value = canvas.BrushSize, Width = 70 };
		input.ValueChanged += (_, args) => canvas.BrushSize = (int) (args.NewValue ?? 1);
		// Paint.NET-style wheel adjustment: hover the width control and scroll.
		input.PointerWheelChanged += (_, args) => {
			canvas.AdjustBrushSize (args.Delta.Y > 0 ? 1 : -1);
			input.Value = canvas.BrushSize;
			args.Handled = true;
		};
		return input;
	}

	private static TextBlock CreateToolbarLabel (string text)
		=> new () { Text = text, VerticalAlignment = VerticalAlignment.Center };

	private static Control CreateShapeWidthInput (CanvasHost canvas)
	{
		var value = new TextBox { Text = canvas.ShapeOutlineWidth.ToString (), Width = 42, Height = 30 };
		void ApplyText ()
		{
			if (int.TryParse (value.Text, out int width)) canvas.ShapeOutlineWidth = width;
			value.Text = canvas.ShapeOutlineWidth.ToString ();
		}
		value.LostFocus += (_, _) => ApplyText ();
		value.KeyDown += (_, args) => {
			if (args.Key == Key.Enter) { ApplyText (); args.Handled = true; }
		};
		Button decrease = new () { Content = "−", Width = 30, Height = 30 };
		Button increase = new () { Content = "+", Width = 30, Height = 30 };
		decrease.Click += (_, _) => { canvas.ShapeOutlineWidth--; value.Text = canvas.ShapeOutlineWidth.ToString (); };
		increase.Click += (_, _) => { canvas.ShapeOutlineWidth++; value.Text = canvas.ShapeOutlineWidth.ToString (); };
		var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = { value, decrease, increase } };
		// Paint.NET-style wheel adjustment: hover the width control and scroll.
		panel.PointerWheelChanged += (_, args) => {
			canvas.ShapeOutlineWidth += args.Delta.Y > 0 ? 1 : -1;
			value.Text = canvas.ShapeOutlineWidth.ToString ();
			args.Handled = true;
		};
		return panel;
	}

	private static Slider CreateBrushSlider (double minimum, double maximum, double value, Action<double> changed, double width)
	{
		var slider = new Slider { Minimum = minimum, Maximum = maximum, Value = value, Width = width };
		slider.ValueChanged += (_, args) => changed (args.NewValue);
		return slider;
	}

	private static MenuItem CreateMenuItem (string header, Action action)
	{
		var item = new MenuItem { Header = header };
		item.Click += (_, _) => action ();
		return item;
	}

	private static MenuItem CreateMenuItem (string header, Action action, PintoraKeyboardSettings keyboard, PintoraKeyCommand command)
	{
		MenuItem item = CreateMenuItem (header, action);
		item.InputGesture = keyboard.GestureFor (command);
		return item;
	}

	private async Task SavePngAsync (CanvasHost canvas)
	{
		if (StorageProvider is null)
			return;

		var file = await StorageProvider.SaveFilePickerAsync (new FilePickerSaveOptions {
			SuggestedFileName = SuggestedFileName (canvas, "pintora.png"),
			DefaultExtension = "png",
			FileTypeChoices = new[] {
				new FilePickerFileType ("PNG image") { Patterns = new[] { "*.png" } }
			}
		});

		if (file is not null)
			canvas.SavePng (file.Path.LocalPath);
	}

	private static string SuggestedFileName (CanvasHost canvas, string fallback)
	{
		string? path = canvas.DocumentSession.FilePath;
		if (path is null)
			return fallback;
		string name = System.IO.Path.GetFileName (path);
		return string.IsNullOrEmpty (name) ? fallback : name;
	}

	private async Task SaveAsAsync (CanvasHost canvas)
	{
		if (StorageProvider is null)
			return;

		var file = await StorageProvider.SaveFilePickerAsync (new FilePickerSaveOptions {
			SuggestedFileName = SuggestedFileName (canvas, "pintora.png"),
			DefaultExtension = "png",
			FileTypeChoices = new[] {
				new FilePickerFileType ("PNG image") { Patterns = new[] { "*.png" } },
				new FilePickerFileType ("JPEG image") { Patterns = new[] { "*.jpg", "*.jpeg" } },
				new FilePickerFileType ("WebP image") { Patterns = new[] { "*.webp" } },
				new FilePickerFileType ("TIFF image") { Patterns = new[] { "*.tif", "*.tiff" } },
				new FilePickerFileType ("OpenRaster image") { Patterns = new[] { "*.ora" } }
			}
		});

		if (file is null)
			return;

		canvas.SaveImage (file.Path.LocalPath);
	}

	private async Task CopyImageToClipboardAsync (CanvasHost canvas)
	{
		if (clipboard_service is not null)
			await canvas.CopyImageToClipboardAsync (clipboard_service);
	}

	private async Task CopySelectionToClipboardAsync (CanvasHost canvas)
	{
		if (clipboard_service is not null)
			await canvas.CopySelectionToClipboardAsync (clipboard_service);
	}

	private async Task OpenImageAsync (CanvasHost canvas)
	{
		if (StorageProvider is null)
			return;

		var file = await StorageProvider.OpenFilePickerAsync (new FilePickerOpenOptions {
			AllowMultiple = false,
			FileTypeFilter = new[] {
				new FilePickerFileType ("Pintora images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp", "*.tif", "*.tiff", "*.bmp", "*.gif", "*.ora" } },
				new FilePickerFileType ("All files") { Patterns = new[] { "*.*" } }
			}
		});

		if (file.Count > 0)
			tab_manager.AddExistingDocument (file[0].Path.LocalPath);
	}

	private async Task ImportImageLayerAsync (CanvasHost canvas)
	{
		if (StorageProvider is null)
			return;
		var file = await StorageProvider.OpenFilePickerAsync (new FilePickerOpenOptions {
			AllowMultiple = false,
			FileTypeFilter = new[] { new FilePickerFileType ("Image") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp", "*.tif", "*.tiff", "*.bmp", "*.gif", "*.svg" } } }
		});
		if (file.Count > 0)
			canvas.ImportImageAsLayer (file[0].Path.LocalPath);
	}

	private async Task InsertImageAnnotationAsync (CanvasHost canvas)
	{
		if (StorageProvider is null)
			return;
		var file = await StorageProvider.OpenFilePickerAsync (new FilePickerOpenOptions {
			AllowMultiple = false,
			FileTypeFilter = new[] { new FilePickerFileType ("Image") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp", "*.tif", "*.tiff", "*.bmp", "*.gif" } } }
		});
		if (file.Count == 0)
			return;
		var size = canvas.ImageSize;
		var bounds = new Rect (size.Width * 0.25, size.Height * 0.25, size.Width * 0.5, size.Height * 0.5);
		await canvas.AddImageAnnotationFromFileAsync (image_source_provider, file[0].Path.LocalPath, bounds);
	}

	private async Task SaveOraAsync (CanvasHost canvas)
	{
		if (StorageProvider is null)
			return;
		var file = await StorageProvider.SaveFilePickerAsync (new FilePickerSaveOptions {
			SuggestedFileName = SuggestedFileName (canvas, "pintora.ora"),
			DefaultExtension = "ora",
			FileTypeChoices = new[] { new FilePickerFileType ("OpenRaster image") { Patterns = new[] { "*.ora" } } }
		});
		if (file is not null)
			canvas.SaveOra (file.Path.LocalPath);
	}

	private async Task SaveJpegAsync (CanvasHost canvas)
	{
		if (StorageProvider is null)
			return;
		var file = await StorageProvider.SaveFilePickerAsync (new FilePickerSaveOptions {
			SuggestedFileName = SuggestedFileName (canvas, "pintora.jpg"),
			DefaultExtension = "jpg",
			FileTypeChoices = new[] { new FilePickerFileType ("JPEG image") { Patterns = new[] { "*.jpg", "*.jpeg" } } }
		});
		if (file is not null)
			canvas.SaveJpeg (file.Path.LocalPath);
	}

	private async Task SaveWebpAsync (CanvasHost canvas)
	{
		if (StorageProvider is null) return;
		var file = await StorageProvider.SaveFilePickerAsync (new FilePickerSaveOptions {
			SuggestedFileName = SuggestedFileName (canvas, "pintora.webp"),
			DefaultExtension = "webp",
			FileTypeChoices = new[] { new FilePickerFileType ("WebP image") { Patterns = new[] { "*.webp" } } }
		});
		if (file is not null) canvas.SaveWebp (file.Path.LocalPath);
	}

	private async Task SaveTiffAsync (CanvasHost canvas)
	{
		if (StorageProvider is null) return;
		var file = await StorageProvider.SaveFilePickerAsync (new FilePickerSaveOptions {
			SuggestedFileName = SuggestedFileName (canvas, "pintora.tiff"),
			DefaultExtension = "tiff",
			FileTypeChoices = new[] { new FilePickerFileType ("TIFF image") { Patterns = new[] { "*.tif", "*.tiff" } } }
		});
		if (file is not null) canvas.SaveTiff (file.Path.LocalPath);
	}

	private async Task OpenOraAsync (CanvasHost canvas)
	{
		if (StorageProvider is null)
			return;
		var files = await StorageProvider.OpenFilePickerAsync (new FilePickerOpenOptions {
			AllowMultiple = false,
			FileTypeFilter = new[] { new FilePickerFileType ("OpenRaster image") { Patterns = new[] { "*.ora" } } }
		});
		if (files.Count > 0)
			canvas.OpenOra (files[0].Path.LocalPath);
	}

	private Control CreateWorkspace (CanvasHost canvas)
	{
		var workspace = new Grid {
			Children = {
				CreateCanvasPlaceholder (canvas),
				(toolbox_control = CreateToolPanel (canvas)),
				(palette_control = CreatePalette (canvas)),
				(layers_control = CreateLayersPanel ())
			}
		};
		workspace.Bind (Grid.BackgroundProperty, new DynamicResourceExtension ("Pintora.Workspace.Background"));
		Grid.SetRow (workspace, 3);
		return workspace;
	}

	private Control CreateToolPanel (CanvasHost canvas)
	{
		var tools = new WrapPanel { Width = 76, ItemWidth = 38, ItemHeight = 38 };
		var buttons = new List<(Button Button, CanvasHost.CanvasTool Tool)> ();
		Button brush = CreateToolButton (canvas, "Brush", CanvasHost.CanvasTool.Brush,
			"M7 14c-1.66 0-3 1.34-3 3 0 1.31-1.16 2-2 2 .92 1.22 2.49 2 4 2 2.21 0 4-1.79 4-4 0-1.66-1.34-3-3-3zm13.71-9.37l-1.34-1.34c-.39-.39-1.02-.39-1.41 0L9 12.25 11.75 15l8.96-8.96c.39-.39.39-1.02 0-1.41z");
		Button pencil = CreateToolButton (canvas, "Pencil", CanvasHost.CanvasTool.Pencil,
			"M4 17.25V21h3.75L18.81 9.94l-3.75-3.75L4 17.25zM20.71 8.04c.39-.39.39-1.02 0-1.41l-2.34-2.34a.9959.9959 0 0 0-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z");
		Button shapes = CreateToolButton (canvas, "Shapes", CanvasHost.CanvasTool.Shapes,
			"M4 4h16v16H4V4zm2 2v12h12V6H6z");
		Button gradient = CreateToolButton (canvas, "Gradient", CanvasHost.CanvasTool.Gradient,
			"M4 4h16v16H4V4zm2 12L16 6h4L8 18H6z");
		Button fill = CreateToolButton (canvas, "Fill", CanvasHost.CanvasTool.Fill,
			"M5 3h10v2H5V3zm0 4h10v2H5V7zm0 4h10v2H5v-2zm12-8h2v18h-2V3z");
		Button picker = CreateToolButton (canvas, "Color Picker", CanvasHost.CanvasTool.ColorPicker,
			"M19.35 10.04L14 4.69 15.41 3.28 20.76 8.63 19.35 10.04zM3 17.25V21h3.75l10.81-10.81-3.75-3.75L3 17.25z");
		Button zoom = CreateToolButton (canvas, "Zoom", CanvasHost.CanvasTool.Zoom,
			"M9.5 3a6.5 6.5 0 1 0 4.06 11.58L19 20l1-1-5.44-5.42A6.5 6.5 0 0 0 9.5 3zm0 2a4.5 4.5 0 1 1 0 9 4.5 4.5 0 0 1 0-9z");
		Button eraser = CreateToolButton (canvas, "Eraser", CanvasHost.CanvasTool.Eraser,
			"M15.8698 2.66878L20.8384 7.6373C21.717 8.51598 21.717 9.9406 20.8384 10.8193L12.1566 19.4998L18.2544 19.5C18.6341 19.5 18.9479 19.7821 18.9976 20.1482L19.0044 20.25C19.0044 20.6297 18.7223 20.9435 18.3562 20.9931L18.2544 21L9.84443 21.0012C9.22822 21.0348 8.60082 20.8163 8.1301 20.3456L3.16157 15.377C2.28289 14.4984 2.28289 13.0737 3.16157 12.1951L12.6879 2.66878C13.5665 1.7901 14.9912 1.7901 15.8698 2.66878Z");
		Button selection = CreateToolButton (canvas, "Select", CanvasHost.CanvasTool.Selection, "M3 3h18v18H3V3zm2 2v14h14V5H5z");
		Button text = CreateToolButton (canvas, "Text", CanvasHost.CanvasTool.Text, "M5 4h14v3h-5v13h-4V7H5V4z");
		Button blur = CreateToolButton (canvas, "Blur", CanvasHost.CanvasTool.Blur, "M4 4h16v16H4V4zm3 3h10v10H7V7z");
		Button pixelate = CreateToolButton (canvas, "Pixelate", CanvasHost.CanvasTool.Pixelate, "M4 4h6v6H4V4zm10 0h6v6h-6V4zM4 14h6v6H4v-6zm10 0h6v6h-6v-6z");
		Button highlight = CreateToolButton (canvas, "Highlight", CanvasHost.CanvasTool.Highlight, "M4 4h16v16H4V4z");
		Button magnify = CreateToolButton (canvas, "Magnify", CanvasHost.CanvasTool.Magnify, "M9 3a6 6 0 1 0 4 10.5L19 19l-1 1-6-5.5A6 6 0 0 0 9 3z");
		Button spotlight = CreateToolButton (canvas, "Spotlight", CanvasHost.CanvasTool.Spotlight, "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18zm0 3a6 6 0 1 1 0 12 6 6 0 0 1 0-12z");
		buttons.Add ((selection, CanvasHost.CanvasTool.Selection));
		buttons.Add ((text, CanvasHost.CanvasTool.Text));
		buttons.Add ((brush, CanvasHost.CanvasTool.Brush));
		buttons.Add ((pencil, CanvasHost.CanvasTool.Pencil));
		buttons.Add ((shapes, CanvasHost.CanvasTool.Shapes));
		buttons.Add ((gradient, CanvasHost.CanvasTool.Gradient));
		buttons.Add ((fill, CanvasHost.CanvasTool.Fill));
		buttons.Add ((picker, CanvasHost.CanvasTool.ColorPicker));
		buttons.Add ((zoom, CanvasHost.CanvasTool.Zoom));
		buttons.Add ((eraser, CanvasHost.CanvasTool.Eraser));
		buttons.Add ((blur, CanvasHost.CanvasTool.Blur));
		buttons.Add ((pixelate, CanvasHost.CanvasTool.Pixelate));
		buttons.Add ((highlight, CanvasHost.CanvasTool.Highlight));
		buttons.Add ((magnify, CanvasHost.CanvasTool.Magnify));
		buttons.Add ((spotlight, CanvasHost.CanvasTool.Spotlight));
		tool_buttons = buttons;
		foreach ((Button button, CanvasHost.CanvasTool tool) in buttons) {
			button.Click += (_, _) => {
				CanvasHost active = ActiveCanvas;
				active_tool = tool;
				active.ActiveTool = tool;
				UpdateToolButtons (buttons, active.ActiveTool);
				RefreshToolOptions (active);
			};
			tools.Children.Add (button);
		}
		// Tool buttons and options always follow the active tab. The handlers
		// read ActiveCanvas instead of capturing the initial canvas, so a tab
		// switch cannot leave them pointing at a stale document.
		tab_manager.ActiveTabChanged += (_, _) => {
			CanvasHost active = ActiveCanvas;
			UpdateToolButtons (buttons, active.ActiveTool);
			RefreshToolOptions (active);
		};
		var panel = new Border {
			Width = 88,
			Padding = new global::Avalonia.Thickness (5),
			Margin = new global::Avalonia.Thickness (8),
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Top,
			Child = new StackPanel { Spacing = 4, Children = {
				new TextBlock { Text = "Tools", FontSize = 12 }, tools
			} }
		};
		panel.Bind (Border.BackgroundProperty, new DynamicResourceExtension ("Pintora.Panel.Background"));
		panel.Bind (Border.BorderBrushProperty, new DynamicResourceExtension ("Pintora.Control.Border"));
		panel.BorderThickness = new global::Avalonia.Thickness (1);
		return panel;
	}

	private Control CreatePalette (CanvasHost canvas)
	{
		var primary = new Button { Content = "", Width = 30, Height = 30, Background = new SolidColorBrush (Colors.Black) };
		var secondary = new Button { Content = "", Width = 30, Height = 30, Background = new SolidColorBrush (Colors.White) };
		var wheel = new ColorPickerSurface { Width = 136, Height = 136, ShowValue = false };
		void RefreshColors ()
		{
			CanvasHost active = ActiveCanvas;
			primary.Background = new SolidColorBrush (ToAvaloniaColor (active.BrushColor));
			secondary.Background = new SolidColorBrush (ToAvaloniaColor (active.SecondaryBrushColor));
			wheel.Color = ToAvaloniaColor (active.BrushColor);
		}
		tab_manager.ActiveTabChanged += (_, _) => RefreshColors ();
		void SaveColors ()
		{
			CanvasHost active = ActiveCanvas;
			settings.PutSetting ("Pintora.Palette.Primary", ToAvaloniaColor (active.BrushColor).ToString ());
			settings.PutSetting ("Pintora.Palette.Secondary", ToAvaloniaColor (active.SecondaryBrushColor).ToString ());
			settings.PutSetting ("Pintora.Palette.Recent", string.Join (",", recent_colors.ConvertAll (ToAvaloniaColor).ConvertAll (color => color.ToString ())));
			settings.PutSetting ("Pintora.Palette.Colors", string.Join (",", palette_colors.ConvertAll (ToAvaloniaColor).ConvertAll (color => color.ToString ())));
			RefreshColors ();
		}
		primary.Click += (_, _) => new ColorDialogWindow (ToAvaloniaColor (ActiveCanvas.BrushColor), color => {
			CanvasHost active = ActiveCanvas;
			active.BrushColor = ColorBgra.FromBgra (color.B, color.G, color.R, color.A);
			SavePaletteColor (active);
			RefreshColors ();
		}).Show (this);
		secondary.Click += (_, _) => new ColorDialogWindow (ToAvaloniaColor (ActiveCanvas.SecondaryBrushColor), color => {
			CanvasHost active = ActiveCanvas;
			active.SecondaryBrushColor = ColorBgra.FromBgra (color.B, color.G, color.R, color.A);
			SavePaletteColor (active);
			RefreshColors ();
		}).Show (this);
		wheel.ColorChanged += (_, color) => {
			ActiveCanvas.BrushColor = ColorBgra.FromBgra (color.B, color.G, color.R, color.A);
			SaveColors ();
		};
		var swap = CreateCompactCommand ("⇄", "Swap colors", () => {
			ActiveCanvas.SwapColors ();
			SaveColors ();
		});
		var reset = CreateCompactCommand ("◩", "Reset colors", () => {
			ActiveCanvas.ResetColors ();
			SaveColors ();
		});
		var colors = new WrapPanel { Width = 216, ItemWidth = 18, ItemHeight = 18 };
		string[] default_palette = {
			"#000000", "#404040", "#808080", "#BFBFBF", "#FFFFFF",
			"#7F0000", "#FF0000", "#FF7F00", "#FFFF00", "#7FFF00", "#00FF00", "#00FF7F",
			"#00FFFF", "#007FFF", "#0000FF", "#7F00FF", "#FF00FF", "#FF007F",
			"#800000", "#C00000", "#E95B64", "#F0B75E", "#72C991", "#3B8CFF"
		};
		if (palette_colors.Count == 0)
			foreach (string hex in default_palette)
				palette_colors.Add (ToColorBgra (Color.Parse (hex)));
		for (int palette_index = 0; palette_index < palette_colors.Count; palette_index++) {
			int index = palette_index;
			ColorBgra color = palette_colors[index];
			var swatch = CreateColorSwatch (ToAvaloniaColor (color).ToString (),
				() => { CanvasHost active = ActiveCanvas; active.BrushColor = color; SavePaletteColor (active); RefreshColors (); },
				() => { CanvasHost active = ActiveCanvas; active.SecondaryBrushColor = color; SavePaletteColor (active); RefreshColors (); },
				() => new ColorDialogWindow (ToAvaloniaColor (color), picked => {
					palette_colors[index] = ColorBgra.FromBgra (picked.B, picked.G, picked.R, picked.A);
					CanvasHost active = ActiveCanvas;
					active.BrushColor = palette_colors[index];
					SavePaletteColor (active);
					RefreshColors ();
				}).Show (this));
			swatch.Width = 18;
			swatch.Height = 18;
			colors.Children.Add (swatch);
		}
		var recent = new WrapPanel { Width = 216, ItemWidth = 18, ItemHeight = 18 };
		foreach (ColorBgra recent_color in recent_colors) {
			ColorBgra color = recent_color;
			var swatch = CreateColorSwatch (ToAvaloniaColor (color).ToString (),
				() => { CanvasHost active = ActiveCanvas; active.BrushColor = color; SavePaletteColor (active); },
				() => { CanvasHost active = ActiveCanvas; active.SecondaryBrushColor = color; SavePaletteColor (active); });
			swatch.Width = 18;
			swatch.Height = 18;
			recent.Children.Add (swatch);
		}
		var hexInput = new TextBox { Width = 112, PlaceholderText = "#RRGGBB" };
		var applyHex = new Button { Content = "Apply", MinWidth = 58 };
		var advancedColors = new StackPanel {
			Spacing = 4,
			IsVisible = false,
			Children = {
				new TextBlock { Text = "Custom color", FontSize = 11 },
				new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { hexInput, applyHex } }
			}
		};
		applyHex.Click += (_, _) => {
			if (!string.IsNullOrWhiteSpace (hexInput.Text) &&
				Color.TryParse (hexInput.Text.Trim (), out Color parsed)) {
				CanvasHost active = ActiveCanvas;
				active.BrushColor = ToColorBgra (parsed);
				SavePaletteColor (active);
				RefreshColors ();
			}
		};
		var more = CreateCompactCommand ("More…", "Show custom color controls", () => {
			advancedColors.IsVisible = !advancedColors.IsVisible;
			if (advancedColors.IsVisible)
				hexInput.Text = ToAvaloniaColor (ActiveCanvas.BrushColor).ToString ();
		});
		RefreshColors ();
		var content = new StackPanel {
			Spacing = 5,
			Children = {
				new TextBlock { Text = "Colors", FontSize = 12 },
				new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { primary, secondary, swap, reset, more } },
				advancedColors,
				new Border { HorizontalAlignment = HorizontalAlignment.Center, Child = wheel },
				new TextBlock { Text = "Recent", FontSize = 11 },
				recent,
				new TextBlock { Text = "Palette", FontSize = 11 },
				colors,
			}
		};
		var panel = new Border {
			Width = 232,
			Padding = new global::Avalonia.Thickness (7),
			Margin = new global::Avalonia.Thickness (8),
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Bottom,
			Child = content,
			BorderThickness = new global::Avalonia.Thickness (1)
		};
		panel.Bind (Border.BackgroundProperty, new DynamicResourceExtension ("Pintora.Panel.Background"));
		panel.Bind (Border.BorderBrushProperty, new DynamicResourceExtension ("Pintora.Control.Border"));
		return panel;
	}

	private void SavePaletteColor (CanvasHost canvas)
	{
		settings.PutSetting ("Pintora.Palette.Primary", ToAvaloniaColor (canvas.BrushColor).ToString ());
		settings.PutSetting ("Pintora.Palette.Secondary", ToAvaloniaColor (canvas.SecondaryBrushColor).ToString ());
		recent_colors.Remove (canvas.BrushColor);
		recent_colors.Insert (0, canvas.BrushColor);
		if (recent_colors.Count > 10)
			recent_colors.RemoveAt (recent_colors.Count - 1);
		settings.PutSetting ("Pintora.Palette.Recent", string.Join (",", recent_colors.ConvertAll (ToAvaloniaColor).ConvertAll (color => color.ToString ())));
		settings.PutSetting ("Pintora.Palette.Colors", string.Join (",", palette_colors.ConvertAll (ToAvaloniaColor).ConvertAll (color => color.ToString ())));
	}

	private void LoadRecentColors ()
	{
		foreach (string value in settings.GetSetting ("Pintora.Palette.Recent", string.Empty).Split (',', StringSplitOptions.RemoveEmptyEntries)) {
			if (global::Avalonia.Media.Color.TryParse (value, out global::Avalonia.Media.Color color))
				recent_colors.Add (ToColorBgra (color));
			if (recent_colors.Count == 10)
				break;
		}
	}

	private void LoadPaletteColors ()
	{
		foreach (string value in settings.GetSetting ("Pintora.Palette.Colors", string.Empty).Split (',', StringSplitOptions.RemoveEmptyEntries)) {
			if (global::Avalonia.Media.Color.TryParse (value, out global::Avalonia.Media.Color color))
				palette_colors.Add (ToColorBgra (color));
			if (palette_colors.Count == 32)
				break;
		}
	}

	private ColorBgra LoadColor (string key, ColorBgra fallback)
	{
		string value = settings.GetSetting (key, string.Empty);
		return global::Avalonia.Media.Color.TryParse (value, out global::Avalonia.Media.Color color)
			? ColorBgra.FromBgra (color.B, color.G, color.R, color.A)
			: fallback;
	}

	private static global::Avalonia.Media.Color ToAvaloniaColor (ColorBgra color)
		=> global::Avalonia.Media.Color.FromArgb (color.A, color.R, color.G, color.B);

	private static ColorBgra ToColorBgra (global::Avalonia.Media.Color color)
		=> ColorBgra.FromBgra (color.B, color.G, color.R, color.A);

	private void RefreshToolOptions (CanvasHost canvas)
	{
		if (tool_options is null)
			return;
		tool_options.Children.Clear ();
		if (antialias_toggle is not null)
			antialias_toggle.IsChecked = canvas.ShapeAntialias;
		switch (canvas.ActiveTool) {
			case CanvasHost.CanvasTool.Selection:
				tool_options.Children.Add (CreateBrushButton ("Select all", () => canvas.SelectAll ()));
				tool_options.Children.Add (CreateBrushButton ("Deselect", canvas.ClearSelection));
				break;
			case CanvasHost.CanvasTool.Text:
				tool_options.Children.Add (CreateToolbarLabel ("Text:"));
				TextAnnotation? selected_text = canvas.SelectedAnnotation as TextAnnotation;
				var text_input = new TextBox { Text = selected_text?.Text ?? canvas.TextToolText, Width = 160 };
				text_input.TextChanged += (_, _) => {
					string value = text_input.Text ?? string.Empty;
					if (selected_text is not null && canvas.UpdateTextAnnotation (selected_text, value))
						selected_text = canvas.SelectedAnnotation as TextAnnotation;
					else
						canvas.TextToolText = value;
				};
				tool_options.Children.Add (text_input);
				tool_options.Children.Add (CreateToolbarLabel ("Font:"));
				var font = new ComboBox { ItemsSource = new[] { "Inter", "Sans", "Serif" }, SelectedItem = selected_text?.FontFamily ?? canvas.TextToolFontFamily, MinWidth = 92 };
				font.SelectionChanged += (_, _) => {
					string value = font.SelectedItem?.ToString () ?? "Inter";
					if (selected_text is not null && canvas.UpdateTextAnnotation (selected_text, selected_text.Text, fontFamily: value))
						selected_text = canvas.SelectedAnnotation as TextAnnotation;
					else
						canvas.TextToolFontFamily = value;
				};
				tool_options.Children.Add (font);
				tool_options.Children.Add (CreateToolbarLabel ("Size:"));
				var text_size = new NumericUpDown { Minimum = 1, Maximum = 256, Value = (decimal) (selected_text?.FontSize ?? canvas.TextToolFontSize), Width = 72 };
				text_size.ValueChanged += (_, args) => {
					double value = (double) (args.NewValue ?? 16);
					if (selected_text is not null && canvas.UpdateTextAnnotation (selected_text, selected_text.Text, value))
						selected_text = canvas.SelectedAnnotation as TextAnnotation;
					else
						canvas.TextToolFontSize = value;
				};
				tool_options.Children.Add (text_size);
				var bold = new CheckBox { Content = "Bold", IsChecked = selected_text?.FontWeight == FontWeight.Bold || canvas.TextToolBold };
				bold.IsCheckedChanged += (_, _) => {
					if (selected_text is not null && canvas.UpdateTextAnnotation (selected_text, selected_text.Text, fontWeight: bold.IsChecked == true ? FontWeight.Bold : FontWeight.Normal))
						selected_text = canvas.SelectedAnnotation as TextAnnotation;
					else
						canvas.TextToolBold = bold.IsChecked == true;
				};
				tool_options.Children.Add (bold);
				var underline = new CheckBox { Content = "Underline", IsChecked = selected_text?.Underline ?? canvas.TextToolUnderline };
				underline.IsCheckedChanged += (_, _) => {
					if (selected_text is not null && canvas.UpdateTextAnnotation (selected_text, selected_text.Text, underline: underline.IsChecked == true))
						selected_text = canvas.SelectedAnnotation as TextAnnotation;
					else
						canvas.TextToolUnderline = underline.IsChecked == true;
				};
				tool_options.Children.Add (underline);
				break;
			case CanvasHost.CanvasTool.Brush:
				tool_options.Children.Add (CreateToolbarLabel ("Tool:"));
				tool_options.Children.Add (new TextBlock { Text = "✎", FontSize = 18, VerticalAlignment = VerticalAlignment.Center });
				tool_options.Children.Add (CreateToolbarLabel ("Brush width:"));
				tool_options.Children.Add (CreateBrushSizeInput (canvas));
				tool_options.Children.Add (CreateToolbarLabel ("Type:"));
				var brush_type = new ComboBox {
					ItemsSource = new[] { "Normal", "Soft" },
					SelectedIndex = canvas.BrushHardness < 1 ? 1 : 0,
					MinWidth = 92
				};
				brush_type.SelectionChanged += (_, _) => {
					if (brush_type.SelectedIndex >= 0)
						canvas.BrushHardness = brush_type.SelectedIndex == 0 ? 1 : 0.45;
				};
				tool_options.Children.Add (brush_type);
				tool_options.Children.Add (CreateToolbarLabel ("Opacity:"));
				tool_options.Children.Add (CreateBrushSlider (0, 1, canvas.BrushOpacity, value => canvas.BrushOpacity = value, 90));
				tool_options.Children.Add (CreateToolbarLabel ("Stroke:"));
				var stroke = new ComboBox { ItemsSource = new[] { "Round", "Hard" }, SelectedIndex = canvas.BrushHardness < 1 ? 0 : 1, MinWidth = 92 };
				stroke.SelectionChanged += (_, _) => { if (stroke.SelectedIndex >= 0) canvas.BrushHardness = stroke.SelectedIndex == 0 ? 0.45 : 1; };
				tool_options.Children.Add (stroke);
				break;
			case CanvasHost.CanvasTool.Pencil:
				break;
			case CanvasHost.CanvasTool.Shapes:
				tool_options.Children.Add (new TextBlock { Text = "Shape:", VerticalAlignment = VerticalAlignment.Center });
				var shape = new ComboBox {
					ItemsSource = new[] { "Line", "Rectangle", "Ellipse", "Rounded rectangle" },
					SelectedIndex = (int) canvas.SelectedShape,
					MinWidth = 135
				};
				tool_options.Children.Add (shape);
				tool_options.Children.Add (new TextBlock { Text = "Fill:", VerticalAlignment = VerticalAlignment.Center });
				var fill = new ComboBox {
					ItemsSource = new[] { "Outline", "Fill", "Fill + outline" },
					SelectedIndex = (int) canvas.FillStyle,
					MinWidth = 130,
					IsEnabled = canvas.SelectedShape != CanvasHost.ShapeKind.Line
				};
				fill.SelectionChanged += (_, _) => {
					if (fill.SelectedIndex >= 0) canvas.FillStyle = (CanvasHost.ShapeFillStyle) fill.SelectedIndex;
				};
				tool_options.Children.Add (fill);
				tool_options.Children.Add (new TextBlock { Text = "Width:", VerticalAlignment = VerticalAlignment.Center });
				tool_options.Children.Add (CreateShapeWidthInput (canvas));
				tool_options.Children.Add (new TextBlock { Text = "Dash:", VerticalAlignment = VerticalAlignment.Center });
				var dash = new ComboBox {
					ItemsSource = new[] { "Solid  ━━━", "Dash  ━ ━", "Dot  · · ·", "Dash-dot  ━ ·" },
					SelectedIndex = (int) canvas.DashStyle,
					MinWidth = 135
				};
				dash.SelectionChanged += (_, _) => {
					if (dash.SelectedIndex >= 0) canvas.DashStyle = (CanvasHost.ShapeDashStyle) dash.SelectedIndex;
				};
				tool_options.Children.Add (dash);
				var corner = new NumericUpDown { Minimum = 1, Maximum = 256, Value = canvas.ShapeCornerRadius, Width = 70, IsVisible = canvas.SelectedShape == CanvasHost.ShapeKind.RoundedRectangle };
				corner.ValueChanged += (_, args) => canvas.ShapeCornerRadius = (int) (args.NewValue ?? 8);
				tool_options.Children.Add (corner);
				shape.SelectionChanged += (_, _) => {
					if (shape.SelectedIndex >= 0) {
						CanvasHost.ShapeKind selected = (CanvasHost.ShapeKind) shape.SelectedIndex;
						canvas.SelectedShape = selected;
						fill.IsEnabled = selected != CanvasHost.ShapeKind.Line;
						corner.IsVisible = selected == CanvasHost.ShapeKind.RoundedRectangle;
					}
				};
				break;
			case CanvasHost.CanvasTool.Fill:
				tool_options.Children.Add (new TextBlock { Text = "Tolerance", VerticalAlignment = VerticalAlignment.Center });
				tool_options.Children.Add (CreateBrushSlider (0, 255, canvas.FillTolerance, value => canvas.FillTolerance = (int) value, 90));
				break;
			case CanvasHost.CanvasTool.Gradient:
				tool_options.Children.Add (CreateToolbarLabel ("From:"));
				var gradient_primary = CreateToolbarColorButton (canvas, true);
				tool_options.Children.Add (gradient_primary);
				tool_options.Children.Add (CreateToolbarLabel ("To:"));
				var gradient_secondary = CreateToolbarColorButton (canvas, false);
				tool_options.Children.Add (gradient_secondary);
				var reverse = new CheckBox { Content = "Reverse", IsChecked = canvas.GradientReversed };
				reverse.IsCheckedChanged += (_, _) => canvas.GradientReversed = reverse.IsChecked == true;
				tool_options.Children.Add (reverse);
				break;
			case CanvasHost.CanvasTool.Blur:
			case CanvasHost.CanvasTool.Pixelate:
			case CanvasHost.CanvasTool.Highlight:
			case CanvasHost.CanvasTool.Magnify:
			case CanvasHost.CanvasTool.Spotlight:
				EffectToolOptions effect_options = canvas.GetEffectOptions (canvas.ActiveTool);
				tool_options.Children.Add (CreateToolbarLabel ("Strength:"));
				tool_options.Children.Add (CreateBrushSlider (0, 1, effect_options.Strength, value =>
				{
					canvas.GetEffectOptions (canvas.ActiveTool).Strength = value;
					canvas.UpdateSelectedEffectOptions (strength: value);
				}, 90));
				if (canvas.ActiveTool == CanvasHost.CanvasTool.Pixelate) {
					tool_options.Children.Add (CreateToolbarLabel ("Pixel size:"));
					var pixel_size = new NumericUpDown { Minimum = 2, Maximum = 64, Value = effect_options.PixelSize, Width = 70 };
					pixel_size.ValueChanged += (_, args) => {
						int value = (int) (args.NewValue ?? 8);
						canvas.GetEffectOptions (canvas.ActiveTool).PixelSize = value;
						canvas.UpdateSelectedEffectOptions (pixelSize: value);
					};
					tool_options.Children.Add (pixel_size);
				}
				break;
			case CanvasHost.CanvasTool.Eraser:
				tool_options.Children.Add (new TextBlock { Text = "Size", VerticalAlignment = VerticalAlignment.Center });
				tool_options.Children.Add (CreateBrushSizeInput (canvas));
				break;
		}
	}

	private Button CreateToolbarColorButton (CanvasHost canvas, bool primary)
	{
		var button = new Button { Width = 34, Height = 28, Padding = new global::Avalonia.Thickness (0) };
		void Refresh () => button.Background = new SolidColorBrush (ToAvaloniaColor (primary ? canvas.BrushColor : canvas.SecondaryBrushColor));
		Refresh ();
		button.Click += (_, _) => new ColorDialogWindow (ToAvaloniaColor (primary ? canvas.BrushColor : canvas.SecondaryBrushColor), color => {
			if (primary)
				canvas.BrushColor = ColorBgra.FromBgra (color.B, color.G, color.R, color.A);
			else
				canvas.SecondaryBrushColor = ColorBgra.FromBgra (color.B, color.G, color.R, color.A);
			SavePaletteColor (canvas);
			Refresh ();
		}).Show (this);
		ToolTip.SetTip (button, primary ? "Gradient start color" : "Gradient end color");
		return button;
	}

	private static Button CreateToolButton (CanvasHost canvas, string label, CanvasHost.CanvasTool tool, string path)
	{
		var button = new Button {
			Content = new PathIcon { Data = StreamGeometry.Parse (path), Width = 20, Height = 20 },
			Width = 34,
			Height = 34,
			Padding = new global::Avalonia.Thickness (5),
			HorizontalContentAlignment = HorizontalAlignment.Center,
			Classes = { "tool-button" }
		};
		if (tool == canvas.ActiveTool)
			button.Classes.Add ("active");
		ToolTip.SetTip (button, label);
		return button;
	}

	private static void UpdateToolButtons (IEnumerable<(Button Button, CanvasHost.CanvasTool Tool)> buttons, CanvasHost.CanvasTool active)
	{
		foreach ((Button button, CanvasHost.CanvasTool tool) in buttons)
			button.Classes.Set ("active", active == tool);
	}

	private async Task<CloseDecision> PromptCloseDecisionAsync (DocumentTab tab)
	{
		if (!tab.IsDirty)
			return CloseDecision.Discard;

		var result = new TaskCompletionSource<CloseDecision> (TaskCreationOptions.RunContinuationsAsynchronously);
		var dialog = new Window {
			Title = $"Save changes to {tab.Title}?",
			Width = 420,
			Height = 160,
			CanResize = false,
			Content = new StackPanel {
				Margin = new Thickness (16),
				Spacing = 12,
				Children = {
					new TextBlock { Text = $"{tab.Title} has unsaved changes." },
					new StackPanel {
						Orientation = Orientation.Horizontal,
						HorizontalAlignment = HorizontalAlignment.Right,
						Spacing = 8,
						Children = {
							new Button { Content = "Cancel", Tag = CloseDecision.Cancel },
							new Button { Content = "Discard", Tag = CloseDecision.Discard },
							new Button { Content = "Save", Tag = CloseDecision.Save }
						}
					}
				}
			}
		};
		if (dialog.Content is StackPanel root && root.Children[1] is StackPanel buttons)
			foreach (Button button in buttons.Children)
				button.Click += (_, _) => {
					result.TrySetResult ((CloseDecision) button.Tag!);
					dialog.Close ();
				};
		dialog.Closed += (_, _) => result.TrySetResult (CloseDecision.Cancel);
		await dialog.ShowDialog (this);
		return await result.Task;
	}

	private async Task<CloseDecision> RequestCloseDecisionAsync (DocumentTab tab)
	{
		CloseDecision decision = await PromptCloseDecisionAsync (tab);
		if (decision == CloseDecision.Save && !await SaveTabAsync (tab))
			return CloseDecision.Cancel;
		return decision;
	}

	private async Task SaveActiveTabAsync ()
	{
		DocumentTab? tab = tab_manager.ActiveTab;
		if (tab is null)
			return;
		await SaveTabAsync (tab);
	}

	private async Task SaveAllDocumentsAsync ()
		=> await close_coordinator.SaveAllAsync ();

	private async Task CloseActiveTabAsync ()
	{
		DocumentTab? tab = tab_manager.ActiveTab;
		if (tab is not null)
			await close_coordinator.CloseTabAsync (tab);
	}

	private async Task CloseAllDocumentsAsync ()
		=> await close_coordinator.CloseAllAsync ();

	private async Task ExitAsync ()
	{
		if (await close_coordinator.TryExitAsync ())
			Close ();
	}

	private async Task<bool> SaveTabAsync (DocumentTab tab)
	{
		string? path = tab.Session.FilePath;
		if (path is null) {
			if (StorageProvider is null)
				return false;
			var file = await StorageProvider.SaveFilePickerAsync (new FilePickerSaveOptions {
				SuggestedFileName = "pintora.png",
				DefaultExtension = "png",
				FileTypeChoices = new[] { new FilePickerFileType ("PNG image") { Patterns = new[] { "*.png" } } }
			});
			if (file is null)
				return false;
			path = file.Path.LocalPath;
		}
		try {
			tab.Canvas.SaveImage (path);
			tab.Session.MarkSaved (path);
			return true;
		} catch (Exception exception) {
			System.Diagnostics.Debug.WriteLine ($"Failed to save '{path}': {exception.Message}");
			await ShowErrorAsync ($"Could not save '{tab.Title}'.", exception.Message);
			return false;
		}
	}

	private async Task ShowErrorAsync (string title, string message)
	{
		var dialog = new Window {
			Title = title,
			Width = 420,
			Height = 160,
			CanResize = false,
			Content = new StackPanel {
				Margin = new Thickness (16),
				Spacing = 12,
				Children = {
					new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
					new Button {
						Content = "OK",
						HorizontalAlignment = HorizontalAlignment.Right,
					}
				}
			}
		};
		if (dialog.Content is StackPanel root && root.Children[1] is Button ok)
			ok.Click += (_, _) => dialog.Close ();
		await dialog.ShowDialog (this);
	}

	private async void OnWindowClosing (object? sender, WindowClosingEventArgs e)
	{
		if (closing_after_confirmation)
			return;
		if (close_in_progress) {
			e.Cancel = true;
			return;
		}
		e.Cancel = true;
		close_in_progress = true;
		try {
			if (!await close_coordinator.TryExitAsync ())
				return;
			closing_after_confirmation = true;
			Close ();
		} catch (Exception exception) {
			System.Diagnostics.Debug.WriteLine ($"Failed to close window: {exception.Message}");
		} finally {
			close_in_progress = false;
		}
	}

	private Control CreateCanvasPlaceholder (CanvasHost canvas)
	{
		tab_strip.AttachAsync (tab_manager, RequestCloseDecisionAsync);
		var tabs_host = new Border {
			Child = tab_strip
		};
		tab_strip_host = tabs_host;
		var canvas_panel = new Border {
			Margin = new global::Avalonia.Thickness (96, 10, 252, 190),
			Child = new DockPanel {
				Children = {
					tabs_host,
					canvas_host
				}
			}
		};
		DockPanel.SetDock (tabs_host, Dock.Top);
		canvas_panel_control = canvas_panel;
		return canvas_panel;
	}

	private void SyncActiveCanvas ()
	{
		canvas_host.Content = tab_manager.ActiveTab?.Canvas;
	}

	/// <summary>
	/// Moves the per-canvas event subscriptions and the thumbnail provider to
	/// the currently active tab. The previous canvas is detached first so
	/// handlers never accumulate across tab switches.
	/// </summary>
	private void RebindActiveCanvas ()
	{
		if (subscribed_canvas is not null) {
			subscribed_canvas.HistoryChanged -= OnActiveCanvasHistoryChanged;
			subscribed_canvas.ScaleChanged -= OnActiveCanvasScaleChanged;
			subscribed_canvas.ActiveToolChanged -= OnActiveCanvasToolOptionsChanged;
			subscribed_canvas.SelectedShapeChanged -= OnActiveCanvasToolOptionsChanged;
			subscribed_canvas.BrushSizeChanged -= OnActiveCanvasToolOptionsChanged;
			subscribed_canvas.PointerPositionChanged -= OnActiveCanvasToolChanged;
			subscribed_canvas.ColorsChanged -= OnActiveCanvasColorsChanged;
			subscribed_canvas.LayersChanged -= OnActiveCanvasLayersChanged;
			subscribed_canvas.ActiveLayerChanged -= OnActiveCanvasLayersChanged;
			subscribed_canvas.ToolOptionsChanged -= OnActiveCanvasToolOptionsChanged;
			subscribed_canvas.AnnotationSelectionChanged -= OnActiveCanvasToolOptionsChanged;
			subscribed_canvas.DocumentStateChanged -= OnActiveCanvasDocumentStateChanged;
			subscribed_canvas = null;
		}

		if (thumbnail_provider is not null && thumbnail_updated_handler is not null)
			thumbnail_provider.ThumbnailUpdated -= thumbnail_updated_handler;
		thumbnail_updated_handler = null;
		thumbnail_provider?.Dispose ();
		thumbnail_provider = null;

		CanvasHost? active = tab_manager.ActiveTab?.Canvas;
		if (active is null)
			return;

		thumbnail_provider = new LayerThumbnailProvider (active);
		subscribed_canvas = active;
		active.HistoryChanged += OnActiveCanvasHistoryChanged;
		active.ScaleChanged += OnActiveCanvasScaleChanged;
		active.ActiveToolChanged += OnActiveCanvasToolOptionsChanged;
		active.SelectedShapeChanged += OnActiveCanvasToolOptionsChanged;
		active.BrushSizeChanged += OnActiveCanvasToolOptionsChanged;
		active.PointerPositionChanged += OnActiveCanvasToolChanged;
		active.ColorsChanged += OnActiveCanvasColorsChanged;
		active.LayersChanged += OnActiveCanvasLayersChanged;
		active.ActiveLayerChanged += OnActiveCanvasLayersChanged;
		active.ToolOptionsChanged += OnActiveCanvasToolOptionsChanged;
		active.AnnotationSelectionChanged += OnActiveCanvasToolOptionsChanged;
		active.DocumentStateChanged += OnActiveCanvasDocumentStateChanged;

		refresh_history_commands?.Invoke ();
		refresh_status?.Invoke ();
		refresh_zoom?.Invoke ();
		refresh_layers?.Invoke ();
		UpdateDocumentTitle (active, version);
	}

	private void OnActiveCanvasHistoryChanged (object? sender, EventArgs e)
		=> refresh_history_commands?.Invoke ();

	private void OnActiveCanvasDocumentStateChanged (object? sender, EventArgs e)
	{
		if (sender is CanvasHost canvas)
			UpdateDocumentTitle (canvas, version);
	}

	private void OnActiveCanvasScaleChanged (object? sender, EventArgs e)
	{
		refresh_status?.Invoke ();
		refresh_zoom?.Invoke ();
	}

	private void OnActiveCanvasToolChanged (object? sender, EventArgs e)
		=> refresh_status?.Invoke ();

	private void OnActiveCanvasToolOptionsChanged (object? sender, EventArgs e)
	{
		refresh_status?.Invoke ();
		if (sender is CanvasHost canvas) {
			// Tool selection is editor-global; document-specific options remain
			// owned by each CanvasHost.
			if (ReferenceEquals (canvas, ActiveCanvas))
				active_tool = canvas.ActiveTool;
			RefreshToolOptions (canvas);
		}
	}

	private void OnActiveCanvasColorsChanged (object? sender, EventArgs e)
		=> refresh_status?.Invoke ();

	private void OnActiveCanvasLayersChanged (object? sender, EventArgs e)
		=> refresh_layers?.Invoke ();

	private CanvasHost CreateCanvasHost (PintoraKeyboardSettings keyboard)
	{
		MemoryImageSurface surface = new (800, 600);
		surface.Fill (ColorBgra.White);

		CanvasHost canvas = new (new Pinta.Core.Size (surface.Width, surface.Height), surface, keyboard);
		ApplyGridViewSettings (canvas);
		canvas.ShowRulers = view_settings.ShowRulers;
		return canvas;
	}

	private Control CreateLayersPanel ()
	{
		var layers = new StackPanel { Spacing = 3 };
		// Maps each layer to the Image control currently displaying it. Rebuilt
		// on every refresh; the single provider subscription below looks up the
		// live control instead of capturing per-row handlers.
		var thumbnails = new Dictionary<AvaloniaLayer, Image> ();
		Slider? opacity_slider = null;
		void RebuildLayers ()
		{
			CanvasHost canvas = ActiveCanvas;
			LayerThumbnailProvider? provider = thumbnail_provider;
			int? drag_source = null;
			layers.Children.Clear ();
			thumbnails.Clear ();
			for (int i = canvas.Layers.Count - 1; i >= 0; i--) {
				int index = i;
				AvaloniaLayer layer = canvas.Layers[index];
				var visibility = new CheckBox { IsChecked = layer.IsVisible };
				ToolTip.SetTip (visibility, "Layer visible");
				var locked = new CheckBox { IsChecked = layer.IsLocked };
				ToolTip.SetTip (locked, "Layer locked");
				var name = new TextBlock {
					Text = layer.Name,
					VerticalAlignment = VerticalAlignment.Center,
					MaxWidth = 96,
					TextTrimming = TextTrimming.CharacterEllipsis
				};
				visibility.IsCheckedChanged += (_, _) => {
					ActiveCanvas.SetLayerVisibility (index, visibility.IsChecked == true);
				};
				locked.IsCheckedChanged += (_, _) => ActiveCanvas.SetLayerLocked (index, locked.IsChecked == true);
				var thumbnail = new Image { Source = provider?.GetThumbnail (layer), Width = 42, Height = 28, Stretch = Stretch.Uniform };
				thumbnails[layer] = thumbnail;
				var button = new Button {
					Content = new StackPanel {
						Orientation = Orientation.Horizontal,
						Spacing = 4,
						Children = {
						new TextBlock { Text = index == canvas.ActiveLayerIndex ? "▣" : "□" },
						thumbnail,
						visibility, name, locked
					}
					},
					HorizontalContentAlignment = HorizontalAlignment.Left,
					Padding = new global::Avalonia.Thickness (4, 2)
				};
				button.PointerPressed += (_, args) => {
					if (args.GetCurrentPoint (button).Properties.IsLeftButtonPressed)
						drag_source = index;
				};
				button.PointerReleased += (_, args) => {
					if (drag_source is int source && source != index)
						ActiveCanvas.MoveLayerTo (source, index);
					drag_source = null;
					RebuildLayers ();
				};
				button.Click += (_, _) => ActiveCanvas.SelectLayer (index);
				button.ContextMenu = new ContextMenu {
					Items = {
						CreateMenuItem ("Rename", () => new LayerRenameWindow (ActiveCanvas, ActiveCanvas.Layers[index].Name, RebuildLayers).Show (this)),
						CreateMenuItem ("Duplicate", () => { ActiveCanvas.SelectLayer (index); ActiveCanvas.DuplicateActiveLayer (); RebuildLayers (); }),
						CreateMenuItem ("Delete", () => { ActiveCanvas.SelectLayer (index); ActiveCanvas.RemoveLayer (); RebuildLayers (); }),
						new Separator (),
						CreateMenuItem ("Move up", () => { ActiveCanvas.SelectLayer (index); ActiveCanvas.MoveLayer (1); RebuildLayers (); }),
						CreateMenuItem ("Move down", () => { ActiveCanvas.SelectLayer (index); ActiveCanvas.MoveLayer (-1); RebuildLayers (); }),
						CreateMenuItem ("Merge down", () => { ActiveCanvas.SelectLayer (index); ActiveCanvas.MergeActiveLayerDown (); RebuildLayers (); })
					}
				};
				layers.Children.Add (button);
			}
			if (opacity_slider is not null && canvas.Layers.Count > 0)
				opacity_slider.Value = canvas.Layers[canvas.ActiveLayerIndex].Opacity;
		}
		opacity_slider = CreateOpacitySlider ();
		var controls = new StackPanel {
			Spacing = 5,
			Children = {
			new TextBlock { Text = "Layers", FontSize = 12 },
			new ScrollViewer { MaxHeight = 280, Content = layers },
			new TextBlock { Text = "Opacity", FontSize = 11 },
			opacity_slider,
			new WrapPanel { Width = 216, ItemWidth = 43, ItemHeight = 34, Children = {
				CreateCompactCommand ("+", "Add layer", () => { ActiveCanvas.AddLayer (); RebuildLayers (); }),
				CreateCompactCommand ("−", "Delete layer", () => { ActiveCanvas.RemoveLayer (); RebuildLayers (); }),
				CreateCompactCommand ("⧉", "Duplicate layer", () => { ActiveCanvas.DuplicateActiveLayer (); RebuildLayers (); }),
				CreateCompactCommand ("↑", "Move layer up", () => { ActiveCanvas.MoveLayer (1); RebuildLayers (); }),
				CreateCompactCommand ("↓", "Move layer down", () => { ActiveCanvas.MoveLayer (-1); RebuildLayers (); }),
				CreateCompactCommand ("⧨", "Merge layer down", () => { ActiveCanvas.MergeActiveLayerDown (); RebuildLayers (); }),
				CreateCompactCommand ("✎", "Rename layer", () => new LayerRenameWindow (ActiveCanvas, ActiveCanvas.Layers[ActiveCanvas.ActiveLayerIndex].Name, RebuildLayers).Show (this)),
				CreateCompactCommand ("↶", "Undo layer operation", () => { ActiveCanvas.UndoLayerOperation (); RebuildLayers (); }),
				CreateCompactCommand ("↷", "Redo layer operation", () => { ActiveCanvas.RedoLayerOperation (); RebuildLayers (); })
			} }
		}
		};
		RebuildLayers ();
		refresh_layers = RebuildLayers;
		// Single subscription for the lifetime of the panel. Rebuilds only
		// replace the dictionary entries, so no handler can outlive its row.
		// The handler reads the current provider via the field so it never
		// holds a reference to a disposed provider after a tab switch.
		EventHandler<LayerThumbnailUpdatedEventArgs> handler = (_, e) => {
			if (e.Thumbnail is not null && thumbnails.TryGetValue (e.Layer, out Image? image))
				image.Source = e.Thumbnail;
		};
		thumbnail_updated_handler = handler;
		if (thumbnail_provider is not null)
			thumbnail_provider.ThumbnailUpdated += handler;
		rebuild_layers_panel = () => {
			// RebindActiveCanvas has already disposed the previous provider and
			// created a fresh one; re-attach the handler to the new instance so
			// no row keeps a reference to a disposed provider.
			if (thumbnail_provider is not null)
				thumbnail_provider.ThumbnailUpdated += handler;
			RebuildLayers ();
		};
		var panel = new Border {
			Width = 232,
			Padding = new global::Avalonia.Thickness (7),
			Margin = new global::Avalonia.Thickness (8),
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Top,
			BorderThickness = new global::Avalonia.Thickness (1),
			Child = controls
		};
		panel.Bind (Border.BackgroundProperty, new DynamicResourceExtension ("Pintora.Panel.Background"));
		panel.Bind (Border.BorderBrushProperty, new DynamicResourceExtension ("Pintora.Control.Border"));
		return panel;
	}

	private Slider CreateOpacitySlider ()
	{
		CanvasHost canvas = ActiveCanvas;
		var slider = new Slider { Minimum = 0, Maximum = 1, Value = canvas.Layers.Count == 0 ? 1 : canvas.Layers[canvas.ActiveLayerIndex].Opacity };
		slider.ValueChanged += (_, args) => {
			CanvasHost active = ActiveCanvas;
			if (active.Layers.Count == 0)
				return;
			active.SetActiveLayerOpacity (args.NewValue);
		};
		return slider;
	}

}
