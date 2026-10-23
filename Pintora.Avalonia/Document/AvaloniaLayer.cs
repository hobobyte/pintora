using Pintora.Avalonia.Imaging;

namespace Pintora.Avalonia.Document;

public sealed class AvaloniaLayer
{
	public AvaloniaLayer (string name, MemoryImageSurface surface)
	{
		Name = name;
		Surface = surface;
	}

	public string Name { get; set; }
	public bool IsVisible { get; set; } = true;
	public bool IsLocked { get; set; }
	private double opacity = 1.0;
	public double Opacity {
		get => opacity;
		set => opacity = System.Math.Clamp (value, 0, 1);
	}
	public MemoryImageSurface Surface { get; }
}
