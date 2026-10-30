# Paintbrush

**Paint.NET:** [Paintbrush Tool](https://mail.getpaint.net/doc/latest/PaintbrushTool.html) · [Tools Window](https://mail.getpaint.net/doc/latest/ToolsWindow.html)

## Behavior

Draws a continuous colored stroke. Left uses Primary and right uses Secondary. Brush size, hardness, shape, spacing, smoothing, antialiasing, and pressure (when available) affect the stroke. `[` and `]` change size. One drag should produce one undo item.

## Shortcut and appearance

`B` activates Paintbrush. The cursor reflects the brush diameter. The toolbar contains size, hardness, preset, spacing, and brush-specific controls.

## Pintora implementation

`PaintBrushTool.cs` and `BaseBrushTool.cs` use `IPaintBrushService`, shaped cursors, and neutral `IToolbarSurface`; GTK supplies control adapters. Avalonia `Canvas/BrushTool.cs` handles basic brush/opacity but does not yet expose the complete preset service.

## Improvements

Connect Avalonia to the Core brush engine, keep presets per tool, and test DPI-scaled cursors, fast drags, spacing, and pressure fallback.

