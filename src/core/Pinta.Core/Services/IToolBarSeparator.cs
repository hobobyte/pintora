namespace Pinta.Core;

/// <summary>Neutral toolbar separator widget implemented by each platform.</summary>
public interface IToolBarSeparator
{
	/// <summary>Native platform widget to append to a toolbar surface.</summary>
	object NativeWidget { get; }

	bool Visible { get; set; }
}
