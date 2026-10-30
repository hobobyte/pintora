# Help Menu

**Paint.NET:** [Help Menu](https://mail.getpaint.net/doc/latest/HelpMenu.html)

![Paint.NET Help Menu](https://mail.getpaint.net/doc/latest/images/menus/help/helpmenu.png)

## Behavior and shortcuts

Help opens online Documentation with `F1`, Paint.NET Search with `Ctrl+E`, the website, forum, tutorials, plugins, feedback/bug reporting, and About. Online entries require a browser/network connection; plugin and forum content is third-party.

## Pintora implementation

GTK has About and keyboard-shortcut actions and platform browser launch helpers. Avalonia has `AboutWindow`; external-link and documentation commands are not yet a complete Help menu surface.

## Gaps and poor behavior to fix

Add a platform-neutral Help action catalog and platform browser service. Provide F1 and Ctrl+E bindings, a clear offline failure message, version/build information in About, and links to Pintora documentation and issue reporting.

