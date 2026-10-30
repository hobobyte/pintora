# View Menu

**Paint.NET:** [View Menu](https://mail.getpaint.net/doc/latest/ViewMenu.html)

![Paint.NET View Menu](https://mail.getpaint.net/doc/latest/images/menus/view/viewmenu.png)

## Commands and shortcuts

View commands do not change pixels. Zoom In/Out use fixed steps; Zoom to Window fits the canvas; Zoom to Selection fits the active selection; Actual Size is 100%. Pixel Grid appears only at 200% or higher. Rulers toggle around the canvas and display the selected measurement units (pixels, inches, centimeters). The Zoom and Pan tool pages define `Z`, `H`, Space+drag, wheel, and arrow-key behavior.

## Pintora implementation

Avalonia exposes zoom percentage, `+`, `-`, Fit, and canvas viewport controls in `MainWindow`; `CanvasCoordinateMapper` handles coordinate conversion. GTK has viewport/ruler widgets and View actions. Core has `DocumentViewportState` and neutral viewport contracts.

## Gaps and poor behavior to fix

Avalonia lacks the complete View menu model: pixel grid, rulers, units, zoom-to-selection, overscroll preferences, and the same fixed-step table. Move viewport math and unit conversion into Core and let GTK/Avalonia render the controls.

