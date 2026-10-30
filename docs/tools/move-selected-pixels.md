# Move Selected Pixels

**Paint.NET:** [Move Tools](https://mail.getpaint.net/doc/latest/MoveTools.html) · [Tools Window](https://mail.getpaint.net/doc/latest/ToolsWindow.html)

## Behavior

Moves, scales, and rotates pixels in the active layer or selection. With no selection, the whole active layer is transformed. Corner and edge nubs resize; the outside rotation corridor rotates around the pivot; `Shift` constrains rotation to 15° increments. `Ctrl` moves a copy. The operation commits as one undo item and leaves transparency where pixels were moved from.

## Shortcut and appearance

`M` once selects Move Selected Pixels. The button shows four-way arrows; the cursor and control nubs indicate move, resize, and rotate states. Arrow keys move by 1 px; `Ctrl` increases the step to 10 px.

## Pintora implementation

`MoveSelectedTool.cs` creates a selection layer when needed, snapshots it, and applies a matrix transform through `BaseTransformTool`. Avalonia has no separate move entry in `CanvasToolFactory`.

## Improvements

Add the Core controller to Avalonia and test transparent edges, locked layers, copy mode, rotation pivot, and undo/redo.

