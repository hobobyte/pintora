//
// DocumentWorkspace.cs
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

namespace Pinta.Core;

public sealed class DocumentWorkspace
{
	private readonly Document document;

	private readonly ActionManager actions;
	private readonly DocumentViewportState viewport;

	/// <summary>
	/// Optional UI host for viewport operations. The legacy GTK path is used when unset.
	/// </summary>
	private IDocumentViewportHost? viewport_host;

	public IDocumentViewportHost? ViewportHost {
		get => viewport_host;
		set {
			viewport_host = value;
			viewport_host?.UpdateContentSize (viewport.ViewSize);
		}
	}

	private enum ZoomType
	{
		ZoomIn,
		ZoomOut,
		ZoomManually,
	}

	internal DocumentWorkspace (
		ActionManager actions,
		Document document)
	{
		this.actions = actions;

		this.document = document;
		viewport = new DocumentViewportState (document.ImageSize);

		History = new DocumentHistory (document);
	}

	#region Public Events
	public event EventHandler<CanvasInvalidatedEventArgs>? CanvasInvalidated;
	public event EventHandler? ViewSizeChanged;
	#endregion

	#region Public Properties
	public Gtk.Widget Canvas { get; set; } = null!; // NRT - This is set soon after creation
	public Gtk.Widget CanvasWindow { get; set; } = null!; // NRT - This is set soon after creation

	/// <summary>
	/// Size of the visible canvas viewport (the area inside the scrollbars) in window
	/// coordinates, or null when the canvas is not hosted in a scrollable viewport.
	/// </summary>
	public Size? CanvasViewportSize {
		get {
			if (Canvas.Parent is Gtk.Viewport view) {
				double width = view.GetHadjustment ()!.PageSize;
				double height = view.GetVadjustment ()!.PageSize;
				return new Size ((int) width, (int) height);
			}

			return null;
		}
	}

	/// <summary>
	/// Returns whether the zoomed image fits in the window without requiring scrolling.
	/// </summary>
	public bool ImageViewFitsInWindow {
		get {
			if (ViewportHost is not null)
				return ViewportHost.ImageViewFitsInWindow;

			Gtk.Viewport view = (Gtk.Viewport) Canvas.Parent!;
			int window_x = view.GetAllocatedWidth ();
			int window_y = view.GetAllocatedHeight ();
			return ViewSize.Width <= window_x && ViewSize.Height <= window_y;
		}
	}

	/// <summary>
	/// Size of the zoomed image.
	/// </summary>
	public Size ViewSize {
		get => viewport.ViewSize;
		set {
			if (viewport.ViewSize == value) return;
			viewport.SetScale (value.Width / (double) viewport.ImageSize.Width);
			ViewportHost?.UpdateContentSize (viewport.ViewSize);
			OnViewSizeChanged ();
		}
	}

	public DocumentHistory History { get; }

	/// <summary>
	/// Returns whether the image (at 100% zoom) would fit in the window without requiring scrolling.
	/// </summary>
	public bool ImageFitsInWindow {
		get {
			if (ViewportHost is not null)
				return ViewportHost.ImageFitsInWindow;

			Gtk.Viewport view = (Gtk.Viewport) Canvas.Parent!;
			int window_x = view.GetAllocatedWidth ();
			int window_y = view.GetAllocatedHeight ();
			return document.ImageSize.Width <= window_x && document.ImageSize.Height <= window_y;
		}
	}

	/// <summary>
	/// Scale factor for the zoomed image.
	/// </summary>
	public double Scale {
		get => viewport.Scale;
		set {
			if (value == Scale)
				return;

			viewport.SetScale (value);
			OnViewSizeChanged ();

			Invalidate ();
		}
	}

	internal void SetImageSize (Size imageSize)
	{
		Size oldViewSize = viewport.ViewSize;
		viewport.ImageSize = imageSize;
		ViewportHost?.UpdateContentSize (viewport.ViewSize);
		if (viewport.ViewSize != oldViewSize)
			OnViewSizeChanged ();
	}

	#endregion

	#region Public Methods
	public void Invalidate ()
	{
		OnCanvasInvalidated (new CanvasInvalidatedEventArgs ());
	}

	/// <summary>
	/// Repaints a rectangle region on the canvas.
	/// </summary>
	/// <param name='canvasRect'>
	/// The rectangle region of the canvas requiring repainting
	/// </param>
	public void Invalidate (RectangleI canvasRect)
	{
		OnCanvasInvalidated (new CanvasInvalidatedEventArgs (canvasRect));
	}

	/// <summary>
	/// Repaints a rectangle region in the window.
	/// Note that this overload uses window coordinates, whereas Invalidate() uses canvas coordinates.
	/// </summary>
	public void InvalidateWindowRect (RectangleI windowRect)
	{
		PointD windowTopLeft = new (windowRect.Left, windowRect.Top);
		PointD windowBtmRight = new (windowRect.Right + 1, windowRect.Bottom + 1);

		PointD canvasTopLeft = ViewPointToCanvas (windowTopLeft);
		PointD canvasBtmRight = ViewPointToCanvas (windowBtmRight);

		RectangleI canvasRect = RectangleD.FromPoints (canvasTopLeft, canvasBtmRight).ToInt ();
		OnCanvasInvalidated (new CanvasInvalidatedEventArgs (canvasRect));
	}

	/// <summary>
	/// Grabs focus to the canvas widget. This can be used to avoid leaving focus in
	/// toolbar widgets, for example.
	/// </summary>
	public void GrabFocusToCanvas ()
	{
		if (ViewportHost is not null) {
			ViewportHost.GrabFocus ();
			return;
		}

		bool gained_focus = CanvasWindow.GrabFocus ();
		// Log a warning if something went wrong, e.g. there is a non-focusable widget
		// in the hierarchy.
		if (!gained_focus)
			Console.Error.WriteLine ("Failed to gain focus on the canvas widget!");
	}

	/// <summary>
	/// Determines whether the rectangle lies (at least partially) outside the canvas area.
	/// </summary>
	public bool IsPartiallyOffscreen (RectangleI rect)
		=> rect.IsEmpty || rect.Left < 0 || rect.Top < 0;

	public bool PointInCanvas (PointD point)
	{
		if (point.X < 0 || point.Y < 0)
			return false;

		if (point.X >= document.ImageSize.Width || point.Y >= document.ImageSize.Height)
			return false;

		return true;
	}

	public void RecenterView (PointD point)
	{
		if (ViewportHost is not null) {
			ViewportHost.Recenter (point, Scale);
			return;
		}

		Gtk.Viewport view = (Gtk.Viewport) Canvas.Parent!;

		var h_adjust = view.GetHadjustment ()!;
		h_adjust.Value = Math.Clamp (point.X * Scale - h_adjust.PageSize / 2, h_adjust.Lower, h_adjust.Upper);

		var v_adjust = view.GetVadjustment ()!;
		v_adjust.Value = Math.Clamp (point.Y * Scale - v_adjust.PageSize / 2, v_adjust.Lower, v_adjust.Upper);
	}

	public void ScrollCanvas (PointI delta)
	{
		if (ViewportHost is not null) {
			ViewportHost.Scroll (delta);
			return;
		}

		Gtk.Viewport view = (Gtk.Viewport) Canvas.Parent!;

		var h_adjust = view.GetHadjustment ()!;
		h_adjust.Value = Math.Clamp (delta.X + h_adjust.Value, h_adjust.Lower, h_adjust.Upper - h_adjust.PageSize);

		var v_adjust = view.GetVadjustment ()!;
		v_adjust.Value = Math.Clamp (delta.Y + v_adjust.Value, v_adjust.Lower, v_adjust.Upper - v_adjust.PageSize);
	}

	/// <summary>
	/// Converts a point from image view coordinates to canvas coordinates
	/// </summary>
	/// <param name='x'>
	/// The X coordinate of the view point
	/// </param>
	/// <param name='y'>
	/// The Y coordinate of the view point
	/// </param>
	public PointD ViewPointToCanvas (PointD viewPoint)
		=> viewport.ViewPointToCanvas (viewPoint);

	/// <summary>
	/// Converts a point from canvas coordinates to view coordinates
	/// </summary>
	public PointD CanvasPointToView (PointD canvasPoint)
		=> viewport.CanvasPointToView (canvasPoint);

	public void ZoomIn ()
	{
		ZoomAroundCenter (ZoomType.ZoomIn);
	}

	public void ZoomOut ()
	{
		ZoomAroundCenter (ZoomType.ZoomOut);
	}

	public void ZoomInAroundViewPoint (in PointD view_point)
	{
		ZoomAndRecenterView (ZoomType.ZoomIn, view_point); // Zoom in relative to mouse position.
	}

	public void ZoomInAroundCanvasPoint (in PointD canvas_point)
	{
		ZoomInAroundViewPoint (CanvasPointToView (canvas_point));
	}

	public void ZoomOutAroundViewPoint (in PointD view_point)
	{
		ZoomAndRecenterView (ZoomType.ZoomOut, view_point); // Zoom out relative to mouse position.
	}

	public void ZoomOutAroundCanvasPoint (in PointD canvas_point)
	{
		ZoomOutAroundViewPoint (CanvasPointToView (canvas_point));
	}

	public void ZoomManually ()
	{
		ZoomAroundCenter (ZoomType.ZoomManually);
	}

	public void ZoomToCanvasRectangle (RectangleD rect)
	{
		double ratio =
			(document.ImageSize.Width / rect.Width <= document.ImageSize.Height / rect.Height)
			? document.ImageSize.Width / rect.Width
			: document.ImageSize.Height / rect.Height;

		actions.View.ZoomComboBox.ComboBox.GetEntry ().SetText (ViewActions.ToPercent (ratio));
		GLib.MainContext.Default ().Iteration (false); //Force update of scrollbar upper before recenter

		PointD newPoint = new (
			X: rect.X + rect.Width / 2,
			Y: rect.Y + rect.Height / 2);

		RecenterView (newPoint);
	}
	#endregion

	#region Private Methods
	private void OnCanvasInvalidated (CanvasInvalidatedEventArgs e)
	{
		CanvasInvalidated?.Invoke (this, e);
	}

	public void OnViewSizeChanged ()
	{
		ViewSizeChanged?.Invoke (this, EventArgs.Empty);
	}

	/// <summary>
	/// Zoom in/out around the center of the screen.
	/// </summary>
	private void ZoomAroundCenter (ZoomType zoomType)
	{
		PointD center = ViewportHost?.ViewCenter ?? GetLegacyViewportCenter ();
		ZoomAndRecenterView (zoomType, center);
	}

	/// <summary>
	/// Zoom in/out around a specific point.
	/// </summary>
	/// <param name="center_point">Center point to zoom around, in view coordinates</param>
	private void ZoomAndRecenterView (ZoomType zoomType, PointD center_point)
	{
		if (zoomType == ZoomType.ZoomOut && (ViewSize.Width == 1 || ViewSize.Height == 1))
			return; //Can't zoom in past a 1x1 px canvas

		if (!ViewActions.TryParsePercent (actions.View.ZoomComboBox.ComboBox.GetActiveText ()!, out var zoom))
			zoom = Scale * 100;

		zoom = Math.Min (zoom, 3600);

		actions.View.SuspendZoomUpdate ();

		PointD scroll_offset = ViewportHost?.ScrollOffset ?? GetLegacyViewportScrollOffset ();
		double scroll_offset_x = center_point.X - scroll_offset.X;
		double scroll_offset_y = center_point.Y - scroll_offset.Y;

		PointD canvas_point = ViewPointToCanvas (center_point);

		if (zoomType == ZoomType.ZoomIn || zoomType == ZoomType.ZoomOut) {

			int i = 0;

			bool UpdateZoomLevel (string zoomInList)
			{
				if (!ViewActions.TryParsePercent (zoomInList, out var zoom_level))
					return false;

				switch (zoomType) {
					case ZoomType.ZoomIn:

						if (zoomInList == Translations.GetString ("Window") || zoom_level <= zoom) {
							actions.View.ZoomComboBox.ComboBox.Active = i - 1;
							return true;
						}

						break;

					case ZoomType.ZoomOut:

						if (zoomInList == Translations.GetString ("Window"))
							return true;

						if (zoom_level < zoom) {
							actions.View.ZoomComboBox.ComboBox.Active = i;
							return true;
						}

						break;
				}
				return false;
			}

			foreach (string item in actions.View.ZoomCollection) {

				if (UpdateZoomLevel (item))
					break;

				i++;
			}
		}

		actions.View.UpdateCanvasScale ();

		// Quick fix : need to manually update Upper limit because the value is not changing after updating the canvas scale.
		// TODO : I think there is an event need to be fired so that those values updated automatically.
		ViewportHost?.UpdateContentSize (ViewSize);

		// Scroll so that the canvas position under 'center_point' is still the same after zooming.
		// Note that the canvas widget might not have resized yet, so using Offset is important for taking
		// the size difference into account.
		PointD new_center_point = CanvasPointToView (canvas_point);
		if (ViewportHost is not null)
			ViewportHost.SetScrollOffset (new PointD (new_center_point.X - scroll_offset_x, new_center_point.Y - scroll_offset_y));
		else {
			Gtk.Viewport view = (Gtk.Viewport) Canvas.Parent!;
			view.Hadjustment!.Value = new_center_point.X - scroll_offset_x;
			view.Vadjustment!.Value = new_center_point.Y - scroll_offset_y;
		}

		actions.View.ResumeZoomUpdate ();
	}

	private PointD GetLegacyViewportCenter ()
	{
		Gtk.Viewport view = (Gtk.Viewport) Canvas.Parent!;
		return new PointD (view.Hadjustment!.Value + view.Hadjustment.PageSize / 2.0, view.Vadjustment!.Value + view.Vadjustment.PageSize / 2.0);
	}

	private PointD GetLegacyViewportScrollOffset ()
	{
		Gtk.Viewport view = (Gtk.Viewport) Canvas.Parent!;
		return new PointD (view.Hadjustment!.Value, view.Vadjustment!.Value);
	}

	#endregion
}
