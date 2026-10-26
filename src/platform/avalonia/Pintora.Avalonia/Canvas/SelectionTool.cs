using System;
using Avalonia;
using Pinta.Core;
using Pintora.Avalonia.Document;
using Pintora.Avalonia.Editor.Annotations;

namespace Pintora.Avalonia.Canvas;

internal sealed class SelectionTool : ICanvasTool
{
	public CanvasHost.CanvasTool Kind => CanvasHost.CanvasTool.Selection;
	private ColorBgra[]? clipboard;
	private Pinta.Core.Size clipboardSize;
	private PointD? dragStart;
	private PointD? dragEnd;
	public RectangleI? Selection { get; private set; }
	public SelectionAnnotation? Annotation { get; private set; }
	public bool IsSelecting => dragStart is not null;

	public bool PointerPressed (CanvasToolPointerContext context)
	{
		BeginSelection (context.Point);
		return true;
	}

	public bool PointerMoved (CanvasToolPointerContext context)
	{
		if (!IsSelecting)
			return false;
		UpdateSelection (context.Point);
		context.Host.InvalidateToolVisual ();
		return true;
	}

	public bool PointerReleased (CanvasToolPointerContext context)
	{
		if (!IsSelecting)
			return false;
		CompleteSelection (context.Host.ImageSize);
		context.Host.InvalidateToolVisual ();
		return true;
	}

	public void CancelPointerAction () => CancelSelection ();

	public void BeginSelection (PointD point)
	{
		dragStart = dragEnd = point;
	}

	public void UpdateSelection (PointD point)
	{
		if (IsSelecting)
			dragEnd = point;
	}

	public bool CompleteSelection (Pinta.Core.Size imageSize)
	{
		if (dragStart is not PointD start || dragEnd is not PointD end)
			return false;
		dragStart = dragEnd = null;
		// Pointer coordinates describe the edges of the dragged rectangle in
		// image space. Convert those edges to pixel bounds instead of rounding
		// each endpoint independently; rounding makes the selection jump at
		// fractional zoom levels and can drop the last row/column.
		double left = Math.Min (start.X, end.X);
		double top = Math.Min (start.Y, end.Y);
		double right = Math.Max (start.X, end.X);
		double bottom = Math.Max (start.Y, end.Y);
		return SelectRectangle (
			new PointI ((int) Math.Floor (left), (int) Math.Floor (top)),
			new PointI ((int) Math.Ceiling (right), (int) Math.Ceiling (bottom)), imageSize);
	}

	public void CancelSelection () => dragStart = dragEnd = null;

	public bool SelectRectangle (PointI start, PointI end, Pinta.Core.Size imageSize)
	{
		RectangleI requested = RectangleI.FromPoints (start, end);
		int left = Math.Clamp (requested.X, 0, imageSize.Width);
		int top = Math.Clamp (requested.Y, 0, imageSize.Height);
		int right = Math.Clamp (requested.X + requested.Width, 0, imageSize.Width);
		int bottom = Math.Clamp (requested.Y + requested.Height, 0, imageSize.Height);
		if (right <= left || bottom <= top) {
			Selection = null;
			Annotation = null;
			return false;
		}
		Selection = new RectangleI (left, top, right - left, bottom - top);
		Annotation = new SelectionAnnotation {
			Bounds = new Rect (left, top, right - left, bottom - top),
			Kind = SelectionAnnotationKind.Rectangle,
			CombineMode = SelectionCombineMode.Replace
		};
		return true;
	}

	public void Clear () { Selection = null; Annotation = null; }

	public bool Copy (IImageSurface? surface)
	{
		if (surface is null || Selection is not RectangleI area)
			return false;
		clipboardSize = new Pinta.Core.Size (area.Width, area.Height);
		clipboard = new ColorBgra[area.Width * area.Height];
		ReadOnlySpan<ColorBgra> source = surface.GetReadOnlyPixelData ();
		for (int y = 0; y < area.Height; y++)
			source.Slice ((area.Y + y) * surface.Width + area.X, area.Width).CopyTo (clipboard.AsSpan (y * area.Width, area.Width));
		return true;
	}

	public bool Paste (PointI destination, Func<string, AvaloniaLayer> createLayer)
	{
		if (clipboard is null || clipboardSize.Width <= 0 || clipboardSize.Height <= 0)
			return false;
		AvaloniaLayer layer = createLayer ($"Pasted Layer");
		Span<ColorBgra> target = layer.Surface.GetPixelData ();
		for (int y = 0; y < clipboardSize.Height; y++) {
			int targetY = destination.Y + y;
			if ((uint) targetY >= (uint) layer.Surface.Height) continue;
			for (int x = 0; x < clipboardSize.Width; x++) {
				int targetX = destination.X + x;
				if ((uint) targetX < (uint) layer.Surface.Width)
					target[targetY * layer.Surface.Width + targetX] = clipboard[y * clipboardSize.Width + x];
			}
		}
		return true;
	}

	public bool Move (IImageSurface? surface, PointI delta, Action beginStroke, Action commitStroke)
	{
		if (surface is null || Selection is not RectangleI area || (delta.X == 0 && delta.Y == 0) || !Copy (surface))
			return false;
		PointI destination = new (area.X + delta.X, area.Y + delta.Y);
		var clipped = new RectangleI (Math.Max (0, destination.X), Math.Max (0, destination.Y),
			Math.Min (area.Width, surface.Width - Math.Max (0, destination.X)),
			Math.Min (area.Height, surface.Height - Math.Max (0, destination.Y)));
		if (clipped.Width <= 0 || clipped.Height <= 0)
			return false;
		beginStroke ();
		Span<ColorBgra> pixels = surface.GetPixelData ();
		for (int y = 0; y < area.Height; y++)
			pixels.Slice ((area.Y + y) * surface.Width + area.X, area.Width).Fill (ColorBgra.Zero);
		for (int y = 0; y < clipped.Height; y++)
			for (int x = 0; x < clipped.Width; x++)
				pixels[(clipped.Y + y) * surface.Width + clipped.X + x] = clipboard![y * area.Width + (clipped.X - destination.X) + x];
		Selection = clipped;
		if (Annotation is not null)
			Annotation = Annotation with { Bounds = new Rect (clipped.X, clipped.Y, clipped.Width, clipped.Height) };
		commitStroke ();
		return true;
	}

	public bool FlipHorizontal (IImageSurface? surface, Action beginStroke, Action commitStroke)
	{
		if (surface is null || Selection is not RectangleI area || area.Width < 2) return false;
		beginStroke ();
		Span<ColorBgra> pixels = surface.GetPixelData ();
		for (int y = 0; y < area.Height; y++)
			for (int x = 0; x < area.Width / 2; x++) {
				int left = (area.Y + y) * surface.Width + area.X + x;
				int right = (area.Y + y) * surface.Width + area.X + area.Width - x - 1;
				(pixels[left], pixels[right]) = (pixels[right], pixels[left]);
			}
		commitStroke ();
		return true;
	}

	public bool FlipVertical (IImageSurface? surface, Action beginStroke, Action commitStroke)
	{
		if (surface is null || Selection is not RectangleI area || area.Height < 2) return false;
		beginStroke ();
		Span<ColorBgra> pixels = surface.GetPixelData ();
		for (int y = 0; y < area.Height / 2; y++)
			for (int x = 0; x < area.Width; x++) {
				int top = (area.Y + y) * surface.Width + area.X + x;
				int bottom = (area.Y + area.Height - y - 1) * surface.Width + area.X + x;
				(pixels[top], pixels[bottom]) = (pixels[bottom], pixels[top]);
			}
		commitStroke ();
		return true;
	}

	public bool RotateClockwise (IImageSurface? surface, Action beginStroke, Action commitStroke)
	{
		if (surface is null || Selection is not RectangleI area || area.Width != area.Height || area.Width < 2) return false;
		int size = area.Width;
		ColorBgra[] source = new ColorBgra[size * size];
		for (int y = 0; y < size; y++)
			for (int x = 0; x < size; x++)
				source[y * size + x] = surface.GetReadOnlyPixelData ()[(area.Y + y) * surface.Width + area.X + x];
		beginStroke ();
		Span<ColorBgra> pixels = surface.GetPixelData ();
		for (int y = 0; y < size; y++)
			for (int x = 0; x < size; x++)
				pixels[(area.Y + y) * surface.Width + area.X + x] = source[(size - 1 - x) * size + y];
		commitStroke ();
		return true;
	}
}
