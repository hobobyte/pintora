namespace Pinta.Core;

public interface IToolbarSurface : IToolbarContainer
{
	object NativeToolbar { get; }

	/// <summary>Insert a widget into the toolbar directly after another widget.</summary>
	void InsertAfter (object widget, object afterWidget);

	/// <summary>Remove a widget previously appended to the toolbar.</summary>
	void Remove (object widget);
}
