namespace Pinta.Core;

/// <summary>Creates image surfaces without exposing a renderer implementation.</summary>
public interface IImageSurfaceFactory
{
	IImageSurface Create (int width, int height);
}

/// <summary>CPU fallback surface factory for headless processing and tests.</summary>
public sealed class ArrayImageSurfaceFactory : IImageSurfaceFactory
{
	public IImageSurface Create (int width, int height) => new ArrayImageSurface (width, height);
}
