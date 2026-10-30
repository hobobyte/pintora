//
// SelectTool.cs
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
using Pinta.Core;

namespace Pinta.Tools;

public abstract class SelectTool : BaseTool
{
	private readonly IToolService tools;
	private readonly IWorkspaceService workspace;

	private SelectionHistoryItem? hist = default;
	private CombineMode combine_mode = default;
	private IToolBarSeparator? mode_sep;
	private IToolBarDropDownButton? auto_scroll_button;

	public override StandardKey? ShortcutKeyStandard => StandardKey.S;
	public override bool IsSelectionTool => true;
	protected override bool ShowAntialiasingButton => false;
	private bool IsAutoScroll => AutoScrollButton.SelectedItem.GetTagOrDefault (true);
	private readonly RectangleHandle handle;
	public override IEnumerable<IToolHandle> Handles => [handle];

	public SelectTool (IServiceProvider services) : base (services)
	{
		tools = services.GetService<IToolService> ();
		workspace = services.GetService<IWorkspaceService> ();

		handle = new (workspace) { InvertIfNegative = true };

		workspace.SelectionChanged += AfterSelectionChange;
	}

	protected abstract void DrawShape (Document document, RectangleD r, Layer l);

	protected override void OnBuildToolBar (IToolbarSurface tb)
	{
		PintaCore.Services.GetService<IToolUiService> ().BuildSelectionModeToolbar (tb.NativeToolbar, workspace.SelectionHandler, Settings);

		tb.Append (Separator.NativeWidget);

		tb.Append (AutoScrollButton.NativeWidget);
	}

	private static PointD AdjustMousePosition (Document document, in PointD position)
		=> SelectionAlgorithms.ClampPointToImage (position, document.ImageSize);

	protected override void OnMouseDown (Document document, ToolMouseEventArgs e)
	{
		// Ignore extra button clicks while drawing
		if (handle.IsDragging)
			return;

		hist = new SelectionHistoryItem (workspace, Icon, Name);
		hist.TakeSnapshot ();

		if (handle.BeginDrag (e.PointDouble, document.ImageSize))
			return;

		// Start drawing a new rectangle.
		combine_mode = PintaCore.Workspace.SelectionHandler.DetermineCombineMode (e);

		PointD adjusted = AdjustMousePosition (document, e.PointDouble);
		handle.Rectangle = new (adjusted.X, adjusted.Y, 0.0, 0.0);

		document.PreviousSelection = document.Selection.Clone ();
		document.Selection.SelectionPolygons.Clear ();

		if (!handle.BeginDrag (adjusted, document.ImageSize))
			throw new InvalidOperationException ("Should be able to start drawing a new rectangle!");
	}

	protected override void OnMouseMove (Document document, ToolMouseEventArgs e)
	{
		if (!handle.IsDragging) {
			UpdateCursor (e.WindowPoint);
			return;
		}

		PointD adjusted = AdjustMousePosition (document, e.PointDouble);
		handle.UpdateDrag (adjusted, e.IsShiftPressed);

		ReDraw (document);

		SelectionModeHandler.PerformSelectionMode (document, combine_mode, document.Selection.SelectionPolygons);

		if (!IsAutoScroll)
			return;

		var viewport = document.Workspace.CanvasViewportSize;
		if (viewport is null)
			return;

		var h_adjust = (double) viewport.Value.Width;
		var v_adjust = (double) viewport.Value.Height;

		//step of 10 pixels or of 1% of visible area, whichever is greater
		int canvasStep = (int) Math.Max (10, h_adjust * 0.01);

		PointI direction = default;
		if (e.RootPoint.X < 0)
			direction = new PointI (-canvasStep, 0); //move left
		if (e.RootPoint.Y < 0)
			direction = new PointI (0, -canvasStep); //move up
		if (e.RootPoint.X > h_adjust)
			direction = new PointI (canvasStep, 0); //move right
		if (e.RootPoint.Y > v_adjust)
			direction = new PointI (0, canvasStep); //move down

		if (direction != default)
			document.Workspace.ScrollCanvas (direction);
	}

	protected override void OnMouseUp (Document document, ToolMouseEventArgs e)
	{
		PointD adjusted = AdjustMousePosition (document, e.PointDouble);
		if (handle.HasDragged (adjusted) && handle.Rectangle.Width > 0 && handle.Rectangle.Height > 0) {
			ReDraw (document);

			SelectionModeHandler.PerformSelectionMode (document, combine_mode, document.Selection.SelectionPolygons);

			document.Selection.HandleBounds = handle.Rectangle;

			if (hist != null) {
				document.History.PushNewItem (hist);
				hist = null;
			}

			handle.EndDrag ();
		} else {
			// If the user didn't move the mouse, they want to deselect

			// Mark as being done interactive drawing before invoking the deselect action.
			// This will allow AfterSelectionChanged() to clear the selection.
			handle.EndDrag ();

			if (hist != null) {
				// Roll back any changes made to the selection, e.g. in OnMouseDown().
				hist.Undo ();

				hist = null;
			}

			PintaCore.Actions.Edit.Deselect.Activate ();
		}

		// Update the mouse cursor.
		UpdateCursor (e.WindowPoint);
	}

	protected override void OnActivated (Document? document)
	{
		base.OnActivated (document);

		// When entering the tool, update the selection handles from the
		// document's current selection.
		if (document is null) return;

		LoadFromDocument (document);
	}

	protected override void OnSaveSettings (ISettingsService settings)
	{
		base.OnSaveSettings (settings);

		workspace.SelectionHandler.OnSaveSettings (settings);

		if (auto_scroll_button is not null) {
			settings.PutSetting (SettingNames.SELECTION_MODE, auto_scroll_button.SelectedIndex);
		}
	}

	private void ReDraw (Document document)
	{
		document.Selection.Visible = true;

		ShowHandles (true);

		RectangleD rect = handle.Rectangle;
		DrawShape (document, rect, document.Layers.SelectionLayer);
	}

	private void ShowHandles (bool visible)
	{
		handle.Active = visible;
	}

	private void UpdateCursor (PointD viewPos)
	{
		string? cursorName = handle.Active ?
			handle.GetCursorAtPoint (viewPos) :
			null;

		if (cursorName is null)
			SetCursor (DefaultCursor);
		else
			SetCursor (cursorName);
	}

	protected override void OnAfterUndo (Document document)
	{
		base.OnAfterUndo (document);
		LoadFromDocument (document);
	}

	protected override void OnAfterRedo (Document document)
	{
		base.OnAfterRedo (document);
		LoadFromDocument (document);
	}

	private void AfterSelectionChange (object? sender, EventArgs event_args)
	{
		if (handle.IsDragging || !workspace.HasOpenDocuments)
			return;

		// TODO: Try to remove this ActiveDocument call
		LoadFromDocument (workspace.ActiveDocument);
	}

	/// <summary>
	/// Initialize from the document's selection.
	/// </summary>
	private void LoadFromDocument (Document document)
	{
		DocumentSelection selection = document.Selection;
		handle.Rectangle = selection.HandleBounds;
		ShowHandles (document.Selection.Visible && tools.CurrentTool == this);
	}

	private IToolBarSeparator Separator => mode_sep ??= WidgetFactory.CreateSeparator ();

	private IToolBarDropDownButton AutoScrollButton {
		get {
			if (auto_scroll_button is null) {
				auto_scroll_button ??= CreateToolDropDownButton ();

				auto_scroll_button.AddItem (Translations.GetString ("Autoscroll On"), Pinta.Resources.Icons.EffectsBlursZoomBlur, true);
				auto_scroll_button.AddItem (Translations.GetString ("Autoscroll Off"), Pinta.Resources.Icons.EffectsBlursUnfocus, false);

				auto_scroll_button.SelectedIndex = Settings.GetSetting (SettingNames.SELECTION_MODE, 0);
			}

			return auto_scroll_button;
		}

	}
}
