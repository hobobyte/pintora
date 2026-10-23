using System;
using Avalonia.Controls;
using Avalonia.Layout;
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

	public MainWindow ()
	{
		Title = "Pintora";
		Width = 1200;
		Height = 760;
		MinWidth = 900;
		MinHeight = 600;

		settings = new SettingsManager ();
		keyboard = new PintoraKeyboardSettings (settings);
		Closed += (_, _) => settings.DoSaveSettingsBeforeQuit ();
		CanvasHost canvas = CreateCanvasHost (keyboard);
		Content = new Grid {
			RowDefinitions = new RowDefinitions ("Auto,*"),
			Children = {
				CreateToolbar (canvas),
				CreateWorkspace (canvas)
			}
		};
	}

	private Control CreateToolbar (CanvasHost canvas)
	{
		var toolbar = new Border {
			Background = new SolidColorBrush (Color.Parse ("#F3F0EA")),
			Padding = new global::Avalonia.Thickness (16, 10),
			Child = new StackPanel {
				Orientation = Orientation.Horizontal,
				Spacing = 8,
				Children = {
					new TextBlock { Text = "Pintora", FontSize = 14, VerticalAlignment = VerticalAlignment.Center },
					CreateBrushButton ("New", canvas.NewDocument),
					CreateBrushButton ("Open PNG", () => _ = OpenPngAsync (canvas)),
					CreateBrushButton ("Save PNG", () => _ = SavePngAsync (canvas)),
					CreateBrushButton ("Fit", canvas.FitToWindow),
					CreateBrushButton ("Black", () => canvas.BrushColor = ColorBgra.Black),
					CreateBrushButton ("Red", () => canvas.BrushColor = ColorBgra.Red),
					CreateBrushButton ("Blue", () => canvas.BrushColor = ColorBgra.Blue),
					CreateBrushButton ("Brush", () => canvas.ActiveTool = CanvasHost.CanvasTool.Brush),
					CreateBrushButton ("Eraser", () => canvas.ActiveTool = CanvasHost.CanvasTool.Eraser),
					CreateBrushButton ("Size 1", () => canvas.BrushSize = 1),
					CreateBrushButton ("Size 3", () => canvas.BrushSize = 3),
					CreateBrushButton ("Size 8", () => canvas.BrushSize = 8),
					CreateBrushButton ("Undo", canvas.UndoLastStroke),
					CreateBrushButton ("Redo", canvas.RedoLastStroke),
					CreateBrushButton ("Keyboard", () => new KeyboardSettingsWindow (keyboard).Show (this))
				}
			}
		};
		Grid.SetRow (toolbar, 0);
		return toolbar;
	}

	private static Button CreateBrushButton (string label, Action action)
	{
		var button = new Button { Content = label };
		button.Click += (_, _) => action ();
		return button;
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

	private static Control CreateWorkspace (CanvasHost canvas)
	{
		var workspace = new Grid {
			ColumnDefinitions = new ColumnDefinitions ("220,*,280"),
			Children = {
				CreateToolPanel (),
				CreateCanvasPlaceholder (canvas),
				CreateLayersPanel (canvas)
			}
		};
		Grid.SetRow (workspace, 1);
		return workspace;
	}

	private static Control CreateToolPanel ()
	{
		var panel = new Border {
			Background = new SolidColorBrush (Color.Parse ("#F3F0EA")),
			Padding = new global::Avalonia.Thickness (16),
			Child = new TextBlock { Text = "Tools\n\nPointer\nBrush\nEraser\nShapes\nText", FontSize = 15 }
		};
		Grid.SetColumn (panel, 0);
		return panel;
	}

	private static Control CreateCanvasPlaceholder (CanvasHost canvas)
	{
		var canvas_panel = new Border {
			Background = new SolidColorBrush (Color.Parse ("#D7DCE3")),
			Margin = new global::Avalonia.Thickness (16),
			Child = canvas
		};
		Grid.SetColumn (canvas_panel, 1);
		return canvas_panel;
	}

	private static CanvasHost CreateCanvasHost (PintoraKeyboardSettings keyboard)
	{
		MemoryImageSurface surface = new (800, 600);
		Span<ColorBgra> pixels = surface.GetPixelData ();
		for (int y = 0; y < surface.Height; y++)
			for (int x = 0; x < surface.Width; x++)
				pixels[y * surface.Width + x] = ColorBgra.FromBgra ((byte) (x / 4), (byte) (y / 3), 180, 255);

		return new CanvasHost (new Pinta.Core.Size (surface.Width, surface.Height), surface, keyboard);
	}

	private static Control CreateLayersPanel (CanvasHost canvas)
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
				CreateBrushButton ("+", () => { canvas.AddLayer (); RebuildLayers (); }),
				CreateBrushButton ("−", () => { canvas.RemoveLayer (); RebuildLayers (); })
			} }
		} };
		RebuildLayers ();
		var panel = new Border {
			Background = new SolidColorBrush (Color.Parse ("#F3F0EA")),
			Padding = new global::Avalonia.Thickness (16),
			Child = controls
		};
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
