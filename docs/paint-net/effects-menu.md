# Effects Menu

**Paint.NET:** [Effects Menu](https://mail.getpaint.net/doc/latest/EffectsMenu.html)

![Paint.NET Effects Menu](https://mail.getpaint.net/doc/latest/images/menus/effects/effectsmenu.png)

## Behavior and shortcuts

Effects apply to the active layer or active selection. Paint.NET groups built-ins into Artistic, Blurs, Color, Distort, Noise, Object, Photo, Render, and Stylize. The first entry repeats the last effect; `Ctrl+F` repeats it with the same parameters and no dialog. Third-party effects show a plugin icon, tooltips identify author/DLL, and plugin-load errors link to Settings.

## Pintora implementation

Core contains effect interfaces, effect implementations, `Pinta.Effects`, and neutral history/dialog contracts. GTK has the mature effect browser and dialogs (`SimpleEffectDialog` and specialized dialogs). Avalonia currently has a small set of canvas effects (`EffectTool`) rather than the full Effects menu/plugin catalog.

## Gaps and poor behavior to fix

Expose one effect catalog to both platforms, preserve plugin isolation and error reporting, implement repeat-last-effect state, and ensure effects honor active selection and undo. Keep native/GPU acceleration behind Core interfaces; do not duplicate effect math in Avalonia.

