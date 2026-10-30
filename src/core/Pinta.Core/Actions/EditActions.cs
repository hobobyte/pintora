//
// EditActions.cs
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
using System.Collections.Generic;
using System.Linq;
using Cairo;

namespace Pinta.Core;

public sealed class EditActions
{
	public Command Undo { get; }
	public Command Redo { get; }
	public Command Cut { get; }
	public Command Copy { get; }
	public Command CopyMerged { get; }
	public Command Paste { get; }
	public Command PasteIntoNewLayer { get; }
	public Command PasteIntoNewImage { get; }
	public Command EraseSelection { get; }
	public Command FillSelection { get; }
	public Command InvertSelection { get; }
	public Command OffsetSelection { get; }
	public Command SelectAll { get; }
	public Command Deselect { get; }
	public Command LoadPalette { get; }
	public Command SavePalette { get; }
	public Command ResetPalette { get; }
	public Command ResizePalette { get; }

	private PathFileReference? last_palette_dir = null;
	private Document? active_document = null;

	private readonly ChromeManager chrome;
	private readonly PaletteFormatManager palette_formats;
	private readonly PaletteManager palette;
	private readonly ToolManager tools;
	private readonly WorkspaceManager workspace;
	private IFileDialogService FileDialogs => PintaCore.Services.GetService<IFileDialogService> ();
	public EditActions (
		ChromeManager chrome,
		PaletteFormatManager paletteFormats,
		PaletteManager palette,
		ToolManager tools,
		WorkspaceManager workspace)
	{
		Undo = new Command (
			"undo",
			Translations.GetString ("Undo"),
			null,
			Resources.StandardIcons.EditUndo,
			shortcuts: ["<Primary>Z"]);

		Redo = new Command (
			"redo",
			Translations.GetString ("Redo"),
			null,
			Resources.StandardIcons.EditRedo,
			shortcuts: ["<Primary><Shift>Z", "<Ctrl>Y"]);

		Cut = new Command (
			"cut",
			Translations.GetString ("Cut"),
			null,
			Resources.StandardIcons.EditCut,
			shortcuts: ["<Primary>X"]);

		Copy = new Command (
			"copy",
			Translations.GetString ("Copy"),
			null,
			Resources.StandardIcons.EditCopy,
			shortcuts: ["<Primary>C"]);

		CopyMerged = new Command (
			"copymerged",
			Translations.GetString ("Copy Merged"),
			null,
			Resources.StandardIcons.EditCopy,
			shortcuts: ["<Primary><Shift>C"]);

		Paste = new Command (
			"paste",
			Translations.GetString ("Paste"),
			null,
			Resources.StandardIcons.EditPaste,
			shortcuts: ["<Primary>V"]);

		PasteIntoNewLayer = new Command (
			"pasteintonewlayer",
			Translations.GetString ("Paste Into New Layer"),
			null,
			Resources.StandardIcons.EditPaste,
			shortcuts: ["<Primary><Shift>V"]);

		// Note: <Ctrl><Alt>V shortcut doesn't seem to work on Windows & macOS (bug 2047921).
		PasteIntoNewImage = new Command (
			"pasteintonewimage",
			Translations.GetString ("Paste Into New Image"),
			null,
			Resources.StandardIcons.EditPaste,
			shortcuts: ["<Shift>V", "<Primary><Alt>V"]);

		EraseSelection = new Command (
			"eraseselection",
			Translations.GetString ("Erase Selection"),
			null,
			Resources.Icons.EditSelectionErase,
			shortcuts: ["Delete"]);

		FillSelection = new Command (
			"fillselection",
			Translations.GetString ("Fill Selection"),
			null,
			Resources.Icons.EditSelectionFill,
			shortcuts: ["BackSpace"]);

		InvertSelection = new Command (
			"invertselection",
			Translations.GetString ("Invert Selection"),
			null,
			Resources.Icons.EditSelectionFill,
			shortcuts: ["<Primary>I"]);

		OffsetSelection = new Command (
			"offsetselection",
			Translations.GetString ("Offset Selection"),
			null,
			Resources.Icons.EditSelectionOffset,
			shortcuts: ["<Primary><Shift>O"]);

		SelectAll = new Command (
			"selectall",
			Translations.GetString ("Select All"),
			null,
			Resources.StandardIcons.EditSelectAll,
			shortcuts: ["<Primary>A"]);

		Deselect = new Command (
			"deselect",
			Translations.GetString ("Deselect All"),
			null,
			Resources.Icons.EditSelectionNone,
			shortcuts: ["<Primary><Shift>A", "<Ctrl>D"]);

		LoadPalette = new Command (
			"loadpalette",
			Translations.GetString ("Open..."),
			null,
			Resources.StandardIcons.DocumentOpen);

		SavePalette = new Command (
			"savepalette",
			Translations.GetString ("Save As..."),
			null,
			Resources.StandardIcons.DocumentSave);

		ResetPalette = new Command (
			"resetpalette",
			Translations.GetString ("Reset to Default"),
			null,
			Resources.StandardIcons.DocumentRevert);

		ResizePalette = new Command (
			"resizepalette",
			Translations.GetString ("Set Number of Colors"),
			null,
			Resources.Icons.ImageResize);

		Undo.Sensitive = false;
		Redo.Sensitive = false;

		this.chrome = chrome;
		palette_formats = paletteFormats;
		this.palette = palette;
		this.tools = tools;
		this.workspace = workspace;
	}

	public void RegisterActions (IActionRegistrar registrar, IActionMenu menu)
	{
		IActionMenu paste_section = menu.CreateSubmenu ();
		paste_section.AppendItem (Cut);
		paste_section.AppendItem (Copy);
		paste_section.AppendItem (CopyMerged);
		paste_section.AppendItem (Paste);
		paste_section.AppendItem (PasteIntoNewLayer);
		paste_section.AppendItem (PasteIntoNewImage);

		IActionMenu sel_section = menu.CreateSubmenu ();
		sel_section.AppendItem (SelectAll);
		sel_section.AppendItem (Deselect);

		IActionMenu edit_sel_section = menu.CreateSubmenu ();
		edit_sel_section.AppendItem (EraseSelection);
		edit_sel_section.AppendItem (FillSelection);
		edit_sel_section.AppendItem (InvertSelection);
		edit_sel_section.AppendItem (OffsetSelection);

		IActionMenu palette_section = menu.CreateSubmenu ();

		IActionMenu palette_menu = menu.CreateSubmenu ();
		palette_menu.AppendItem (LoadPalette);
		palette_menu.AppendItem (SavePalette);
		palette_menu.AppendItem (ResetPalette);
		palette_menu.AppendItem (ResizePalette);

		menu.AppendItem (Undo);
		menu.AppendItem (Redo);
		menu.AppendSection (null, paste_section);
		menu.AppendSection (null, sel_section);
		menu.AppendSection (null, edit_sel_section);
		menu.AppendSection (null, palette_section);
		menu.AppendSubmenu (Translations.GetString ("Palette"), palette_menu);

		registrar.AddCommands ([

			Undo,
			Redo,

			Cut,
			Copy,
			CopyMerged,
			Paste,
			PasteIntoNewLayer,
			PasteIntoNewImage,

			SelectAll,
			Deselect,

			EraseSelection,
			FillSelection,
			InvertSelection,
			OffsetSelection,
			LoadPalette,
			SavePalette,
			ResetPalette,
			ResizePalette]);
	}

	public void CreateHistoryWindowToolBar (IToolbarSurface toolbar)
	{
		IToolbarWidgetFactory widgetFactory = PintaCore.Services.GetService<IToolbarWidgetFactory> ();
		toolbar.Append (widgetFactory.CreateToolBarItem (Undo));
		toolbar.Append (widgetFactory.CreateToolBarItem (Redo));
	}

	public void RegisterHandlers ()
	{
		Deselect.Activated += HandlePintaCoreActionsEditDeselectActivated;
		EraseSelection.Activated += HandlePintaCoreActionsEditEraseSelectionActivated;
		SelectAll.Activated += HandlePintaCoreActionsEditSelectAllActivated;
		FillSelection.Activated += HandlePintaCoreActionsEditFillSelectionActivated;
		Copy.Activated += HandlerPintaCoreActionsEditCopyActivated;
		CopyMerged.Activated += HandlerPintaCoreActionsEditCopyMergedActivated;
		Undo.Activated += HandlerPintaCoreActionsEditUndoActivated;
		Redo.Activated += HandlerPintaCoreActionsEditRedoActivated;
		Cut.Activated += HandlerPintaCoreActionsEditCutActivated;
		LoadPalette.Activated += HandlerPintaCoreActionsEditLoadPaletteActivated;
		SavePalette.Activated += HandlerPintaCoreActionsEditSavePaletteActivated;
		ResetPalette.Activated += HandlerPintaCoreActionsEditResetPaletteActivated;
		InvertSelection.Activated += HandleInvertSelectionActivated;

		workspace.ActiveDocumentChanged += WorkspaceActiveDocumentChanged;

		workspace.SelectionChanged += (o, _) => {
			var visible = false;
			if (workspace.HasOpenDocuments)
				visible = workspace.ActiveDocument.Selection.Visible;

			Deselect.Sensitive = visible;
			EraseSelection.Sensitive = visible;
			FillSelection.Sensitive = visible;
			InvertSelection.Sensitive = visible;
			OffsetSelection.Sensitive = visible;
		};
	}

	#region Action Handlers
	private void HandlePintaCoreActionsEditFillSelectionActivated (object sender, EventArgs e)
	{
		Document doc = workspace.ActiveDocument;

		tools.Commit ();

		ImageSurface old = doc.Layers.CurrentUserLayer.CairoSurface.Clone ();

		using Context g = new (doc.Layers.CurrentUserLayer.CairoSurface);

		g.AppendPath (doc.Selection.SelectionPath);
		g.FillRule = FillRule.EvenOdd;

		g.SetSourceColor (palette.PrimaryColor);
		g.Fill ();

		doc.Workspace.Invalidate ();
		doc.History.PushNewItem (
			new SimpleHistoryItem (
				Resources.Icons.EditSelectionFill,
				Translations.GetString ("Fill Selection"),
				old,
				doc.Layers.CurrentUserLayerIndex
			)
		);
	}

	private void HandlePintaCoreActionsEditSelectAllActivated (object sender, EventArgs e)
	{
		Document doc = workspace.ActiveDocument;

		tools.Commit ();

		SelectionHistoryItem hist = new (
			workspace,
			Resources.StandardIcons.EditSelectAll,
			Translations.GetString ("Select All"));

		hist.TakeSnapshot ();

		doc.ResetSelectionPaths ();
		doc.Selection.Visible = true;

		doc.History.PushNewItem (hist);
		doc.Workspace.Invalidate ();
	}

	private void HandlePintaCoreActionsEditEraseSelectionActivated (object sender, EventArgs e)
	{
		Document doc = workspace.ActiveDocument;

		tools.Commit ();

		ImageSurface old = doc.Layers.CurrentUserLayer.CairoSurface.Clone ();

		using Context g = new (doc.Layers.CurrentUserLayer.CairoSurface);

		g.AppendPath (doc.Selection.SelectionPath);
		g.FillRule = FillRule.EvenOdd;

		g.Operator = Cairo.Operator.Clear;
		g.Fill ();

		doc.Workspace.Invalidate ();

		doc.History.PushNewItem (
			sender switch {
				string and "Cut" => new SimpleHistoryItem (Resources.StandardIcons.EditCut, Translations.GetString ("Cut"), old, doc.Layers.CurrentUserLayerIndex),
				_ => new SimpleHistoryItem (Resources.Icons.EditSelectionErase, Translations.GetString ("Erase Selection"), old, doc.Layers.CurrentUserLayerIndex),
			}
		);
	}

	private void HandlePintaCoreActionsEditDeselectActivated (object sender, EventArgs e)
	{
		Document doc = workspace.ActiveDocument;

		tools.Commit ();

		SelectionHistoryItem hist = new (
			workspace,
			Resources.Icons.EditSelectionNone,
			Translations.GetString ("Deselect"));

		hist.TakeSnapshot ();

		doc.ResetSelectionPaths ();

		doc.History.PushNewItem (hist);
		doc.Workspace.Invalidate ();
	}

	private void HandlerPintaCoreActionsEditCopyActivated (object sender, EventArgs e)
	{
		Document doc = workspace.ActiveDocument;
		IClipboardService cb = PintaCore.Services.GetService<IClipboardService> ();
		if (tools.CurrentTool?.DoHandleCopy (doc, cb) == true)
			return;

		tools.Commit ();

		CopyLayersToClipboard (doc, doc.Layers.CurrentUserLayer.GetLayersToPaint (), cb);
	}

	private void HandlerPintaCoreActionsEditCopyMergedActivated (object sender, EventArgs e)
	{
		tools.Commit ();

		Document doc = workspace.ActiveDocument;
		IClipboardService cb = PintaCore.Services.GetService<IClipboardService> ();
		CopyLayersToClipboard (doc, doc.Layers.GetLayersToPaint (), cb);
	}

	private static void CopyLayersToClipboard (Document doc, IEnumerable<Layer> layers, IClipboardService clipboard)
	{
		RectangleI rect = doc.GetSelectedBounds (true);
		if (rect.IsEmpty)
			return;

		using ImageSurface dest = CairoExtensions.CreateImageSurface (Format.Argb32, rect.Width, rect.Height);
		using Context g = new (dest);

		// Move the selected region to the upper left of the target surface, and clip to the original selection.
		g.Translate (-rect.X, -rect.Y);
		doc.Selection.Clip (g);

		foreach (Layer layer in layers) {
			layer.Draw (g);
		}

		CopyImageToClipboard (clipboard, dest, new PointI (rect.X, rect.Y), doc.Selection);
	}

	/// <summary>
	/// Copy an image to the clipboard.
	/// This records two content providers: one with the image (equivalent to Clipboard.SetTexture()), and
	/// one storing additional custom data that Pinta can check for when pasting.
	/// </summary>
	private static void CopyImageToClipboard (IClipboardService clipboard, ImageSurface image, PointI srcPos, DocumentSelection selection)
	{
		clipboard.WriteImage (new ClipboardImageData (CairoImageSurface.Wrap (image), srcPos));
	}

	private void HandlerPintaCoreActionsEditCutActivated (object sender, EventArgs e)
	{
		Document doc = workspace.ActiveDocument;

		IClipboardService cb = PintaCore.Services.GetService<IClipboardService> ();

		if (tools.CurrentTool?.DoHandleCut (doc, cb) == true)
			return;

		tools.Commit ();

		// Copy selection
		HandlerPintaCoreActionsEditCopyActivated (sender, e);

		// Erase selection
		HandlePintaCoreActionsEditEraseSelectionActivated ("Cut", e);
	}

	private void HandlerPintaCoreActionsEditUndoActivated (object sender, EventArgs e)
	{
		Document doc = workspace.ActiveDocument;

		if (tools.CurrentTool?.DoHandleUndo (doc) == true)
			return;

		doc.History.Undo ();

		tools.CurrentTool?.DoAfterUndo (doc);
	}

	private void HandlerPintaCoreActionsEditRedoActivated (object sender, EventArgs e)
	{
		Document doc = workspace.ActiveDocument;

		if (tools.CurrentTool?.DoHandleRedo (doc) == true)
			return;

		doc.History.Redo ();

		tools.CurrentTool?.DoAfterRedo (doc);
	}

	private async void HandlerPintaCoreActionsEditLoadPaletteActivated (object sender, EventArgs e)
	{

		IFileReference? choice = await FileDialogs.OpenFileAsync (new FileDialogOptions (
			Translations.GetString ("Open Palette File"),
			[CreatePalettesFilter (), new FileDialogFilter (Translations.GetString ("All files"), [])],
			last_palette_dir));

		if (choice is null)
			return;

		PathFileReference file = choice as PathFileReference ?? throw new InvalidOperationException ("The selected file is not path-backed.");
		last_palette_dir = GetParentDirectory (file);

		try {
			palette.CurrentPalette.Load (palette_formats, file);

		} catch (PaletteLoadException ex) {

			await chrome.ShowUnsupportedFormatDialog (
				palette_formats.Formats,
				ex.FileName,
				Translations.GetString ("Unsupported palette format"),
				ex.Message);
		}
	}

	private async void HandlerPintaCoreActionsEditSavePaletteActivated (object sender, EventArgs e)
	{
		var writableFormats = palette_formats.Formats.Where (format => !format.IsReadOnly ()).ToArray ();
		var filters = writableFormats.Select (format => new FileDialogFilter (format.DisplayName, format.Extensions)).ToArray ();
		IFileReference? choice = await FileDialogs.SaveFileAsync (new FileDialogOptions (
			Translations.GetString ("Save Palette File"),
			filters,
			last_palette_dir));
		if (choice is null)
			return;

		string path = (choice as PathFileReference)?.Path ?? throw new InvalidOperationException ("The selected file is not path-backed.");
		string extension = System.IO.Path.GetExtension (path);
		if (string.IsNullOrEmpty (extension))
			path += "." + writableFormats.First ().Extensions.First ();

		PathFileReference file = new (path);
		PaletteDescriptor format = palette_formats.GetFormatByFilename (path) ?? throw new FormatException ();
		palette.CurrentPalette.Save (file, format.Saver);
		last_palette_dir = GetParentDirectory (file);
	}

	private static PathFileReference? GetParentDirectory (PathFileReference file)
	{
		string? directory = System.IO.Path.GetDirectoryName (file.Path);
		return string.IsNullOrEmpty (directory) ? null : new PathFileReference (directory);
	}

	private FileDialogFilter CreatePalettesFilter ()
	{
		var extensions = new List<string> ();

		foreach (var format in palette_formats.Formats) {

			if (format.IsWriteOnly ())
				continue;

			foreach (var ext in format.Extensions)
				extensions.Add (ext);
		}
		return new FileDialogFilter (Translations.GetString ("Palette files"), extensions);
	}

	private void HandlerPintaCoreActionsEditResetPaletteActivated (object sender, EventArgs e)
	{
		palette.CurrentPalette.LoadDefault ();
	}

	void HandleInvertSelectionActivated (object sender, EventArgs e)
	{
		tools.Commit ();

		Document doc = workspace.ActiveDocument;

		// Clear the selection resize handles if necessary.
		doc.Layers.ToolLayer.Clear ();

		SelectionHistoryItem historyItem = new (
			workspace,
			Resources.Icons.EditSelectionInvert,
			Translations.GetString ("Invert Selection"));

		historyItem.TakeSnapshot ();

		doc.Selection.Invert (doc.ImageSize);

		doc.History.PushNewItem (historyItem);
		doc.Workspace.Invalidate ();
	}

	private void WorkspaceActiveDocumentChanged (object? sender, EventArgs e)
	{
		if (active_document != null) {
			active_document.History.HistoryItemAdded -= OnDocumentHistoryChanged;
			active_document.History.ActionRedone -= OnDocumentHistoryChanged;
			active_document.History.ActionUndone -= OnDocumentHistoryChanged;
		}

		active_document = workspace.ActiveDocumentOrDefault;

		// Register event handlers for the new document to update the undo/redo state.
		if (active_document != null) {
			active_document.History.HistoryItemAdded += OnDocumentHistoryChanged;
			active_document.History.ActionRedone += OnDocumentHistoryChanged;
			active_document.History.ActionUndone += OnDocumentHistoryChanged;
		}

		// Force an update of the undo/redo state after switching documents.
		UpdateUndoRedoActions ();
	}

	private void OnDocumentHistoryChanged (object? sender, EventArgs e)
	{
		UpdateUndoRedoActions ();
	}

	private void UpdateUndoRedoActions ()
	{
		if (active_document != null) {
			Redo.Sensitive = active_document.Workspace.History.CanRedo;
			Undo.Sensitive = active_document.Workspace.History.CanUndo;
		} else {
			Redo.Sensitive = false;
			Undo.Sensitive = false;
		}

	}

	#endregion
}
