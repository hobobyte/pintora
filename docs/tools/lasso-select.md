# Lasso Select

**Paint.NET:** [Lasso Selection Tool](https://mail.getpaint.net/doc/latest/LassoSelectionTool.html) · [Tools Window](https://mail.getpaint.net/doc/latest/ToolsWindow.html)

## Behavior

In freeform mode the outline follows the pointer while dragging and closes with a straight segment back to the start. Selection modes (Replace, Add, Subtract, Intersect, Exclude) define how the new polygon combines with the existing selection.

## Shortcut and appearance

`S` cycles selection tools and `Shift+S` cycles backward. The toolbar should expose selection mode and Freeform/Polygon mode. The button is a lasso icon; the canvas shows a live marquee.

## Pintora implementation

`src/core/Pinta.Tools/Tools/LassoSelectTool.cs` implements the polygon, history, `Enter` commit, and `Backspace` last-point removal. GTK has the selection toolbar. Avalonia's general `SelectionTool` does not yet expose this workflow.

## Improvements

Add Polygon mode, last-point undo, Enter/Escape handling, and consistent self-intersection rules to Avalonia. Test cancellation and partially completed paths.

