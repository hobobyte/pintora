# Move Selection

**Paint.NET:** [Move Tools](https://mail.getpaint.net/doc/latest/MoveTools.html) · [Tools Window](https://mail.getpaint.net/doc/latest/ToolsWindow.html)

## Behavior

Moves, scales, or rotates only the selection outline; image pixels remain unchanged. `Ctrl` copies the selection outline. Arrow keys move it by 1 px (10 px with `Ctrl`), and `Shift` constrains rotation to 15° increments.

## Shortcut and appearance

`M` twice or `Shift+M` selects Move Selection. A blue highlight, control nubs, and a rotation center distinguish it from Move Selected Pixels.

## Pintora implementation

`src/core/Pinta.Tools/Tools/MoveSelectionTool.cs` transforms `DocumentSelection` and `PreviousSelection` without creating a selection layer. Avalonia currently has no separate entry.

## Improvements

Expose the tool in Avalonia and add a regression test proving that moving the outline leaves layer pixels byte-for-byte unchanged.

