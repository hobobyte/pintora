# Gradient

**Paint.NET:** [Gradient Tool](https://mail.getpaint.net/doc/latest/GradientTool.html) · [Tools Window](https://mail.getpaint.net/doc/latest/ToolsWindow.html)

## Behavior

Drag a line from the start to the end of the gradient. Paint.NET supports seven shapes, color and transparency modes, and repeat modes. A transparent gradient edits only alpha and can blend two layers. Control nubs remain editable after the drag; right-clicking a nub swaps Primary and Secondary. Commit and undo should be one operation.

## Shortcut and appearance

`G` activates Gradient. The canvas shows a line handle and control points; the toolbar shows gradient shape, color/transparency mode, and repeat options.

## Pintora implementation

`GradientTool.cs` and `GradientRenderers/*` are in Core; GTK only adapts the controls. Avalonia `Canvas/GradientTool.cs` renders a preview, but shape count, repeat, and alpha behavior must match Core.

## Improvements

Reuse the Core renderer in Avalonia and test premultiplied alpha, reversed drag, handle editing, Enter/tool-switch commit, repeat modes, and one undo item.

