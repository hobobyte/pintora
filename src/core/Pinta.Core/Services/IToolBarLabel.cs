namespace Pinta.Core;

/// <summary>Neutral toolbar label widget implemented by each platform.</summary>
public interface IToolBarLabel
{
	/// <summary>Label text.</summary>
	string Text { get; }

	/// <summary>Native platform widget to append to a toolbar surface.</summary>
	object NativeWidget { get; }

	bool Visible { get; set; }
}
