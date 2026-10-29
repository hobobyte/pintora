using Pinta.Core;

namespace Pinta;

internal sealed class GtkToolUiService : IToolUiService
{
	private readonly ChromeManager chrome;
	private Gtk.Box tool_bar = null!;
	private Gtk.Label? tool_label;
	private Gtk.Image? tool_image;
	private Gtk.Separator? tool_separator;
	private Gtk.Box? options_box;
	private Gtk.ScrolledWindow? options_scroll;
	private Gtk.Label? selection_label;
	private ToolBarComboBox? selection_combo_box;

	public GtkToolUiService (ChromeManager chrome) => this.chrome = chrome;
	public void InitializeToolBar (object toolbar) => tool_bar = (Gtk.Box) toolbar;

	public void ActivateTool (BaseTool tool)
	{
		ToolImage.SetFromIconName (tool.Icon);
		tool_bar.Append (ToolLabel);
		tool_bar.Append (ToolImage);
		tool_bar.Append (ToolSeparator);
		tool_bar.Append (OptionsScroll);
		tool.DoBuildToolBar (new GtkToolbarSurface (OptionsBox));
	}

	public void DeactivateTool (BaseTool tool, BaseTool? nextTool)
	{
		OptionsBox.RemoveAll ();
		tool_bar.RemoveAll ();
		tool.DoDeactivated (PintaCore.Workspace.ActiveDocumentOrDefault, nextTool);
	}

	public void BuildSelectionModeToolbar (object toolbar, SelectionModeHandler handler, ISettingsService settings)
	{
		Gtk.Box box = (Gtk.Box) toolbar;
		selection_label ??= Gtk.Label.New (Translations.GetString (" Selection Mode: "));
		box.Append (selection_label);

		if (selection_combo_box is null) {
			selection_combo_box = ToolBarComboBox.New (170, 0, false);
			selection_combo_box.ComboBox.OnChanged += (_, _) => {
				string? text = selection_combo_box.ComboBox.GetActiveText ();
				if (text is not null && handler.CombineModes.TryGetValue (text, out CombineMode mode))
					handler.SetSelectedMode (mode, (int) selection_combo_box.ComboBox.Active);
			};
			foreach (string mode in handler.CombineModes.Keys)
				selection_combo_box.ComboBox.AppendText (mode);
			selection_combo_box.ComboBox.Active = settings.GetSetting (Pinta.Core.SettingNames.SELECTION_COMBINE_MODE, 0);
		}

		box.Append (selection_combo_box);
	}

	private Gtk.Label ToolLabel => tool_label ??= Gtk.Label.New ($" {Translations.GetString ("Tool")}:  ");
	private Gtk.Image ToolImage => tool_image ??= Gtk.Image.New ();
	private Gtk.Separator ToolSeparator => tool_separator ??= GtkExtensions.CreateToolBarSeparator ();
	private Gtk.Box OptionsBox => options_box ??= Gtk.Box.New (Gtk.Orientation.Horizontal, 0);

	private Gtk.ScrolledWindow OptionsScroll {
		get {
			if (options_scroll is null) {
				options_scroll = Gtk.ScrolledWindow.New ();
				options_scroll.Child = OptionsBox;
				options_scroll.HscrollbarPolicy = Gtk.PolicyType.Automatic;
				options_scroll.VscrollbarPolicy = Gtk.PolicyType.Never;
				options_scroll.HasFrame = false;
				options_scroll.OverlayScrolling = true;
				options_scroll.WindowPlacement = Gtk.CornerType.BottomRight;
				options_scroll.Hexpand = true;
				options_scroll.Halign = Gtk.Align.Fill;
			}
			return options_scroll;
		}
	}
}

internal sealed class GtkToolbarSurface : IToolbarSurface
{
	private readonly Gtk.Box box;
	public GtkToolbarSurface (Gtk.Box box) => this.box = box;
	public object NativeToolbar => box;
	public void Append (object widget) => box.Append ((Gtk.Widget) widget);

	public void InsertAfter (object widget, object afterWidget)
		=> box.InsertChildAfter ((Gtk.Widget) widget, (Gtk.Widget) afterWidget);

	public void Remove (object widget) => box.Remove ((Gtk.Widget) widget);

	public void RemoveAll () => box.RemoveAll ();
}
