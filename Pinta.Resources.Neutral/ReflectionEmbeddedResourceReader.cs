using System.Reflection;

namespace Pinta.Resources;

public sealed class ReflectionEmbeddedResourceReader : IEmbeddedResourceReader
{
	public byte[]? Read (Assembly assembly, string name)
		=> EmbeddedResourceReader.Read (assembly, name);
}
