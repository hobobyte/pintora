# Adjustments Menu

**Paint.NET:** [Adjustments Menu](https://mail.getpaint.net/doc/latest/AdjustmentsMenu.html)

![Paint.NET Adjustments Menu](https://mail.getpaint.net/doc/latest/images/menus/adjustments/adjustmentsmenu.png)

## Behavior and shortcuts

Adjustments change pixel colorization and apply to the active selection when one exists, otherwise to the active layer/image according to the command. Dialog controls support sliders, numeric entry, and arrow-key increments. Paint.NET documents shortcuts for every built-in adjustment; the shortcut must be shown in the menu and remain discoverable in Pintora.

The menu includes Auto-Level, Black and White, Brightness/Contrast, Curves, Hue/Saturation, Invert Colors, Levels, Posterize, Sepia, and other color operations. Dialog previews should be cancellable and commit as one history item.

## Pintora implementation

Core has effects/adjustment implementations under `src/core/Pinta.Effects/Adjustments` and shared pixel/color math. GTK provides effect dialogs such as Curves, Levels, Posterize, and Alignment. Avalonia has only selected effect/annotation tools and does not expose the full adjustment menu.

## Gaps and poor behavior to fix

Build a neutral adjustment catalog and dialog contract, then provide GTK/Avalonia views. Audit selection clipping, alpha preservation, preview cancellation, keyboard shortcuts, and one-undo semantics. Do not move adjustment mathematics into platform projects.

