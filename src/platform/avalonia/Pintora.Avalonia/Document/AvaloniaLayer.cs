using Pintora.Avalonia.Imaging;
using Pintora.Avalonia.Editor.Annotations;
using System;

namespace Pintora.Avalonia.Document;

public sealed class AvaloniaLayer
{
	private string name;
	private bool is_visible = true;
	private bool is_locked;
	private double opacity = 1.0;
	public string Name { get => name; set { if (name == value) return; name = value; NotifyMetadataChanged (affectsRenderedAppearance: false); } }
	public bool IsVisible { get => is_visible; set { if (is_visible == value) return; is_visible = value; NotifyMetadataChanged (affectsRenderedAppearance: true); } }
	public bool IsLocked { get => is_locked; set { if (is_locked == value) return; is_locked = value; NotifyMetadataChanged (affectsRenderedAppearance: false); } }
	public double Opacity {
		get => opacity;
		set { double next = System.Math.Clamp (value, 0, 1); if (System.Math.Abs (opacity - next) < double.Epsilon) return; opacity = next; NotifyMetadataChanged (affectsRenderedAppearance: true); }
	}
	public MemoryImageSurface Surface { get; }
	public AnnotationCollection Annotations { get; } = new ();
	public event EventHandler? Changed;
	public event EventHandler? MetadataChanged;

	private void OnAnnotationsChanged (object? sender, EventArgs e)
	{
		Changed?.Invoke (this, e);
	}

	private void NotifyMetadataChanged (bool affectsRenderedAppearance)
	{
		MetadataChanged?.Invoke (this, new LayerMetadataChangedEventArgs (affectsRenderedAppearance));
	}

	public AvaloniaLayer (string name, MemoryImageSurface surface)
	{
		this.name = name;
		Surface = surface;
		Annotations.Changed += OnAnnotationsChanged;
	}
}
