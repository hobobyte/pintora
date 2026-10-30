# File Menu

**Paint.NET:** [File Menu](https://mail.getpaint.net/doc/latest/FileMenu.html)

![Paint.NET File Menu](https://mail.getpaint.net/doc/latest/images/menus/file/filemenu.png)

## Commands and shortcuts

`Ctrl+N` creates a new white, single-layer image; the dialog supports pixel size, resolution, print size, units, and aspect-ratio locking. `Ctrl+O` opens an image and adds a document tab. Open Recent keeps the last ten entries and can be cleared. `Ctrl+S` saves using the current path/format; `Ctrl+Shift+S` selects a new name, location, format, and settings. `Ctrl+Alt+S` saves all modified documents. `Ctrl+P` prints; `Ctrl+W` closes the document. Exit prompts to save every modified image.

Save configuration is format-specific (quality, transparency, dithering, bit depth, compression). Paint.NET's current format list includes PDN, PNG, JPEG, JPEG XL/XR, BMP, GIF, TGA, DDS, TIFF, HEIC, WebP, and AVIF.

## Pintora implementation

Core has `FileActions`, `RecentFileManager`, format catalogs/providers, and document save events. GTK has separate New/Open/Save/Save As/Save All/Close/Exit/Print actions and platform file dialogs. Avalonia wires New/Open/Save/Save All/Close/Exit through `MainWindow`, `DocumentTabManager`, and `AvaloniaFileDialogService`; recent files and format providers are present.

## Gaps and poor behavior to fix

The shortcut mapping differs: Avalonia currently documents Save All as `Ctrl+Shift+S`, while Paint.NET uses `Ctrl+Alt+S`; this must be resolved deliberately and tested. Avalonia does not yet match Paint.NET's full format/configuration matrix, scanner Acquire command, URL-open flow, print wizard, and comprehensive unsaved-close coordination. Keep save validation and format configuration in Core/platform services, not menu handlers.

