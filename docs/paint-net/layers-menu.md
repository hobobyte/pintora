# Layers Menu

**Paint.NET:** [Layers Menu](https://mail.getpaint.net/doc/latest/LayersMenu.html)

![Paint.NET Layers Menu](https://mail.getpaint.net/doc/latest/images/menus/layers/layersmenu.png)

## Commands and shortcuts

Commands apply only to the active layer: add, delete, duplicate, merge down, toggle visibility, import from file, flip horizontal/vertical, rotate 180°, Rotate/Zoom, and navigate to top/above/below/bottom. Paint.NET also exposes keyboard shortcuts for layer navigation and actions; the Layers Window buttons mirror many of them.

## Pintora implementation

Core `LayerActions`, `DocumentLayers`, history items, and blend/compositing services provide the model. GTK wires the action menu and Layers pad. Avalonia's `MainWindow` and `CanvasHost` already implement add, duplicate, move, merge, delete, import-as-layer, visibility, locking, and layer undo.

## Gaps and poor behavior to fix

Document the final shortcut table instead of relying on menu text. Add missing Rotate/Zoom and import/move-to-top/bottom affordances to Avalonia, and verify that all layer commands preserve annotations, metadata, locks, opacity, blend mode, and active-layer identity.

