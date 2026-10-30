# Image Menu

**Paint.NET:** [Image Menu](https://mail.getpaint.net/doc/latest/ImageMenu.html)

![Paint.NET Image Menu](https://mail.getpaint.net/doc/latest/images/menus/image/imagemenu.png)

## Commands and shortcuts

Image commands affect all layers. Crop to Selection uses the selection bounding rectangle and makes pixels outside a non-rectangular selection transparent. `Ctrl+R` opens Resize; it supports percentage/absolute size, resolution/DPI, aspect-ratio lock, and multiple resampling algorithms. Canvas Size changes the document bounds without scaling pixels; Flip/Rotate affect the complete image.

## Pintora implementation

Core owns `Document`, image size, selections, layer history, and pixel operations. GTK has image resize/canvas/rotate actions and dialogs. Avalonia exposes resize/canvas actions in `MainWindow` and keeps image-size state in `CanvasHost`/`DocumentSession`.

## Gaps and poor behavior to fix

Audit Avalonia for crop-to-nonrectangular-selection, DPI/print-size semantics, high-quality resampler parity, and one undo item per image command. Image-wide operations must not accidentally operate only on the active layer; keep those algorithms in Core.

