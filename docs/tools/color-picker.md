# Color Picker

**Paint.NET:** [Color Picker Tool](https://mail.getpaint.net/doc/latest/ColorPickerTool.html) · [Tools Window](https://mail.getpaint.net/doc/latest/ToolsWindow.html)

## Behavior

Click to sample a color from the active layer: left assigns Primary and right assigns Secondary. Dragging updates the sample under the pointer. Sampling can use the active layer or the composited image, and the tool can stay active or return to the previous tool.

## Shortcut and appearance

`K` activates Color Picker. The cursor is an eyedropper; the toolbar contains sampling and post-pick tool-selection controls. Primary/Secondary swatches update immediately.

## Pintora implementation

`ColorPickerTool.cs` implements drag sampling, both color slots, sampling mode, and return-to-previous behavior. Avalonia has `Canvas/ColorPickerTool.cs` and `ColorPickerSurface.cs`; Image-vs-Layer parity still needs verification.

## Improvements

Centralize sampling in Core, support a temporary eyedropper modifier from every tool, and test zoom-aware coordinates, transparent composites, and post-pick selection.

