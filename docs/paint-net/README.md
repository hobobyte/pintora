# Paint.NET workspace and menu reference

This section complements the per-tool reference in [`docs/tools`](../tools/README.md). It covers the main Paint.NET windows and menus, with special attention to keyboard shortcuts, interaction semantics, and how the behavior maps to Pintora's Core, GTK, and Avalonia code.

Sources are the official Paint.NET documentation pages. “Pintora gap” means a behavior not found in the inspected implementation; it is not a claim that the feature can never be implemented elsewhere. Where the source exposes them, pages include remote screenshots from the official documentation; no binary assets are added to the repository.

## Windows

- [Layers Window](layers-window.md)
- [Colors Window](colors-window.md)
- [History Window](history-window.md)

## Menus

- [File Menu](file-menu.md)
- [Edit Menu](edit-menu.md)
- [View Menu](view-menu.md)
- [Image Menu](image-menu.md)
- [Layers Menu](layers-menu.md)
- [Adjustments Menu](adjustments-menu.md)
- [Effects Menu](effects-menu.md)
- [Help Menu](help-menu.md)

## Product-level comparison

Paint.NET has a document-centric command model: commands act on the active document and, where applicable, the active layer or selection. Pintora's Core already contains document, layer, history, palette, file-format, and action services. The main compatibility risk is that GTK exposes the Core action graph while Avalonia still has several direct `MainWindow`/`CanvasHost` implementations and does not expose every command or dock window.

