# Pintora tools: Paint.NET reference

These pages document every tool listed in the official [Paint.NET Tools Window](https://mail.getpaint.net/doc/latest/ToolsWindow.html), then compare it with current Core, GTK, and Avalonia implementations in Pintora.

Each page covers the shortcut, appearance, options, interaction details, current Pintora behavior, known gaps, and practical improvements. Implementation paths are included as an engineering reference.

![Paint.NET Tools Window](https://mail.getpaint.net/doc/latest/images/tools/toolswindow.png)

## Selection

- [Rectangle Select](rectangle-select.md)
- [Lasso Select](lasso-select.md)
- [Ellipse Select](ellipse-select.md)
- [Magic Wand](magic-wand.md)

## Move and view

- [Move Selected Pixels](move-selected-pixels.md)
- [Move Selection](move-selection.md)
- [Zoom](zoom.md)
- [Pan](pan.md)

## Fill and drawing

- [Paint Bucket](paint-bucket.md)
- [Gradient](gradient.md)
- [Paintbrush](paintbrush.md)
- [Eraser](eraser.md)
- [Pencil](pencil.md)

## Photo, text, and shapes

- [Color Picker](color-picker.md)
- [Clone Stamp](clone-stamp.md)
- [Recolor](recolor.md)
- [Text](text.md)
- [Line/Curve](line-curve.md)
- [Shapes](shapes.md)

## Shared UI behavior

Paint.NET uses `F5` to show or hide Tools and `Ctrl+Shift+F5` to reset its position and size. Tooltips expose shortcuts. `Space` + drag temporarily pans from any tool. GTK and Avalonia should expose the same behavior.

## Main Pintora gaps

Avalonia currently groups several Core tools into `Selection`, `Brush`, and `Shapes`; separate Magic Wand, Lasso, Move, Clone Stamp, Recolor, and Line/Curve entries are missing. Core/GTK already have the finer-grained model. Flood, selection, gradient, and drawing algorithms should remain in Core; platform projects should provide input, cursors, and controls only.

