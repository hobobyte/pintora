namespace Pinta.Core;

/// <summary>Configuration supplied by a platform composition root before PintaCore is first used.</summary>
public static class PintaCoreConfiguration
{
	public static IImageFormatProvider? ImageFormatProvider { get; set; }
}
