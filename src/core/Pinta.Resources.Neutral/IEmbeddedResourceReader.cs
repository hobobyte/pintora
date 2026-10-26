using System.Reflection;

namespace Pinta.Resources;

public interface IEmbeddedResourceReader
{
	byte[]? Read (Assembly assembly, string name);
}
