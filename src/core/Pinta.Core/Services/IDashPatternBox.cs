using System;

namespace Pinta.Core;

/// <summary>Neutral dash-pattern toolbar option (label + editable preset list) implemented by each platform.</summary>
public interface IDashPatternBox
{
	/// <summary>Current dash pattern text, e.g. " --".</summary>
	string Pattern { get; set; }
	event EventHandler? PatternChanged;
	/// <summary>Native widget containing the separator, label and pattern entry.</summary>
	object NativeWidget { get; }
	bool Visible { get; set; }
}
