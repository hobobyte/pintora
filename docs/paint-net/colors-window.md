# Colors Window

**Paint.NET:** [Colors Window](https://mail.getpaint.net/doc/latest/ColorsWindow.html)

![Paint.NET Colors Window](https://mail.getpaint.net/doc/latest/images/colorswindow/colorswindow.png)

## Behavior and appearance

The window owns two color slots: Primary (foreground) and Secondary (background). A notch marks the active slot. `F8` toggles the window; `Ctrl+Shift+F8` resets its dock position. `C` switches the active slot, `X` swaps Primary and Secondary, and the black/white button restores defaults.

The expanded view exposes RGB, HSV, opacity, editable hexadecimal text, and a 96-color palette. Left-clicking the wheel writes to the active slot; right-clicking writes to the inactive slot. `Ctrl`, `Alt`, `Shift`, and `Ctrl+Shift` constrain hue/saturation selection. Palette left-click targets the active slot; right-click targets the inactive slot.

## Pintora implementation

Core stores colors in `ColorBgra`, `HsvColor`, palette services, and `PaletteManager`. GTK provides `ColorPanelWidget`, `ColorGradientWidget`, `PaletteWidget`, and `ColorPickerDialog`. Avalonia implements the color/palette panel in `MainWindow.cs`, persists Primary/Secondary and recent colors, and exposes `ColorDialogWindow`.

## Gaps and poor behavior to fix

Avalonia's panel is a compact custom control rather than a full dockable Colors Window: RGB/HSV/opacity editing, 96-slot palette expansion, modifier-constrained wheel selection, and F8/reset behavior are incomplete or need parity tests. Keep color math in Core and make both platforms consume the same conversion and palette services.

