using System.Reflection;

namespace Pinta.Resources;

/// <summary>
/// Platform-neutral lookup for embedded resource bytes.
/// Rendering those bytes as an icon remains the responsibility of a platform adapter.
/// </summary>
public static class EmbeddedResourceLocator
{
	public static byte[]? Find (string name, Assembly? preferredAssembly = null)
	{
		if (preferredAssembly is not null) {
			byte[]? result = EmbeddedResourceReader.Read (preferredAssembly, name);
			if (result is not null)
				return result;
		}

		foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies ()) {
			byte[]? result = EmbeddedResourceReader.Read (assembly, name);
			if (result is not null)
				return result;
		}

		return null;
	}
}
