# Pintora changelog

Notable changes to Pintora are documented here. The initial `0.0.0` entry records the fork
baseline; later entries track Pintora-specific work.

## Unreleased

### Added

- Unified Open and Save As workflows for PNG, JPEG, WebP, TIFF, BMP, GIF, SVG layers, and ORA.
- Layer duplication, ordering, merge, opacity controls, and history management.
- Selection commands and configurable shortcuts for Select All, Deselect, and layer duplication.
- Avalonia About window and History window controls.

### Fixed

- Debian package creation now writes artifacts to the intended output directory.
- Layer opacity controls and standard undo behavior remain synchronized when changing layers.

## 0.0.1-alpha

First alpha preview of the Pintora desktop editor.

### Added

- Cross-platform desktop shell and raster canvas.
- Core painting tools: brush, pencil, eraser, fill, gradient, color picker, selections, zoom, and
  editable shapes.
- Layers, common image operations, configurable keyboard shortcuts, and light/dark themes.
- Shape selection, move, resize, rotation, and live updates when shape options change.
- Self-contained Windows, Linux, and macOS builds, including MSI, AppImage, Debian, and DMG
  packages.

### Known limitations

- Feature coverage and file-format workflows remain incomplete compared with the original Pinta
  application and mature raster editors.
- This is an early preview; keep backups of important artwork and project files.
- Packages are unsigned. macOS builds are not notarized and may show a security warning on first
  launch.

## 0.0.0 — Fork baseline

- Started from the Pinta raster graphics editor codebase as the foundation for Pintora.
- Preserved the original Pinta and Paint.NET license notices and icon attributions.
