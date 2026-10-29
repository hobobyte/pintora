using System.Collections.Generic;
using Gtk;
using Pinta.Core;

namespace Pinta.Tools;

/// <summary>GTK adapter for the legacy tool-option views.</summary>
public static class ToolOptionWidgetService
{
	private static readonly Dictionary<ToolOption, Widget> tool_option_widgets = new ();

	public static Widget GetWidgetForOption (ToolOption toolOption)
	{
		if (tool_option_widgets.TryGetValue (toolOption, out Widget? widget))
			return widget;

		Box box = Box.New (Orientation.Horizontal, 0);
		if (toolOption is IntegerOption integerOption) {
			int optionValue = integerOption.Value;
			SpinButton spinButton = GtkExtensions.CreateToolBarSpinButton (integerOption.Minimum, integerOption.Maximum, 1, optionValue);
			spinButton.OnValueChanged += (_, _) => integerOption.Value = (int) spinButton.Value;
			integerOption.OnValueChanged += value => spinButton.Value = value;
			box.Append (Label.New ($"{integerOption.LabelText}: "));
			box.Append (spinButton);
			integerOption.Value = optionValue;
		}

		tool_option_widgets.Add (toolOption, box);
		return box;
	}
}
