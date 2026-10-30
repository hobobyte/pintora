# Recolor

**Paint.NET:** [Recolor Tool](https://mail.getpaint.net/doc/latest/RecolorTool.html) · [Tools Window](https://mail.getpaint.net/doc/latest/ToolsWindow.html)

## Behavior

Replaces matching colors under the brush with Primary (left) or Secondary (right), subject to tolerance. Unlike Bucket, the operation follows the brush path and is not restricted to one connected region.

## Shortcut and appearance

`R` activates Recolor. The button shows a recoloring icon; the toolbar has brush controls and a tolerance slider; the cursor is shaped like the active brush.

## Pintora implementation

`RecolorTool.cs` compares `ColorBgra` values within tolerance, writes through a stencil/ROI, and persists `RECOLOR_TOLERANCE`. Avalonia has no separate tool entry.

## Improvements

Add Avalonia Recolor and a shared Core color-distance/alpha policy. Test continuous drag, ROI boundaries, large layers, and tolerance performance.

