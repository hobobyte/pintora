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
	public double Opacity { get; set; } = 1.0;
	public MemoryImageSurface Surface { get; }
}
