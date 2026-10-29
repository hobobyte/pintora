namespace Pinta.Core;

/// <summary>
/// A neutral container widget that can hold other native toolbar widgets,
/// e.g. a group of brush-specific option views.
/// </summary>
public interface IToolbarContainer
{
	/// <summary>Native platform widget to append to a toolbar surface.</summary>
	object NativeWidget { get; }

	void Append (object widget);

	void RemoveAll ();
}
