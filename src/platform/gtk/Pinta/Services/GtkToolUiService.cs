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

	public GtkToolUiService (ChromeManager chrome) => this.chrome = chrome;
	public void InitializeToolBar (object toolbar) => tool_bar = (Gtk.Box) toolbar;

	public void ActivateTool (BaseTool tool)
	{
		ToolImage.SetFromIconName (tool.Icon);
		tool_bar.Append (ToolLabel);
		tool_bar.Append (ToolImage);
		tool_bar.Append (ToolSeparator);
		tool_bar.Append (OptionsScroll);
		tool.DoBuildToolBar (OptionsBox);
	}

	public void DeactivateTool (BaseTool tool, BaseTool? nextTool)
	{
		OptionsBox.RemoveAll ();
		tool_bar.RemoveAll ();
		tool.DoDeactivated (PintaCore.Workspace.ActiveDocumentOrDefault, nextTool);
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
