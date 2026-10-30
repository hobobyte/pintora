# History Window

**Paint.NET:** [History Window](https://mail.getpaint.net/doc/latest/HistoryWindow.html)

![Paint.NET History Window](https://mail.getpaint.net/doc/latest/images/historywindow/historywindow.png)

## Behavior and shortcuts

Every action in the current document session appears in the list, including selection edits. `F6` toggles the window; `Ctrl+Shift+F6` resets its dock position. Undo/Redo are `Ctrl+Z`/`Ctrl+Y`, and clicking a history entry rewinds the document to that point. Undone entries are grey. Starting a new action while viewing an older entry discards the remaining redo branch. Closing the document clears its history permanently.

## Pintora implementation

Core has `DocumentHistory` and history item classes for pixels, layers, selections, and tools. GTK has a History pad. Avalonia maintains `EditorHistory` plus a separate layer snapshot history in `CanvasHost`.

## Gaps and poor behavior to fix

Two history systems in Avalonia can diverge from the Core document history. Define one document-level history contract, expose F6/reset behavior, and test branch truncation, tab isolation, mixed pixel/layer operations, and closing a document.

