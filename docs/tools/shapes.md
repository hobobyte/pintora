# Shapes

**Paint.NET:** [Shapes Tool](https://mail.getpaint.net/doc/latest/ShapesTool.html) · [Tools Window](https://mail.getpaint.net/doc/latest/ToolsWindow.html)

## Behavior

Drag to create a predefined shape. The toolbar chooses shape, fill/outline, width, dash, corner radius, and antialiasing. Preview remains editable until commit; handles resize and rotate; undo restores the previous raster/annotation state.

## Shortcut and appearance

`O` cycles shape tools in Core (including Freeform Shape). The button shows the active shape and the toolbar shows the shape picker, fill mode, width, dash, radius, and edit handles.

## Pintora implementation

Core uses `ShapeTool.cs`, `RectangleTool.cs`, `EllipseTool.cs`, `RoundedRectangleTool.cs`, and `FreeformShapeTool.cs`. Avalonia `ShapeTool.cs` and `ShapeRasterizer.cs` currently support Line, Rectangle, Ellipse, Rounded Rectangle, fill, width, dash, radius, and antialiasing.

## Improvements

Avalonia still lacks the full Core figure set and Freeform/curve workflow. Move raster math and edit handles into Core first, then add shapes and golden-image tests for live parameter changes.

