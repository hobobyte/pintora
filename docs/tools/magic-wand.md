# Magic Wand

**Paint.NET:** [Magic Wand Tool](https://mail.getpaint.net/doc/latest/MagicWand.html) · [Tools Window](https://mail.getpaint.net/doc/latest/ToolsWindow.html)

## Behavior

Selects pixels of similar color. Contiguous mode floods from the click through neighboring pixels; Global mode checks the whole layer. Tolerance controls similarity. Sampling can use the composite Image or active Layer, and alpha comparison can be Premultiplied or Straight. The active selection can be recalculated after changing tolerance or dragging its source-point nub.

`Ctrl` temporarily adds, `Alt` subtracts, and `Shift` enables Global mode. `Enter` commits active selection editing.

## Shortcut and appearance

`S` four times, or `Shift+S`, selects it. The toolbar contains tolerance, flood, sampling, alpha, and selection modes; the canvas shows a source-point control nub.

## Pintora implementation

`src/core/Pinta.Tools/Tools/MagicWandTool.cs` shares the flood/selection pipeline with `FloodTool.cs` and GTK toolbar adapters. Avalonia has `FillTool`, but no separate Magic Wand or editable tolerance selection.

## Improvements

Add an Avalonia controller, Image/Layer sampling, alpha modes, and a source-point nub. Test transparent pixels, Global islands, tolerance recomputation, and additive/subtractive selection.

