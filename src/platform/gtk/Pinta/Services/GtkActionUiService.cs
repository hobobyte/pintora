using Pinta.Core;

namespace Pinta;

internal sealed class GtkActionUiService : IActionUiService
{
	private readonly ChromeManager chrome;

	public GtkActionUiService (ChromeManager chrome) => this.chrome = chrome;

	public void CreateToolBar (ActionManager actions, object toolbar)
	{
		Gtk.Box target = (Gtk.Box) toolbar;
		target.Append (actions.File.New.CreateToolBarItem ());
		target.Append (actions.File.Open.CreateToolBarItem ());
		target.Append (actions.File.Save.CreateToolBarItem ());
		target.Append (GtkExtensions.CreateToolBarSeparator ());
		if (actions.OperatingSystem == OS.Windows) {
			target.Append (actions.Edit.Cut.CreateToolBarItem ());
			target.Append (actions.Edit.Copy.CreateToolBarItem ());
			target.Append (actions.Edit.Paste.CreateToolBarItem ());
			target.Append (GtkExtensions.CreateToolBarSeparator ());
		}
		target.Append (actions.Edit.Undo.CreateToolBarItem ());
		target.Append (actions.Edit.Redo.CreateToolBarItem ());
		target.Append (GtkExtensions.CreateToolBarSeparator ());
		target.Append (actions.Edit.Cut.CreateToolBarItem ());
		target.Append (actions.Edit.Copy.CreateToolBarItem ());
		target.Append (actions.Edit.Paste.CreateToolBarItem ());
		target.Append (GtkExtensions.CreateToolBarSeparator ());
		target.Append (actions.Image.CropToSelection.CreateToolBarItem ());
		target.Append (actions.Edit.Deselect.CreateToolBarItem ());
	}

	public void CreateHeaderToolBar (ActionManager actions, object header)
	{
		Adw.HeaderBar target = (Adw.HeaderBar) header;
		target.PackStart (actions.File.New.CreateToolBarItem ());
		target.PackStart (actions.File.Open.CreateToolBarItem ());
		target.PackStart (actions.File.Save.CreateToolBarItem ());
		target.PackStart (GtkExtensions.CreateToolBarSeparator ());
		target.PackStart (actions.Edit.Undo.CreateToolBarItem ());
		target.PackStart (actions.Edit.Redo.CreateToolBarItem ());
		target.PackStart (GtkExtensions.CreateToolBarSeparator ());
		target.PackStart (actions.Edit.Cut.CreateToolBarItem ());
		target.PackStart (actions.Edit.Copy.CreateToolBarItem ());
		target.PackStart (actions.Edit.Paste.CreateToolBarItem ());
		target.PackStart (GtkExtensions.CreateToolBarSeparator ());
		target.PackStart (actions.Image.CropToSelection.CreateToolBarItem ());
		target.PackStart (actions.Edit.Deselect.CreateToolBarItem ());
	}

	public void CreateStatusBar (ActionManager actions, WorkspaceManager workspace, object statusbar)
	{
		Gtk.Box target = (Gtk.Box) statusbar;
		target.Append (Gtk.Image.NewFromIconName (Resources.Icons.CursorPosition));
		Gtk.Label cursor = Gtk.Label.New ("0, 0");
		cursor.Xalign = 0.0f;
		cursor.Halign = Gtk.Align.Start;
		cursor.WidthChars = 11;
		target.Append (cursor);
		chrome.LastCanvasCursorPointChanged += (_, _) => {
			var point = chrome.LastCanvasCursorPoint;
			cursor.SetText ($"{point.X}, {point.Y}");
		};

		target.Append (Gtk.Image.NewFromIconName (Resources.Icons.ToolSelectRectangle));
		Gtk.Label selection = Gtk.Label.New ("0, 0");
		selection.Xalign = 0.0f;
		selection.Halign = Gtk.Align.Start;
		selection.WidthChars = 11;
		target.Append (selection);
		workspace.SelectionChanged += (_, _) => {
			var bounds = workspace.HasOpenDocuments ? workspace.ActiveDocument.Selection.GetBounds () : new RectangleD ();
			selection.SetText ($"{bounds.Width}, {bounds.Height}");
		};
		target.Append (actions.View.ZoomOut.CreateToolBarItem ());
		target.Append ((Gtk.Widget) actions.View.ZoomComboBox.NativeWidget);
		target.Append (actions.View.ZoomIn.CreateToolBarItem ());
	}
}
