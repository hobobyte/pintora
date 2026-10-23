# Pintora 0.0.1-alpha

This is the first alpha preview of Pintora's Avalonia desktop application. It is an early,
work-in-progress editor and is not yet a replacement for the established GTK application.

## Included in this preview

- A cross-platform Avalonia desktop shell and canvas.
- Core raster painting tools, including brush, pencil, eraser, fill, gradient, color picker,
  selection, zoom, and shape editing.
- Layers, common image operations, keyboard shortcuts, and light/dark themes.
- Self-contained builds for Windows x64, Linux x64, and macOS x64/Apple silicon.
- Installable packages: Windows MSI, Linux AppImage and Debian `.deb`, and macOS DMGs for Intel and
  Apple silicon. Portable ZIP/tar.gz archives are also available.

## Alpha caveats

- Feature coverage and file-format workflows are incomplete compared with the GTK application.
- Keep backup copies of important artwork and project files; this alpha is for evaluation.
- Packages are currently unsigned. macOS builds are not notarized, so the operating system may show
  a security warning on first launch.
- Report bugs with the operating system, steps to reproduce, and a sample file when it is safe to
  share one.

## Running

Extract the portable archive for your operating system and launch the `Pintora` executable inside.
No .NET runtime installation is required for these self-contained packages.
