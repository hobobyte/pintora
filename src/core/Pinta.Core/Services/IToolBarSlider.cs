namespace Pinta.Core;

/// <summary>Neutral toolbar slider widget implemented by each platform.</summary>
public interface IToolBarSlider
{
	/// <summary>Current value of the slider.</summary>
	double Value { get; }

	/// <summary>Native platform widget to append to a toolbar surface.</summary>
	object NativeWidget { get; }
}
