# Rectangle Select

**Paint.NET:** [Rectangle Selection Tool](https://mail.getpaint.net/doc/latest/RectangleSelectionTool.html) · [Tools Window](https://mail.getpaint.net/doc/latest/ToolsWindow.html)

## Behavior

Click and drag from one corner to the opposite corner. `Shift` constrains the region to a square. Paint.NET supports Any Size, Fixed Ratio, and Fixed Size modes; dragging outside the canvas clamps the result while preserving the selected constraint.

## Shortcut and appearance

`S` cycles selection tools; `Shift+S` cycles them in the opposite direction. The toolbar exposes selection-combine mode and size/ratio controls. The button shows a rectangular marquee and the cursor is a selection cursor.

## Pintora implementation

Core: `src/core/Pinta.Tools/Tools/RectangleSelectTool.cs`; GTK builds the neutral selection toolbar through `IToolUiService`. Avalonia currently exposes one `SelectionTool` (`Canvas/SelectionTool.cs`) rather than a separate Rectangle Select button and does not expose all three Paint.NET sizing modes.

## Improvements

Expose Rectangle Select and its three sizing modes in Avalonia, including px/in/cm units. Test square constraint, negative drag direction, canvas clamping, and all selection-combine modes.

