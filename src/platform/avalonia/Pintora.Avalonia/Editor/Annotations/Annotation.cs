using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Pinta.Core;
using Pintora.Avalonia.Canvas;

namespace Pintora.Avalonia.Editor.Annotations;

/// <summary>Persistent, non-raster editor content that can be edited before commit.</summary>
public abstract record Annotation
{
	public required Rect Bounds { get; init; }
	public double Rotation { get; init; }
}

public sealed record ShapeAnnotation : Annotation
{
	public required CanvasHost.ShapeKind Kind { get; init; }
	public required PointD From { get; init; }
	public required PointD To { get; init; }
	public required ColorBgra Outline { get; init; }
	public required ColorBgra Fill { get; init; }
	public CanvasHost.ShapeFillStyle FillStyle { get; init; }
	public int OutlineWidth { get; init; }
	public CanvasHost.ShapeDashStyle Dash { get; init; }
	public int CornerRadius { get; init; }
	public bool Antialias { get; init; }
}

public sealed record TextAnnotation : Annotation
{
	public required string Text { get; init; }
	public string FontFamily { get; init; } = "Inter";
	public double FontSize { get; init; } = 16;
	public FontWeight FontWeight { get; init; } = FontWeight.Normal;
	public ColorBgra Color { get; init; } = ColorBgra.Black;
	public bool Underline { get; init; }
}

public enum RegionEffectKind
{
	Blur,
	Pixelate,
	Highlight,
	Magnify,
	Spotlight
}

public sealed record RegionEffectAnnotation : Annotation
{
	public required RegionEffectKind Effect { get; init; }
	public double Strength { get; init; } = 1;
	public int PixelSize { get; init; } = 8;
	public ColorBgra HighlightColor { get; init; } = ColorBgra.Yellow;
	public bool PreserveAlpha { get; init; } = true;
}

public sealed record ImageAnnotation : Annotation
{
	public required string SourceId { get; init; }
	public double Opacity { get; init; } = 1;
	public bool PreserveAspectRatio { get; init; } = true;
}

public sealed record CursorAnnotation : Annotation
{
	public required string CursorId { get; init; }
	public double Scale { get; init; } = 1;
	public ColorBgra Tint { get; init; } = ColorBgra.White;
	public int HotspotX { get; init; }
	public int HotspotY { get; init; }
}

public enum SelectionAnnotationKind { Rectangle, Ellipse, Freeform, Polygon }

public sealed record SelectionAnnotation : Annotation
{
	public required SelectionAnnotationKind Kind { get; init; }
	public Pinta.Core.SelectionCombineMode CombineMode { get; init; } = Pinta.Core.SelectionCombineMode.Replace;
	public double FeatherRadius { get; init; }
	public IReadOnlyList<Point> Points { get; init; } = Array.Empty<Point> ();
}
