# Eraser

**Paint.NET:** [Eraser Tool](https://mail.getpaint.net/doc/latest/EraserTool.html) · [Tools Window](https://mail.getpaint.net/doc/latest/ToolsWindow.html)

## Behavior

Removes alpha from the active layer, rather than painting white. Brush size, hardness, spacing, smoothing, antialiasing, and the Primary/Secondary alpha values control the edge and amount erased. Fully erased pixels become transparent; the checkerboard is only an editor cue.

## Shortcut and appearance

`E` activates Eraser; `[` and `]` change size. The button and shaped cursor show an eraser. The toolbar inherits brush controls and adds erase type.

## Pintora implementation

`EraserTool.cs` uses `BaseBrushTool`, an alpha lookup table, and the persisted `ERASER_ERASE_TYPE` setting. Avalonia currently represents it as `BrushTool(CanvasTool.Eraser)`.

## Improvements

Share erase/alpha semantics in Core, expose erase type in Avalonia, and test partial alpha, transparent layers, locked layers, antialiasing, and undo.

