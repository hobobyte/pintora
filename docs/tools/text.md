# Text

**Paint.NET:** [Text Tool](https://mail.getpaint.net/doc/latest/TextTool.html) · [Tools Window](https://mail.getpaint.net/doc/latest/ToolsWindow.html)

## Behavior

Click or drag a text box, type, then commit with `Enter` or by switching tools; `Escape` cancels unfinished input. Font, size, variant, alignment, fill/outline, and IME preedit affect the live layout. IME composition must remain visible until commit.

## Shortcut and appearance

`T` activates Text; `[` and `]` change size. The toolbar contains font, variant, size, bold/italic/underline, alignment, fill style, outline, and join controls. The cursor changes between text, editable, and not-allowed states.

## Pintora implementation

`TextTool.cs` uses neutral `ITextInputContext`, font descriptors, and `IToolBarFontButton`; GTK provides the IME adapter. Avalonia `Canvas/TextTool.cs` and `MainWindow.cs` support editable annotations, font family/size, bold, and underline, but variants, outline, and full IME parity need checking.

## Improvements

Keep text layout/measurement in Core and platform-specific IME/controls outside it. Test preedit/commit, Unicode, RTL, font fallback, cancellation, and stable undo.

