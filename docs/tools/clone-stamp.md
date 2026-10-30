# Clone Stamp

**Paint.NET:** [Clone Stamp Tool](https://mail.getpaint.net/doc/latest/CloneStampTool.html) · [Tools Window](https://mail.getpaint.net/doc/latest/ToolsWindow.html)

## Behavior

`Ctrl` + left click sets the source origin. Subsequent left drags paint pixels at the same relative offset; the source may be in the same or another layer. Brush size and hardness shape the stamp.

## Shortcut and appearance

`L` activates Clone Stamp. The shaped cursor shows brush size and source state; a source nub/offset is visible on the canvas. Brush controls remain available in the toolbar.

## Pintora implementation

`CloneStampTool.cs` implements Ctrl-origin, `BrushHandle`, offset tracking, and history. Avalonia has no separate Clone Stamp descriptor in `CanvasToolFactory`.

## Improvements

Add an Avalonia controller on the Core engine, define cross-layer sampling policy, and test source outside canvas, transparency, fast drag, and cancellation.

