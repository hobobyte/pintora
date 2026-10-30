# Zoom

**Paint.NET:** [View Tools](https://mail.getpaint.net/doc/latest/ViewTools.html) · [Tools Window](https://mail.getpaint.net/doc/latest/ToolsWindow.html)

## Behavior

Left click zooms in, right click zooms out, and dragging a rectangle fits that region to the editor. Ctrl + mouse wheel duplicates zooming. Middle-button drag pans while Zoom is active; zoom changes the view only and never pixels.

## Shortcut and appearance

`Z` activates Zoom. The button is a magnifying glass; the cursor indicates zoom-in or zoom-out. Avalonia's status bar shows percentage, `+`, `-`, and Fit controls.

## Pintora implementation

Core: `ZoomTool.cs`; Avalonia: `Canvas/ZoomTool.cs` and `MainWindow.cs`; GTK uses the document viewport host. The scale exists in both platforms, but region zoom and focus-around-pointer need parity testing.

## Improvements

Centralize viewport math in Core, standardize scale steps and cursor focus, and test Fit, region zoom, wheel zoom, and zooming while a selection is active.

