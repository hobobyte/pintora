namespace Pinta.Core;

public interface IToolbarSurface
{
	object NativeToolbar { get; }
	void Append (object widget);
	void RemoveAll ();
}
