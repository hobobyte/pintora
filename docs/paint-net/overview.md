# Paint.NET product model relevant to Pintora

**Paint.NET:** [Overview/Features](https://mail.getpaint.net/doc/latest/index.html)

![Paint.NET main window](https://mail.getpaint.net/doc/latest/images/mainwindow/Index-UISCREENSHOT.jpg)

## Behavior to preserve

Paint.NET is a tabbed multi-document editor with live image thumbnails, a layer stack, per-document history, many raster formats, color management, and plugin-based effects/file types. PDN preserves the lossless layer structure; single-layer images commonly default to PNG. Commands are scoped to the active document, layer, or selection.

## Pintora comparison

Pintora already has document sessions/tabs, Core layers/history, format providers, palette services, GTK docking, and an Avalonia document/layer editor. The Avalonia path is the target cross-platform UI, while GTK remains the reference implementation for mature commands.

## Priority gaps

1. Unify command and history contracts across GTK/Avalonia.
2. Complete dock windows and keyboard shortcuts (Layers, Colors, History, Tools).
3. Move image/effect/color mathematics into Core and keep platform projects thin.
4. Match format coverage, color-profile handling, plugin/effect discovery, and save configuration.
5. Add behavioral tests for active-document routing, selections, alpha, undo branches, and unsaved-close prompts.

