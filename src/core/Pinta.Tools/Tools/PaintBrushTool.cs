//
// PaintBrushTool.cs
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
using System.Linq;
using Cairo;
using Pinta.Core;

namespace Pinta.Tools;

public sealed class PaintBrushTool : BaseBrushTool
{
	private readonly IPaintBrushService brushes;
	private readonly IWorkspaceService workspace;
	private readonly IToolOptionViewFactory option_view_factory;

	private BasePaintBrush? default_brush;
	private BasePaintBrush? active_brush;
	private PointI? last_point = PointI.Zero;
	private RectangleI? stroke_dirty_rect;
	private IDisposable? open_repeating_draw_timer;
	private IToolbarContainer brush_specific_options_box;

	public PaintBrushTool (IServiceProvider services) : base (services)
	{
		brushes = services.GetService<IPaintBrushService> ();
		option_view_factory = services.GetService<IToolOptionViewFactory> ();
		foreach (BasePaintBrush brush in brushes) {
			brush.OnCursorChanged += () => SetCursorFromBrush (brush);
		}

		default_brush = brushes.FirstOrDefault ();
		active_brush = default_brush;

		brushes.BrushAdded += (_, a) => {
			RebuildBrushComboBox ();
			a.Brush.OnCursorChanged += () => SetCursorFromBrush (a.Brush);
		};
		brushes.BrushRemoved += (_, _) => RebuildBrushComboBox ();

		brush_specific_options_box = WidgetFactory.CreateBox (10, 10);

		workspace = services.GetService<IWorkspaceService> ();

		// Update cursor on zoom
		workspace.ViewSizeChanged += (_, _) => {
			if (IsActiveTool ()) {
				if (active_brush is null) {
					SetCursor (DefaultCursor);
				} else {
					SetCursorFromBrush (active_brush);
				}
			}
		};
	}

	public override string Name => Translations.GetString ("Paintbrush");
	public override string Icon => Pinta.Resources.Icons.ToolPaintBrush;
	public override string StatusBarText => Translations.GetString ("Left click to draw with primary color, right click to draw with secondary color.");
	public override StandardKey? ShortcutKeyStandard => StandardKey.B;
	public override int Priority => 21;

	public override ShapedCursorDescriptor? DefaultShapedCursor {
		get {
			double scale = workspace.GetScale ();
			return new ShapedCursorDescriptor (
				new CursorDescriptor ("Cursor.Paintbrush.png", 8, 24),
				CursorShape.Ellipse,
				scale,
				BrushWidth);
		}
	}


	protected override void OnBuildToolBar (IToolbarSurface tb)
	{
		base.OnBuildToolBar (tb);

		tb.Append (Separator.NativeWidget);

		tb.Append (BrushLabel.NativeWidget);
		tb.Append (BrushComboBox);

		RebuildBrushSpecificOptions ();
		tb.Append (brush_specific_options_box.NativeWidget);
		if (active_brush is not null) {
			active_brush.UpdateLineWidth (BrushWidth);
		}
	}

	protected override void OnMouseDown (Document document, ToolMouseEventArgs e)
	{
		document.Layers.ToolLayer.Clear ();
		document.Layers.ToolLayer.Hidden = false;

		base.OnMouseDown (document, e);

		active_brush?.DoMouseDown ();
	}

	protected override void OnMouseMove (Document document, ToolMouseEventArgs e)
	{
		if (active_brush is null)
			return;

		if (mouse_button is not (MouseButton.Left or MouseButton.Right)) {
			last_point = null;
			return;
		}

		// TODO: also multiply color by pressure
		Color strokeColor = mouse_button switch {
			MouseButton.Right => new (
				Palette.SecondaryColor.R,
				Palette.SecondaryColor.G,
				Palette.SecondaryColor.B,
				Palette.SecondaryColor.A * active_brush.StrokeAlphaMultiplier
			),
			MouseButton.Left or _ => new (
				Palette.PrimaryColor.R,
				Palette.PrimaryColor.G,
				Palette.PrimaryColor.B,
				Palette.PrimaryColor.A * active_brush.StrokeAlphaMultiplier
			)
		};

		if (!last_point.HasValue)
			last_point = e.Point;

		var surf = document.Layers.ToolLayer.CairoSurface;
		using Context g = document.CreateClippedToolContext ();

		g.Antialias = UseAntialiasing ? Antialias.Subpixel : Antialias.None;
		g.LineWidth = BrushWidth;
		g.LineJoin = LineJoin.Round;
		g.LineCap = LineCap.Round;
		g.SetSourceColor (strokeColor);

		BrushStrokeArgs strokeArgs = new (strokeColor, e.Point, last_point.Value);

		CancelRepeatingDraw ();
		var invalidate_rect = active_brush.DoMouseMove (g, surf, strokeArgs);
		if (stroke_dirty_rect is null)
			stroke_dirty_rect = invalidate_rect;
		else
			stroke_dirty_rect = stroke_dirty_rect.Value.Union (invalidate_rect);

		if (!document.ClampToImageSize (invalidate_rect).IsEmpty)
			surface_modified = true;

		// If we draw partially offscreen, Cairo gives us a bogus
		// dirty rectangle, so redraw everything.
		if (document.Workspace.IsPartiallyOffscreen (invalidate_rect))
			document.Workspace.Invalidate ();
		else
			document.Workspace.Invalidate (document.ClampToImageSize (invalidate_rect));

		if (active_brush.MillisecondsBeforeReapply != 0) {
			IUiScheduler uiScheduler = PintaCore.Services.GetService<IUiScheduler> ()
				?? throw new InvalidOperationException ("No IUiScheduler service is registered.");
			open_repeating_draw_timer = uiScheduler.StartTimer (
				TimeSpan.FromMilliseconds (active_brush.MillisecondsBeforeReapply),
				() => {
					OnMouseMove (document, e);
					return true;
				});
		}
		last_point = e.Point;
	}

	protected override void OnMouseUp (Document document, ToolMouseEventArgs e)
	{
		CancelRepeatingDraw ();
		using Context g = new (document.Layers.CurrentUserLayer.CairoSurface);

		document.Layers.ToolLayer.Draw (g);

		document.Layers.ToolLayer.Hidden = true;

		base.OnMouseUp (document, e);

		active_brush?.DoMouseUp ();

		if (stroke_dirty_rect != null) {
			if (document.Workspace.IsPartiallyOffscreen (stroke_dirty_rect.Value))
				document.Workspace.Invalidate ();
			else
				document.Workspace.Invalidate (document.ClampToImageSize (stroke_dirty_rect.Value));

			stroke_dirty_rect = null;
		}
	}

	protected override void OnSaveSettings (ISettingsService settings)
	{
		base.OnSaveSettings (settings);

		if (brush_combo_box is not null)
			settings.PutSetting (SettingNames.PAINT_BRUSH_BRUSH, brush_combo_box.Active);

		if (active_brush is not null) {
			foreach (var option in active_brush.Options) {
				option.SaveValueToSettings (settings);
			}
		}
	}

	protected override void OnBrushWidthChanged ()
	{
		if (active_brush is null) {
			base.OnBrushWidthChanged ();
		} else {
			active_brush.UpdateLineWidth (BrushWidth);
		}
	}

	private IToolBarLabel? brush_label;
	private IToolBarComboBox? brush_combo_box;
	private IToolBarSeparator? separator;

	private IToolBarSeparator Separator => separator ??= WidgetFactory.CreateSeparator ();
	private IToolBarLabel BrushLabel => brush_label ??= WidgetFactory.CreateLabel (string.Format (" {0}:  ", Translations.GetString ("Type")));


	private IToolBarComboBox BrushComboBox {
		get {
			if (brush_combo_box is null) {
				brush_combo_box = WidgetFactory.CreateComboBox (100, 0, false);
				brush_combo_box.Changed += (o, e) => {
					var brush_name = brush_combo_box.ActiveText;
					active_brush = brushes.SingleOrDefault (brush => brush.Name == brush_name) ?? default_brush;
					if (active_brush is not null) {
						active_brush.UpdateLineWidth (BrushWidth);
					}
				};

				RebuildBrushComboBox ();

				var brush = Settings.GetSetting (SettingNames.PAINT_BRUSH_BRUSH, 0);

				if (brush < brush_combo_box.ItemCount)
					brush_combo_box.Active = brush;
			}

			return brush_combo_box;
		}
	}

	/// <summary>
	/// Rebuild the list of brushes.
	/// </summary>
	private void RebuildBrushComboBox ()
	{
		default_brush = brushes.FirstOrDefault ();

		BrushComboBox.RemoveAllItems ();

		foreach (var brush in brushes)
			BrushComboBox.AppendItem (brush.Name);

		BrushComboBox.Active = 0;
		BrushComboBox.Changed += (cbx, ev) => RebuildBrushSpecificOptions ();
	}

	private void CancelRepeatingDraw ()
	{
		open_repeating_draw_timer?.Dispose ();
		open_repeating_draw_timer = null;
	}

	private void RebuildBrushSpecificOptions ()
	{
		brush_specific_options_box.RemoveAll ();
		if (active_brush is not null) {
			foreach (var option in active_brush.Options) {
				brush_specific_options_box.Append (option_view_factory.CreateView (option));
			}
		}
	}

	private void SetCursorFromBrush (BasePaintBrush brush)
	{
		if (brush.GetCursorNative () is { } brush_cursor) {
			SetCursor (brush_cursor);
		} else {
			SetCursor (DefaultCursor);
		}
	}
}
