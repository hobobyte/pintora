using System;
using System.Collections.Generic;
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
	private readonly IImageSourceProvider image_source_provider;
	private readonly IClipboardService? clipboard_service;
	private readonly List<ColorBgra> recent_colors = new ();
	private readonly List<ColorBgra> palette_colors = new ();
	private LayerThumbnailProvider? thumbnail_provider;
	private EventHandler<LayerThumbnailUpdatedEventArgs>? thumbnail_updated_handler;
	private Action? refresh_layers;
	private StackPanel? tool_options;
	private readonly EditorCommandRegistry commands = new ();
	private static readonly CanvasHost.CanvasTool[] EffectTools = [CanvasHost.CanvasTool.Blur, CanvasHost.CanvasTool.Pixelate, CanvasHost.CanvasTool.Highlight, CanvasHost.CanvasTool.Magnify, CanvasHost.CanvasTool.Spotlight];

	public MainWindow ()
	{
		string version = typeof (MainWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute> ()?.InformationalVersion
			?? "0.0.1-alpha";
		int buildMetadata = version.IndexOf ('+');
		if (buildMetadata >= 0)
			version = version[..buildMetadata];
		Title = $"Pintora {version} — Simple. Powerful. Creative.";
		Width = 1200;
		Height = 760;
		MinWidth = 900;
		MinHeight = 600;

		settings = new SettingsManager ();
		App.InitializeTheme (settings);
		keyboard = new PintoraKeyboardSettings (settings);
		CanvasHost canvas = CreateCanvasHost (keyboard);
		clipboard_service = Clipboard is null ? null : new AvaloniaClipboardService (Clipboard);
		image_source_provider = new FileImageSourceProvider (clipboard_service);
		canvas.DocumentStateChanged += (_, _) => UpdateDocumentTitle (canvas, version);
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
		string saved_tool_name = settings.GetSetting ("Pintora.Tool", "Brush");
		if (Enum.TryParse (saved_tool_name, out CanvasHost.ShapeKind legacy_shape)) {
			canvas.SelectedShape = legacy_shape;
			canvas.ActiveTool = CanvasHost.CanvasTool.Shapes;
		} else if (Enum.TryParse (saved_tool_name, out CanvasHost.CanvasTool saved_tool) && Enum.IsDefined (saved_tool))
			canvas.ActiveTool = saved_tool;
		Closed += (_, _) => {
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
	}

	private Control CreateCommandBar (CanvasHost canvas)
	{
		var commands = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
		commands.Children.Add (CreateCompactCommand ("New", "New image", () => new NewDocumentWindow (canvas).Show (this)));
		commands.Children.Add (CreateCompactCommand ("Open", "Open image", () => _ = OpenImageAsync (canvas)));
		commands.Children.Add (CreateCompactCommand ("Save", "Save image as", () => _ = SaveAsAsync (canvas)));
		commands.Children.Add (new Separator { Width = 1, Margin = new global::Avalonia.Thickness (6, 4) });
		var undo = CreateCompactCommand ("↶", "Undo", () => this.commands.TryExecute (PintoraKeyCommands.Undo.Id));
		var redo = CreateCompactCommand ("↷", "Redo", () => this.commands.TryExecute (PintoraKeyCommands.Redo.Id));
		void RefreshHistory () { undo.IsEnabled = canvas.CanUndoStroke; redo.IsEnabled = canvas.CanRedoStroke; }
		canvas.HistoryChanged += (_, _) => RefreshHistory ();
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
		var toolbar = new Border {
			Padding = new global::Avalonia.Thickness (8, 4),
			Child = new ScrollViewer {
				HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
				VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
				Content = tool_options
			}
		};
		RefreshToolOptions (canvas);
		toolbar.Bind (Border.BackgroundProperty, new DynamicResourceExtension ("Pintora.Toolbar.Background"));
		Grid.SetRow (toolbar, 2);
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

	private static Control CreateStatusBar (CanvasHost canvas)
	{
		var dimensions = new TextBlock { Text = $"{canvas.ImageSize.Width} × {canvas.ImageSize.Height} px" };
		var details = new TextBlock ();
		void RefreshDetails () {
			string position = canvas.PointerPosition is PointD point ? $"  •  X: {point.X:0}  Y: {point.Y:0}" : string.Empty;
			details.Text = $"{(canvas.ActiveTool == CanvasHost.CanvasTool.Shapes ? canvas.SelectedShape.ToString () : canvas.ActiveTool.ToString ())}  •  Brush {canvas.BrushSize}px{position}";
		}
		RefreshDetails ();
		canvas.ScaleChanged += (_, _) => RefreshDetails ();
		canvas.ActiveToolChanged += (_, _) => RefreshDetails ();
		canvas.SelectedShapeChanged += (_, _) => RefreshDetails ();
		canvas.BrushSizeChanged += (_, _) => RefreshDetails ();
		canvas.PointerPositionChanged += (_, _) => RefreshDetails ();
		var zoom = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
		void RefreshZoom () => zoom.Text = canvas.Scale.ToString ("P0");
		RefreshZoom ();
		canvas.ScaleChanged += (_, _) => RefreshZoom ();
		var zoom_controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Children = {
			dimensions,
			CreateCompactCommand ("−", "Zoom out", () => canvas.SetScale (Math.Max (0.1, canvas.Scale / 1.25))),
			zoom,
			CreateCompactCommand ("+", "Zoom in", () => canvas.SetScale (Math.Min (32, canvas.Scale * 1.25))),
			CreateCompactCommand ("Fit", "Fit canvas to window", canvas.FitToWindow)
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
		return status;
	}

	private Menu CreateMainMenu (CanvasHost canvas)
	{
		var preferences = new MenuItem { Header = "Preferences" };
		preferences.Click += (_, _) => new SettingsWindow (canvas, settings, keyboard).Show (this);
		MenuItem undo = CreateMenuItem ("_Undo", () => commands.TryExecute (PintoraKeyCommands.Undo.Id), keyboard, PintoraKeyCommands.Undo);
		MenuItem redo = CreateMenuItem ("_Redo", () => commands.TryExecute (PintoraKeyCommands.Redo.Id), keyboard, PintoraKeyCommands.Redo);
		MenuItem layer_undo = CreateMenuItem ("Undo layer", () => canvas.UndoLayerOperation ());
		MenuItem layer_redo = CreateMenuItem ("Redo layer", () => canvas.RedoLayerOperation ());
		MenuItem clear_history = CreateMenuItem ("Clear history", () => commands.TryExecute (PintoraKeyCommands.ClearHistory.Id), keyboard, PintoraKeyCommands.ClearHistory);
		void RefreshHistoryCommands () {
			undo.IsEnabled = canvas.CanUndoStroke;
			redo.IsEnabled = canvas.CanRedoStroke;
			layer_undo.IsEnabled = canvas.CanUndoLayerOperation;
			layer_redo.IsEnabled = canvas.CanRedoLayerOperation;
			clear_history.IsEnabled = canvas.History.Count > 0;
		}
		canvas.HistoryChanged += (_, _) => RefreshHistoryCommands ();
		RefreshHistoryCommands ();
		var menu = new Menu {
			Items = {
				new MenuItem { Header = "_File", Items = {
					CreateMenuItem ("_New", () => new NewDocumentWindow (canvas).Show (this)),
					CreateMenuItem ("_Open", () => _ = OpenImageAsync (canvas)),
					CreateMenuItem ("Import image as layer", () => _ = ImportImageLayerAsync (canvas)),
					CreateMenuItem ("Insert image annotation", () => _ = InsertImageAnnotationAsync (canvas)),
					new Separator (),
					CreateMenuItem ("Save _As", () => _ = SaveAsAsync (canvas)),
					CreateMenuItem ("Save _PNG", () => _ = SavePngAsync (canvas)),
					CreateMenuItem ("Save _JPEG", () => _ = SaveJpegAsync (canvas)),
					CreateMenuItem ("Save _WebP", () => _ = SaveWebpAsync (canvas)),
					CreateMenuItem ("Save _TIFF", () => _ = SaveTiffAsync (canvas)),
					CreateMenuItem ("Save _ORA", () => _ = SaveOraAsync (canvas))
				} },
				new MenuItem { Header = "_Edit", Items = {
					undo,
					redo,
					clear_history,
					layer_undo,
					layer_redo,
					CreateMenuItem ("Clear layer", () => canvas.ClearActiveLayer (), keyboard, PintoraKeyCommands.ClearLayer),
					new Separator (),
					CreateMenuItem ("Copy selection", () => canvas.CopySelection (), keyboard, PintoraKeyCommands.Copy),
					CreateMenuItem ("Copy selection to clipboard", () => _ = CopySelectionToClipboardAsync (canvas), keyboard, PintoraKeyCommands.CopySelectionImage),
					CreateMenuItem ("Copy image to clipboard", () => _ = CopyImageToClipboardAsync (canvas), keyboard, PintoraKeyCommands.CopyImage),
					CreateMenuItem ("Paste selection", () => canvas.PasteSelection (), keyboard, PintoraKeyCommands.Paste),
					CreateMenuItem ("Paste image as layer", () => _ = canvas.PasteImageFromClipboardAsync (image_source_provider), keyboard, PintoraKeyCommands.PasteImageAsLayer),
					CreateMenuItem ("Paste image as annotation", () => _ = canvas.AddImageAnnotationFromClipboardAsync (image_source_provider), keyboard, PintoraKeyCommands.PasteImageAnnotation),
					CreateMenuItem ("Rotate selected annotation 90° clockwise", () => {
						if (canvas.SelectedAnnotation is { } annotation)
							canvas.RotateAnnotation (annotation, Math.PI / 2);
					}),
					CreateMenuItem ("Delete selected annotation", () => {
						if (canvas.SelectedAnnotation is { } annotation && canvas.RemoveAnnotation (annotation))
							canvas.ClearAnnotationSelection ();
					}),
					CreateMenuItem ("Duplicate selected annotation", () => canvas.DuplicateSelectedAnnotation (), keyboard, PintoraKeyCommands.DuplicateAnnotation),
					CreateMenuItem ("Select _all", () => canvas.SelectAll (), keyboard, PintoraKeyCommands.SelectAll),
					CreateMenuItem ("Deselect", canvas.ClearSelection, keyboard, PintoraKeyCommands.Deselect),
					CreateMenuItem ("Flip selection horizontally", () => canvas.FlipSelectionHorizontal ()),
					CreateMenuItem ("Flip selection vertically", () => canvas.FlipSelectionVertical ()),
					CreateMenuItem ("Rotate selection 90° clockwise", () => canvas.RotateSelectionClockwise ()),
					new Separator (),
					preferences
				} },
				new MenuItem { Header = "_View", Items = {
					CreateMenuItem ("Fit canvas", canvas.FitToWindow, keyboard, PintoraKeyCommands.FitCanvas),
					CreateMenuItem ("100%", canvas.SetActualSize, keyboard, PintoraKeyCommands.ActualSize),
					new Separator (),
					CreateMenuItem ("Light theme", () => App.SetTheme (global::Avalonia.Styling.ThemeVariant.Light)),
					CreateMenuItem ("Dark theme", () => App.SetTheme (global::Avalonia.Styling.ThemeVariant.Dark)),
					CreateMenuItem ("System theme", () => App.SetTheme (global::Avalonia.Styling.ThemeVariant.Default))
				} },
				new MenuItem { Header = "_Image", Items = {
					CreateMenuItem ("Resize image", () => new ResizeImageWindow (canvas).Show (this)),
					CreateMenuItem ("Crop image", () => new CropImageWindow (canvas).Show (this))
				} },
				new MenuItem { Header = "_Layers", Items = {
					CreateMenuItem ("Add layer", canvas.AddLayer),
					CreateMenuItem ("Duplicate layer", () => canvas.DuplicateActiveLayer (), keyboard, PintoraKeyCommands.DuplicateLayer),
					CreateMenuItem ("Move layer up", () => canvas.MoveLayer (1)),
					CreateMenuItem ("Move layer down", () => canvas.MoveLayer (-1)),
					CreateMenuItem ("Merge layer down", () => canvas.MergeActiveLayerDown ()),
					CreateMenuItem ("Delete layer", canvas.RemoveLayer),
					CreateMenuItem ("Rename layer", () => new LayerRenameWindow (canvas, canvas.Layers[canvas.ActiveLayerIndex].Name, () => { }).Show (this))
				} },
				new MenuItem { Header = "_Window", Items = {
					CreateMenuItem ("History", () => new HistoryWindow (canvas).Show (this))
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

	private void UpdateDocumentTitle (CanvasHost canvas, string version)
		=> Title = $"{(canvas.IsDirty ? "* " : string.Empty)}Pintora {version} — Simple. Powerful. Creative.";

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
		return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = { value, decrease, increase } };
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
			SuggestedFileName = "pintora.png",
			DefaultExtension = "png",
			FileTypeChoices = new[] {
				new FilePickerFileType ("PNG image") { Patterns = new[] { "*.png" } }
			}
		});

		if (file is not null)
			canvas.SavePng (file.Path.LocalPath);
	}

	private async Task SaveAsAsync (CanvasHost canvas)
	{
		if (StorageProvider is null)
			return;

		var file = await StorageProvider.SaveFilePickerAsync (new FilePickerSaveOptions {
			SuggestedFileName = "pintora.png",
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
			canvas.OpenImage (file[0].Path.LocalPath);
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
			SuggestedFileName = "pintora.ora",
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
			SuggestedFileName = "pintora.jpg",
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
			SuggestedFileName = "pintora.webp",
			DefaultExtension = "webp",
			FileTypeChoices = new[] { new FilePickerFileType ("WebP image") { Patterns = new[] { "*.webp" } } }
		});
		if (file is not null) canvas.SaveWebp (file.Path.LocalPath);
	}

	private async Task SaveTiffAsync (CanvasHost canvas)
	{
		if (StorageProvider is null) return;
		var file = await StorageProvider.SaveFilePickerAsync (new FilePickerSaveOptions {
			SuggestedFileName = "pintora.tiff",
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
				CreateToolPanel (canvas),
				CreatePalette (canvas),
				CreateLayersPanel (canvas)
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
		foreach ((Button button, CanvasHost.CanvasTool tool) in buttons) {
			button.Click += (_, _) => {
				canvas.ActiveTool = tool;
				UpdateToolButtons (buttons, canvas.ActiveTool);
				RefreshToolOptions (canvas);
			};
			tools.Children.Add (button);
		}
		canvas.ActiveToolChanged += (_, _) => {
			UpdateToolButtons (buttons, canvas.ActiveTool);
			RefreshToolOptions (canvas);
		};
		canvas.ToolOptionsChanged += (_, _) => RefreshToolOptions (canvas);
		canvas.BrushSizeChanged += (_, _) => RefreshToolOptions (canvas);
		canvas.SelectedShapeChanged += (_, _) => {
			if (canvas.ActiveTool == CanvasHost.CanvasTool.Shapes)
				RefreshToolOptions (canvas);
		};
		canvas.AnnotationSelectionChanged += (_, _) => {
			if (canvas.ActiveTool == CanvasHost.CanvasTool.Text)
				RefreshToolOptions (canvas);
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
			primary.Background = new SolidColorBrush (ToAvaloniaColor (canvas.BrushColor));
			secondary.Background = new SolidColorBrush (ToAvaloniaColor (canvas.SecondaryBrushColor));
			wheel.Color = ToAvaloniaColor (canvas.BrushColor);
		}
		canvas.ColorsChanged += (_, _) => RefreshColors ();
		void SaveColors ()
		{
			settings.PutSetting ("Pintora.Palette.Primary", ToAvaloniaColor (canvas.BrushColor).ToString ());
			settings.PutSetting ("Pintora.Palette.Secondary", ToAvaloniaColor (canvas.SecondaryBrushColor).ToString ());
			settings.PutSetting ("Pintora.Palette.Recent", string.Join (",", recent_colors.ConvertAll (ToAvaloniaColor).ConvertAll (color => color.ToString ())));
			settings.PutSetting ("Pintora.Palette.Colors", string.Join (",", palette_colors.ConvertAll (ToAvaloniaColor).ConvertAll (color => color.ToString ())));
			RefreshColors ();
		}
		primary.Click += (_, _) => new ColorDialogWindow (ToAvaloniaColor (canvas.BrushColor), color => {
			canvas.BrushColor = ColorBgra.FromBgra (color.B, color.G, color.R, color.A);
			SavePaletteColor (canvas);
			RefreshColors ();
		}).Show (this);
		secondary.Click += (_, _) => new ColorDialogWindow (ToAvaloniaColor (canvas.SecondaryBrushColor), color => {
			canvas.SecondaryBrushColor = ColorBgra.FromBgra (color.B, color.G, color.R, color.A);
			SavePaletteColor (canvas);
			RefreshColors ();
		}).Show (this);
		wheel.ColorChanged += (_, color) => {
			canvas.BrushColor = ColorBgra.FromBgra (color.B, color.G, color.R, color.A);
			SaveColors ();
		};
		var swap = CreateCompactCommand ("⇄", "Swap colors", () => {
			canvas.SwapColors ();
			SaveColors ();
		});
		var reset = CreateCompactCommand ("◩", "Reset colors", () => {
			canvas.ResetColors ();
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
				() => { canvas.BrushColor = color; SavePaletteColor (canvas); RefreshColors (); },
				() => { canvas.SecondaryBrushColor = color; SavePaletteColor (canvas); RefreshColors (); },
				() => new ColorDialogWindow (ToAvaloniaColor (color), picked => {
					palette_colors[index] = ColorBgra.FromBgra (picked.B, picked.G, picked.R, picked.A);
					canvas.BrushColor = palette_colors[index];
					SavePaletteColor (canvas);
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
				() => { canvas.BrushColor = color; SavePaletteColor (canvas); },
				() => { canvas.SecondaryBrushColor = color; SavePaletteColor (canvas); });
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
				canvas.BrushColor = ToColorBgra (parsed);
				SavePaletteColor (canvas);
				RefreshColors ();
			}
		};
		var more = CreateCompactCommand ("More…", "Show custom color controls", () => {
			advancedColors.IsVisible = !advancedColors.IsVisible;
			if (advancedColors.IsVisible)
				hexInput.Text = ToAvaloniaColor (canvas.BrushColor).ToString ();
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
				tool_options.Children.Add (new TextBlock { Text = "Shape Type:", VerticalAlignment = VerticalAlignment.Center });
				var shape = new ComboBox {
					ItemsSource = new[] { "Line", "Rectangle", "Ellipse", "Rounded rectangle" },
					SelectedIndex = (int) canvas.SelectedShape,
					MinWidth = 135
				};
				tool_options.Children.Add (shape);
				tool_options.Children.Add (new TextBlock { Text = "Fill Style:", VerticalAlignment = VerticalAlignment.Center });
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
				tool_options.Children.Add (new TextBlock { Text = "Outline width:", VerticalAlignment = VerticalAlignment.Center });
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
				var smoothing = new ComboBox {
					ItemsSource = new[] { "Smooth", "Pixelated" },
					SelectedIndex = canvas.ShapeAntialias ? 0 : 1,
					MinWidth = 105
				};
				smoothing.SelectionChanged += (_, _) => canvas.ShapeAntialias = smoothing.SelectedIndex == 0;
				tool_options.Children.Add (smoothing);
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

	private static Control CreateCanvasPlaceholder (CanvasHost canvas)
	{
		var canvas_panel = new Border {
			Margin = new global::Avalonia.Thickness (96, 10, 252, 190),
			Child = canvas
		};
		return canvas_panel;
	}

	private static CanvasHost CreateCanvasHost (PintoraKeyboardSettings keyboard)
	{
		MemoryImageSurface surface = new (800, 600);
		surface.Fill (ColorBgra.White);

		return new CanvasHost (new Pinta.Core.Size (surface.Width, surface.Height), surface, keyboard);
	}

	private Control CreateLayersPanel (CanvasHost canvas)
	{
		thumbnail_provider ??= new LayerThumbnailProvider (canvas);
		LayerThumbnailProvider provider = thumbnail_provider;
		var layers = new StackPanel { Spacing = 3 };
		// Maps each layer to the Image control currently displaying it. Rebuilt
		// on every refresh; the single provider subscription below looks up the
		// live control instead of capturing per-row handlers.
		var thumbnails = new Dictionary<AvaloniaLayer, Image> ();
		Slider? opacity_slider = null;
		void RebuildLayers ()
		{
			int? drag_source = null;
			layers.Children.Clear ();
			thumbnails.Clear ();
			for (int i = canvas.Layers.Count - 1; i >= 0; i--) {
				int index = i;
				AvaloniaLayer layer = canvas.Layers[index];
				var visibility = new CheckBox { IsChecked = layer.IsVisible, Content = layer.Name };
				var locked = new CheckBox { IsChecked = layer.IsLocked, Content = "Lock" };
				visibility.IsCheckedChanged += (_, _) => {
					canvas.SetLayerVisibility (index, visibility.IsChecked == true);
				};
				locked.IsCheckedChanged += (_, _) => canvas.SetLayerLocked (index, locked.IsChecked == true);
				var thumbnail = new Image { Source = provider.GetThumbnail (layer), Width = 42, Height = 28, Stretch = Stretch.Uniform };
				thumbnails[layer] = thumbnail;
				var button = new Button {
					Content = new StackPanel {
						Orientation = Orientation.Horizontal,
						Spacing = 4,
						Children = {
						new TextBlock { Text = index == canvas.ActiveLayerIndex ? "▣" : "□" },
						thumbnail,
						visibility, locked
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
						canvas.MoveLayerTo (source, index);
					drag_source = null;
					RebuildLayers ();
				};
				button.Click += (_, _) => canvas.SelectLayer (index);
				button.ContextMenu = new ContextMenu {
					Items = {
						CreateMenuItem ("Rename", () => new LayerRenameWindow (canvas, canvas.Layers[index].Name, RebuildLayers).Show (this)),
						CreateMenuItem ("Duplicate", () => { canvas.SelectLayer (index); canvas.DuplicateActiveLayer (); RebuildLayers (); }),
						CreateMenuItem ("Delete", () => { canvas.SelectLayer (index); canvas.RemoveLayer (); RebuildLayers (); }),
						new Separator (),
						CreateMenuItem ("Move up", () => { canvas.SelectLayer (index); canvas.MoveLayer (1); RebuildLayers (); }),
						CreateMenuItem ("Move down", () => { canvas.SelectLayer (index); canvas.MoveLayer (-1); RebuildLayers (); }),
						CreateMenuItem ("Merge down", () => { canvas.SelectLayer (index); canvas.MergeActiveLayerDown (); RebuildLayers (); })
					}
				};
				layers.Children.Add (button);
			}
			if (opacity_slider is not null && canvas.Layers.Count > 0)
				opacity_slider.Value = canvas.Layers[canvas.ActiveLayerIndex].Opacity;
		}
		opacity_slider = CreateOpacitySlider (canvas);
		var controls = new StackPanel {
			Spacing = 5,
			Children = {
			new TextBlock { Text = "Layers", FontSize = 12 },
			new ScrollViewer { MaxHeight = 280, Content = layers },
			new TextBlock { Text = "Opacity", FontSize = 11 },
			opacity_slider,
			new WrapPanel { Width = 216, ItemWidth = 43, ItemHeight = 34, Children = {
				CreateCompactCommand ("+", "Add layer", () => { canvas.AddLayer (); RebuildLayers (); }),
				CreateCompactCommand ("−", "Delete layer", () => { canvas.RemoveLayer (); RebuildLayers (); }),
				CreateCompactCommand ("⧉", "Duplicate layer", () => { canvas.DuplicateActiveLayer (); RebuildLayers (); }),
				CreateCompactCommand ("↑", "Move layer up", () => { canvas.MoveLayer (1); RebuildLayers (); }),
				CreateCompactCommand ("↓", "Move layer down", () => { canvas.MoveLayer (-1); RebuildLayers (); }),
				CreateCompactCommand ("Merge", "Merge layer down", () => { canvas.MergeActiveLayerDown (); RebuildLayers (); }),
				CreateCompactCommand ("Name", "Rename layer", () => new LayerRenameWindow (canvas, canvas.Layers[canvas.ActiveLayerIndex].Name, RebuildLayers).Show (this)),
				CreateCompactCommand ("↶", "Undo layer operation", () => { canvas.UndoLayerOperation (); RebuildLayers (); }),
				CreateCompactCommand ("↷", "Redo layer operation", () => { canvas.RedoLayerOperation (); RebuildLayers (); })
			} }
		}
		};
		RebuildLayers ();
		refresh_layers = RebuildLayers;
		// Single subscription for the lifetime of the panel. Rebuilds only
		// replace the dictionary entries, so no handler can outlive its row.
		thumbnail_updated_handler = (_, e) => {
			if (e.Thumbnail is not null && thumbnails.TryGetValue (e.Layer, out Image? image))
				image.Source = e.Thumbnail;
		};
		provider.ThumbnailUpdated += thumbnail_updated_handler;
		canvas.LayersChanged += (_, _) => RebuildLayers ();
		canvas.ActiveLayerChanged += (_, _) => RebuildLayers ();
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

	private static Slider CreateOpacitySlider (CanvasHost canvas)
	{
		var slider = new Slider { Minimum = 0, Maximum = 1, Value = canvas.Layers.Count == 0 ? 1 : canvas.Layers[canvas.ActiveLayerIndex].Opacity };
		slider.ValueChanged += (_, args) => {
			if (canvas.Layers.Count == 0)
				return;
			canvas.SetActiveLayerOpacity (args.NewValue);
		};
		return slider;
	}

}
