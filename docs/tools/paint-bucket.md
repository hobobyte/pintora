# Paint Bucket

**Paint.NET:** [Paint Bucket Tool](https://mail.getpaint.net/doc/latest/PaintBucketTool.html) · [Tools Window](https://mail.getpaint.net/doc/latest/ToolsWindow.html)

## Behavior

Fills a similar-color region with Primary (left button) or Secondary (right button). Contiguous mode stays connected to the click; Global mode examines the whole layer. Tolerance, sampling, alpha mode, and selection mode control comparison and the resulting region.

## Shortcut and appearance

`F` activates the tool. The toolbar shows tolerance, flood mode, sampling, alpha mode, and selection mode. The button and cursor show a paint bucket.

## Pintora implementation

`PaintBucketTool.cs` uses `FloodTool` and an undo history; GTK builds the neutral controls. Avalonia `Canvas/FillTool.cs` provides basic filling, but its Paint.NET options and sampling semantics need parity verification.

## Improvements

Share one Core flood service between Bucket and Magic Wand, then test contiguous/global behavior, transparent pixels, tolerance, and locked layers on both platforms.

