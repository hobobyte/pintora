# Pan

**Paint.NET:** [View Tools](https://mail.getpaint.net/doc/latest/ViewTools.html) · [Tools Window](https://mail.getpaint.net/doc/latest/ToolsWindow.html)

## Behavior

Click and drag the canvas to scroll the view without changing pixels. Paint.NET also permits Space + drag from every other tool; mouse-wheel scrolling and keyboard arrows are available while Space is held.

## Shortcut and appearance

`H` activates Pan; holding `Space` is temporary activation. The button is a hand and the cursor changes between open/closed hand states.

## Pintora implementation

`PanTool.cs` changes the neutral viewport. Avalonia uses `CanvasCoordinateMapper`; GTK uses `GtkDocumentViewportHost`. Verify that Space does not conflict with TextTool or IME composition.

## Improvements

Share pan/zoom coordinate math in a Core viewport service and test temporary Space-pan from brush, text, and shape tools.

