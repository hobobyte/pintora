using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using System.Threading.Tasks;
using Pintora.Avalonia.Canvas;
using Pintora.Avalonia.Imaging;
using Pintora.Avalonia.Input;
using Pinta.Core;

namespace Pintora.Avalonia;

public sealed class MainWindow : Window
{
	private readonly PintoraKeyboardSettings keyboard;
	private readonly SettingsManager settings;
	private Action? refresh_layers;
	private StackPanel? tool_options;

	public MainWindow ()
	{
		Title = "Pintora — Simple. Powerful. Creative.";
		Width = 1200;
		Height = 760;
		MinWidth = 900;
		MinHeight = 600;

		settings = new SettingsManager ();
		App.InitializeTheme (settings);
		keyboard = new PintoraKeyboardSettings (settings);
		Closed += (_, _) => settings.DoSaveSettingsBeforeQuit ();
		CanvasHost canvas = CreateCanvasHost (keyboard);
		Content = new Grid {
			RowDefinitions = new RowDefinitions ("Auto,Auto,*,Auto"),
			Children = {
				CreateMainMenu (canvas),
				CreateToolbar (canvas),
				CreateWorkspace (canvas),
				CreateStatusBar (canvas)
			}
		};
	}

	private Control CreateToolbar (CanvasHost canvas)
	{
		tool_options = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
		var toolbar = new Border {
			Padding = new global::Avalonia.Thickness (16, 10),
			Child = new StackPanel {
				Orientation = Orientation.Horizontal,
				Spacing = 8,
				Children = {
					new Separator (),
					tool_options
				}
			}
		};
		RefreshToolOptions (canvas);
		toolbar.Bind (Border.BackgroundProperty, new DynamicResourceExtension ("Pintora.Toolbar.Background"));
		Grid.SetRow (toolbar, 0);
		Grid.SetRow (toolbar, 1);
		return toolbar;
	}

	private static Button CreateColorSwatch (string color, Action action)
	{
		var button = new Button {
			Content = "",
			Width = 28,
			Height = 28,
			Background = new SolidColorBrush (Color.Parse (color)),
			Padding = new global::Avalonia.Thickness (0)
		};
		button.Click += (_, _) => action ();
		return button;
	}

	private static Control CreateStatusBar (CanvasHost canvas)
	{
		var dimensions = new TextBlock { Text = $"{canvas.ImageSize.Width} × {canvas.ImageSize.Height} px" };
		DockPanel.SetDock (dimensions, Dock.Right);
		var status = new Border {
			BorderBrush = new SolidColorBrush (Color.Parse ("#D8DEE8")),
			BorderThickness = new global::Avalonia.Thickness (0, 1, 0, 0),
			Padding = new global::Avalonia.Thickness (12, 6),
			Child = new DockPanel {
				Children = {
					dimensions,
					new TextBlock { Text = $"{canvas.Scale:P0}  •  {canvas.ActiveTool}  •  Brush {canvas.BrushSize}px" }
				}
			}
		};
		status.Bind (Border.BackgroundProperty, new DynamicResourceExtension ("Pintora.Status.Background"));
		Grid.SetRow (status, 3);
		return status;
	}

	private Menu CreateMainMenu (CanvasHost canvas)
	{
		var preferences = new MenuItem { Header = "Preferences" };
		preferences.Click += (_, _) => new KeyboardSettingsWindow (keyboard).Show (this);
		var menu = new Menu {
			Items = {
				new MenuItem { Header = "_File", Items = {
					CreateMenuItem ("_New", canvas.NewDocument),
					CreateMenuItem ("Open _PNG", () => _ = OpenPngAsync (canvas)),
					new Separator (),
					CreateMenuItem ("Save _PNG", () => _ = SavePngAsync (canvas)),
					CreateMenuItem ("Save _JPEG", () => _ = SaveJpegAsync (canvas)),
					CreateMenuItem ("Save _ORA", () => _ = SaveOraAsync (canvas))
				} },
				new MenuItem { Header = "_Edit", Items = {
					CreateMenuItem ("_Undo", canvas.UndoLastStroke),
					CreateMenuItem ("_Redo", canvas.RedoLastStroke),
					new Separator (),
					preferences
				} },
				new MenuItem { Header = "_View", Items = {
					CreateMenuItem ("Fit canvas", canvas.FitToWindow),
					CreateMenuItem ("100%", () => canvas.SetScale (1)),
					new Separator (),
					CreateMenuItem ("Light theme", () => App.SetTheme (global::Avalonia.Styling.ThemeVariant.Light)),
					CreateMenuItem ("Dark theme", () => App.SetTheme (global::Avalonia.Styling.ThemeVariant.Dark)),
					CreateMenuItem ("System theme", () => App.SetTheme (global::Avalonia.Styling.ThemeVariant.Default)),
					new MenuItem { Header = "Show layers" }
				} },
				new MenuItem { Header = "_Image", Items = {
					CreateMenuItem ("Add layer", () => { canvas.AddLayer (); refresh_layers?.Invoke (); }),
					CreateMenuItem ("Delete layer", () => { canvas.RemoveLayer (); refresh_layers?.Invoke (); }),
					CreateMenuItem ("Rename layer", () => new LayerRenameWindow (canvas, canvas.Layers[canvas.ActiveLayerIndex].Name, () => refresh_layers?.Invoke ()).Show (this)),
					new MenuItem { Header = "Resize image" }
				} },
				new MenuItem { Header = "_Adjustments", Items = {
					new MenuItem { Header = "Brightness / Contrast" },
					new MenuItem { Header = "Hue / Saturation" },
					new MenuItem { Header = "Levels" }
				} },
				new MenuItem { Header = "Effe_cts", Items = {
					new MenuItem { Header = "Blur" },
					new MenuItem { Header = "Sharpen" }
				} },
				new MenuItem { Header = "A_dd-ins" },
				new MenuItem { Header = "_Window", Items = {
					new MenuItem { Header = "Layers" },
					new MenuItem { Header = "Tools" }
				} },
				new MenuItem { Header = "_Help", Items = {
					CreateMenuItem ("Keyboard shortcuts", () => new KeyboardSettingsWindow (keyboard).Show (this)),
					new MenuItem { Header = "About Pintora" }
				} }
			}
		};
		Grid.SetRow (menu, 0);
		return menu;
	}

	private static Button CreateBrushButton (string label, Action action)
	{
		var button = new Button { Content = label };
		button.Click += (_, _) => action ();
		return button;
	}

	private static NumericUpDown CreateBrushSizeInput (CanvasHost canvas)
	{
		var input = new NumericUpDown { Minimum = 1, Maximum = 1000, Value = canvas.BrushSize, Width = 70 };
		input.ValueChanged += (_, args) => canvas.BrushSize = (int) (args.NewValue ?? 1);
		return input;
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

	private async Task OpenPngAsync (CanvasHost canvas)
	{
		if (StorageProvider is null)
			return;

		var file = await StorageProvider.OpenFilePickerAsync (new FilePickerOpenOptions {
			AllowMultiple = false,
			FileTypeFilter = new[] {
				new FilePickerFileType ("PNG image") { Patterns = new[] { "*.png" } }
			}
		});

		if (file.Count > 0)
			canvas.OpenPng (file[0].Path.LocalPath);
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
			ColumnDefinitions = new ColumnDefinitions ("220,*,280"),
			Children = {
				CreateToolPanel (canvas),
				CreateCanvasPlaceholder (canvas),
				CreateLayersPanel (canvas)
			}
		};
		Grid.SetRow (workspace, 2);
		return workspace;
	}

	private Control CreateToolPanel (CanvasHost canvas)
	{
		var tools = new StackPanel { Spacing = 6 };
		var buttons = new List<(Button Button, CanvasHost.CanvasTool Tool)> ();
		Button brush = CreateToolButton (canvas, "Brush", CanvasHost.CanvasTool.Brush,
			"M7 14c-1.66 0-3 1.34-3 3 0 1.31-1.16 2-2 2 .92 1.22 2.49 2 4 2 2.21 0 4-1.79 4-4 0-1.66-1.34-3-3-3zm13.71-9.37l-1.34-1.34c-.39-.39-1.02-.39-1.41 0L9 12.25 11.75 15l8.96-8.96c.39-.39.39-1.02 0-1.41z");
		Button pencil = CreateToolButton (canvas, "Pencil", CanvasHost.CanvasTool.Pencil,
			"M4 17.25V21h3.75L18.81 9.94l-3.75-3.75L4 17.25zM20.71 8.04c.39-.39.39-1.02 0-1.41l-2.34-2.34a.9959.9959 0 0 0-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z");
		Button line = CreateToolButton (canvas, "Line", CanvasHost.CanvasTool.Line,
			"M4 19.5l16-15L21.5 6l-16 15L4 19.5z");
		Button rectangle = CreateToolButton (canvas, "Rectangle", CanvasHost.CanvasTool.Rectangle,
			"M4 4h16v16H4V4zm2 2v12h12V6H6z");
		Button ellipse = CreateToolButton (canvas, "Ellipse", CanvasHost.CanvasTool.Ellipse,
			"M12 4a8 8 0 1 0 0 16 8 8 0 1 0 0-16z");
		Button fill = CreateToolButton (canvas, "Fill", CanvasHost.CanvasTool.Fill,
			"M5 3h10v2H5V3zm0 4h10v2H5V7zm0 4h10v2H5v-2zm12-8h2v18h-2V3z");
		Button picker = CreateToolButton (canvas, "Color Picker", CanvasHost.CanvasTool.ColorPicker,
			"M19.35 10.04L14 4.69 15.41 3.28 20.76 8.63 19.35 10.04zM3 17.25V21h3.75l10.81-10.81-3.75-3.75L3 17.25z");
		Button zoom = CreateToolButton (canvas, "Zoom", CanvasHost.CanvasTool.Zoom,
			"M9.5 3a6.5 6.5 0 1 0 4.06 11.58L19 20l1-1-5.44-5.42A6.5 6.5 0 0 0 9.5 3zm0 2a4.5 4.5 0 1 1 0 9 4.5 4.5 0 0 1 0-9z");
		Button eraser = CreateToolButton (canvas, "Eraser", CanvasHost.CanvasTool.Eraser,
			"M15.8698 2.66878L20.8384 7.6373C21.717 8.51598 21.717 9.9406 20.8384 10.8193L12.1566 19.4998L18.2544 19.5C18.6341 19.5 18.9479 19.7821 18.9976 20.1482L19.0044 20.25C19.0044 20.6297 18.7223 20.9435 18.3562 20.9931L18.2544 21L9.84443 21.0012C9.22822 21.0348 8.60082 20.8163 8.1301 20.3456L3.16157 15.377C2.28289 14.4984 2.28289 13.0737 3.16157 12.1951L12.6879 2.66878C13.5665 1.7901 14.9912 1.7901 15.8698 2.66878Z");
		buttons.Add ((brush, CanvasHost.CanvasTool.Brush));
		buttons.Add ((pencil, CanvasHost.CanvasTool.Pencil));
		buttons.Add ((line, CanvasHost.CanvasTool.Line));
		buttons.Add ((rectangle, CanvasHost.CanvasTool.Rectangle));
		buttons.Add ((ellipse, CanvasHost.CanvasTool.Ellipse));
		buttons.Add ((fill, CanvasHost.CanvasTool.Fill));
		buttons.Add ((picker, CanvasHost.CanvasTool.ColorPicker));
		buttons.Add ((zoom, CanvasHost.CanvasTool.Zoom));
		buttons.Add ((eraser, CanvasHost.CanvasTool.Eraser));
		foreach ((Button button, CanvasHost.CanvasTool tool) in buttons) {
			button.Click += (_, _) => {
				canvas.ActiveTool = tool;
				UpdateToolButtons (buttons, canvas.ActiveTool);
				RefreshToolOptions (canvas);
			};
			tools.Children.Add (button);
		}
		var panel = new Border {
			Padding = new global::Avalonia.Thickness (16),
			Child = new StackPanel { Spacing = 8, Children = {
				new TextBlock { Text = "Tools", FontSize = 15 },
				tools,
				CreatePalette (canvas),
				new Separator ()
			} }
		};
		panel.Bind (Border.BackgroundProperty, new DynamicResourceExtension ("Pintora.Panel.Background"));
		Grid.SetColumn (panel, 0);
		return panel;
	}

	private static Control CreatePalette (CanvasHost canvas)
	{
		var primary = new Button { Content = "FG", Width = 42, Height = 32, Background = new SolidColorBrush (Colors.Black) };
		var secondary = new Button { Content = "BG", Width = 42, Height = 32, Background = new SolidColorBrush (Colors.White) };
		primary.Click += (_, _) => canvas.BrushColor = canvas.BrushColor;
		secondary.Click += (_, _) => canvas.BrushColor = canvas.SecondaryBrushColor;
		var swap = CreateBrushButton ("⇄", () => {
			(canvas.BrushColor, canvas.SecondaryBrushColor) = (canvas.SecondaryBrushColor, canvas.BrushColor);
		});
		var colors = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
		foreach ((string Hex, ColorBgra Color) color in new[] {
			("#000000", ColorBgra.Black),
			("#FFFFFF", ColorBgra.White),
			("#E95B64", ColorBgra.FromBgra (100, 91, 233, 255)),
			("#3B8CFF", ColorBgra.FromBgra (255, 140, 59, 255)),
			("#72C991", ColorBgra.FromBgra (145, 201, 114, 255)),
			("#F0B75E", ColorBgra.FromBgra (94, 183, 240, 255))
		})
			colors.Children.Add (CreateColorSwatch (color.Hex, () => canvas.BrushColor = color.Color));
		var picker = new Button { Content = "Color Picker" };
		picker.Click += (_, _) => canvas.ActiveTool = CanvasHost.CanvasTool.ColorPicker;
		return new StackPanel {
			Spacing = 5,
			Children = {
				new TextBlock { Text = "Colors", FontSize = 15 },
				new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Children = { primary, secondary, swap } },
				colors,
				picker
			}
		};
	}

	private void RefreshToolOptions (CanvasHost canvas)
	{
		if (tool_options is null)
			return;
		tool_options.Children.Clear ();
		switch (canvas.ActiveTool) {
			case CanvasHost.CanvasTool.Brush:
				tool_options.Children.Add (new TextBlock { Text = "Size", VerticalAlignment = VerticalAlignment.Center });
				tool_options.Children.Add (CreateBrushSizeInput (canvas));
				tool_options.Children.Add (new TextBlock { Text = "Hardness", VerticalAlignment = VerticalAlignment.Center });
				tool_options.Children.Add (CreateBrushSlider (0, 1, canvas.BrushHardness, value => canvas.BrushHardness = value, 90));
				tool_options.Children.Add (new TextBlock { Text = "Opacity", VerticalAlignment = VerticalAlignment.Center });
				tool_options.Children.Add (CreateBrushSlider (0, 1, canvas.BrushOpacity, value => canvas.BrushOpacity = value, 90));
				break;
			case CanvasHost.CanvasTool.Pencil:
				break;
			case CanvasHost.CanvasTool.Line:
				tool_options.Children.Add (new TextBlock { Text = "Width", VerticalAlignment = VerticalAlignment.Center });
				tool_options.Children.Add (CreateBrushSizeInput (canvas));
				break;
			case CanvasHost.CanvasTool.Rectangle:
			case CanvasHost.CanvasTool.Ellipse:
				tool_options.Children.Add (new TextBlock { Text = "Width", VerticalAlignment = VerticalAlignment.Center });
				tool_options.Children.Add (CreateBrushSizeInput (canvas));
				var fill = new CheckBox { Content = "Fill", IsChecked = canvas.ShapeFilled };
				fill.IsCheckedChanged += (_, _) => canvas.ShapeFilled = fill.IsChecked == true;
				tool_options.Children.Add (fill);
				break;
			case CanvasHost.CanvasTool.Fill:
				tool_options.Children.Add (new TextBlock { Text = "Tolerance", VerticalAlignment = VerticalAlignment.Center });
				tool_options.Children.Add (CreateBrushSlider (0, 255, canvas.FillTolerance, value => canvas.FillTolerance = (int) value, 90));
				break;
			case CanvasHost.CanvasTool.Eraser:
				tool_options.Children.Add (new TextBlock { Text = "Size", VerticalAlignment = VerticalAlignment.Center });
				tool_options.Children.Add (CreateBrushSizeInput (canvas));
				break;
		}
	}

	private static Button CreateToolButton (CanvasHost canvas, string label, CanvasHost.CanvasTool tool, string path)
	{
		var button = new Button {
			Content = new StackPanel {
				Spacing = 2,
				HorizontalAlignment = HorizontalAlignment.Center,
				Children = {
					new PathIcon { Data = StreamGeometry.Parse (path), Width = 20, Height = 20 },
					new TextBlock { Text = label, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center }
				}
			},
			Width = 92,
			Height = 52,
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
			Margin = new global::Avalonia.Thickness (16),
			Child = canvas
		};
		canvas_panel.Bind (Border.BackgroundProperty, new DynamicResourceExtension ("Pintora.Canvas.Background"));
		Grid.SetColumn (canvas_panel, 1);
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
		var layers = new StackPanel { Spacing = 6 };
		void RebuildLayers ()
		{
			layers.Children.Clear ();
			for (int i = canvas.Layers.Count - 1; i >= 0; i--) {
				int index = i;
				var visibility = new CheckBox { IsChecked = canvas.Layers[index].IsVisible, Content = canvas.Layers[index].Name };
				visibility.IsCheckedChanged += (_, _) => {
					canvas.Layers[index].IsVisible = visibility.IsChecked == true;
					canvas.InvalidateVisual ();
				};
				var button = new Button {
					Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
						new TextBlock { Text = index == canvas.ActiveLayerIndex ? "●" : "○" }, visibility
					} },
					HorizontalContentAlignment = HorizontalAlignment.Left
				};
				button.Click += (_, _) => { canvas.SelectLayer (index); RebuildLayers (); };
				layers.Children.Add (button);
			}
		}
		var controls = new StackPanel { Spacing = 8, Children = {
			new TextBlock { Text = "Layers", FontSize = 15 },
			layers,
			new TextBlock { Text = "Opacity" },
			CreateOpacitySlider (canvas),
			new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
				CreateBrushButton ("↑", () => { canvas.MoveLayer (1); RebuildLayers (); }),
				CreateBrushButton ("↓", () => { canvas.MoveLayer (-1); RebuildLayers (); }),
				CreateBrushButton ("Rename", () => new LayerRenameWindow (canvas, canvas.Layers[canvas.ActiveLayerIndex].Name, RebuildLayers).Show (this)),
				CreateBrushButton ("+", () => { canvas.AddLayer (); RebuildLayers (); }),
				CreateBrushButton ("−", () => { canvas.RemoveLayer (); RebuildLayers (); })
			} }
		} };
		RebuildLayers ();
		refresh_layers = RebuildLayers;
		var panel = new Border {
			Padding = new global::Avalonia.Thickness (16),
			Child = controls
		};
		panel.Bind (Border.BackgroundProperty, new DynamicResourceExtension ("Pintora.Panel.Background"));
		Grid.SetColumn (panel, 2);
		return panel;
	}

	private static Slider CreateOpacitySlider (CanvasHost canvas)
	{
		var slider = new Slider { Minimum = 0, Maximum = 1, Value = canvas.Layers.Count == 0 ? 1 : canvas.Layers[canvas.ActiveLayerIndex].Opacity };
		slider.ValueChanged += (_, args) => {
			if (canvas.Layers.Count == 0)
				return;
			canvas.Layers[canvas.ActiveLayerIndex].Opacity = args.NewValue;
			canvas.InvalidateVisual ();
		};
		return slider;
	}
}
