# Line/Curve

**Paint.NET:** [Line/Curve Tool](https://mail.getpaint.net/doc/latest/LineCurveTool.html) · [Tools Window](https://mail.getpaint.net/doc/latest/ToolsWindow.html)

## Behavior

The first drag creates a straight line; control points then turn it into a curve. Preview remains editable until commit. `Escape` cancels and undo stores one completed stroke. Width, cap/join, dash, and antialiasing affect raster output.

## Shortcut and appearance

The button shows a line/curve icon. The current Core tool does not declare a `StandardKey`; the tooltip should still expose any platform shortcut. Toolbar controls should show width, cap/join, dash, and curve mode.

## Pintora implementation

`LineCurveTool.cs` uses the Core `LineCurveEditEngine`. Avalonia currently exposes Line as a `Shapes` variant and has no separate curve editor; GTK uses the full Core tool.

## Improvements

Expose Line/Curve in Avalonia, reuse the edit engine, add keyboard control-point navigation, and test caps, dashes, cancellation, and undo.

