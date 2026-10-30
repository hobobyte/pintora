# Ellipse Select

**Paint.NET:** [Ellipse Selection Tool](https://mail.getpaint.net/doc/latest/EllipseSelectionTool.html) · [Tools Window](https://mail.getpaint.net/doc/latest/ToolsWindow.html)

## Behavior

Drag a bounding rectangle for an ellipse. Holding `Shift` forces equal width and height, producing a circle. The resulting outline is applied through the active selection-combine mode and never changes pixels.

## Shortcut and appearance

`S` cycles selection tools; `Shift+S` cycles backward. The button is an ellipse icon and the toolbar exposes selection mode.

## Pintora implementation

`EllipseSelectTool.cs` calls `DocumentSelection.CreateEllipseSelection`. GTK uses the full Core tool; Avalonia's combined `SelectionTool` has no separately documented ellipse workflow.

## Improvements

Expose Ellipse Select in Avalonia, preserve `Shift` for all drag directions, and test circles, zero-sized drags, and canvas boundaries.

