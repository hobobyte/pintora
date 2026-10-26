using System.Reflection;

namespace Pinta.Resources;

public static class EmbeddedResourceReader
{
	public static byte[]? Read (Assembly assembly, string name)
	{
		using Stream? stream = assembly.GetManifestResourceStream (name);
		if (stream is null)
			return null;

		byte[] buffer = new byte[stream.Length];
		stream.ReadExactly (buffer);
		return buffer;
	}
}
