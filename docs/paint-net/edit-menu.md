# Edit Menu

**Paint.NET:** [Edit Menu](https://mail.getpaint.net/doc/latest/EditMenu.html)

![Paint.NET Edit Menu](https://mail.getpaint.net/doc/latest/images/menus/edit/editmenu.png)

## Commands and shortcuts

`Ctrl+Z` undoes and `Ctrl+Y` redoes the active document's history. `Ctrl+X` cuts the active selection to the clipboard and clears it; `Ctrl+C` copies the selection or whole active layer; `Ctrl+Shift+C` copies the composited image (or selected region). `Ctrl+V` pastes into the active layer, selects the pasted pixels, and activates Move Selected Pixels. If the paste exceeds the canvas, Paint.NET can resize the canvas.

Selection commands include Select All (`Ctrl+A`), Deselect (`Ctrl+Shift+A`), Invert Selection (`Ctrl+I`), and selection offset/transform commands. The active document, not another open tab, owns the history operation.

## Pintora implementation

Core exposes `ActionManager`, history, clipboard contracts, selection, and `FileActions`. GTK has `PasteAction` and selection actions. Avalonia registers Undo/Redo, copy variants, paste variants, Select All/Deselect, and selection transforms in `PintoraKeyboardSettings` and `MainWindow`.

## Gaps and poor behavior to fix

Verify that Cut writes transparent pixels with the expected RGB policy, Copy Merged composites all visible layers, and Paste always creates the same movable selection. Clipboard and selection logic should be shared rather than reimplemented in `CanvasHost`; add cross-platform tests for multi-tab active-document routing.

