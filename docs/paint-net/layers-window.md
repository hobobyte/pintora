# Layers Window

**Paint.NET:** [Layers Window](https://mail.getpaint.net/doc/latest/LayersWindow.html)

![Paint.NET Layers Window](https://mail.getpaint.net/doc/latest/images/layerswindow/layerswindow.png)

## Behavior and appearance

The window is a dockable stack of layer rows. One row is highlighted as the active layer; drawing commands affect only that layer. Each row exposes thumbnail, visibility, name, and lock state. Layer opacity and blend mode apply to every pixel in that layer. `F7` toggles the window; `Ctrl+Shift+F7` resets its dock position and size. `F4` opens active-layer properties; double-clicking the name does the same.

Buttons add a transparent layer above the active one, delete (except the final layer), duplicate, merge down, move up/down, and open properties. Drag-and-drop reorders layers; `Ctrl` + move-up/down sends the layer to the top/bottom.

## Pintora implementation

Core: `DocumentLayers`, `Layer`, `LayerProperties`, `LayerActions`, and the layer history items. GTK: `Pinta/Pads/LayersPad.cs` and `Pinta.Gui.Widgets/Widgets/Layers/*`. Avalonia: `CanvasHost` layer operations, `AvaloniaLayer`, `DocumentSession`, and the Layers panel in `MainWindow.cs`. Avalonia already supports add/delete/duplicate/move/merge, visibility, locking, opacity, thumbnails, and undo snapshots.

## Gaps and poor behavior to fix

Avalonia lacks Paint.NET's dock/reset shortcut model, drag-and-drop layer ordering in the visible panel, and a full Layer Properties dialog with blend mode parity. Verify that merge/duplicate preserve annotations, blend mode, opacity, lock, and undo exactly. Do not duplicate layer compositing math in the platform UI.

