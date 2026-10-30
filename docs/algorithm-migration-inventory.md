# GTK algorithm migration inventory

This inventory separates platform-neutral math from GTK rendering and input. The neutral part
belongs in `Pinta.Core.Neutral/Algorithms`; GTK and Avalonia should call the same operation.

## Already shared

- Flood stencil: contiguous flood, global scan, tolerance conversion, mask combine, rectangle
  masks, bounds (`FloodStencil`).
- Raster sampling, flood fill, linear gradient, opacity, alpha compositing, recolor adjustment,
  recolor matching (`RasterAlgorithms`).
- Selection combine and mask geometry.

## Remaining neutral candidates

1. **Eraser falloff and brush coverage** — radial LUT and Avalonia hardness coverage now live in
   Core; keep Cairo stroke compositing and remaining brush rasterization in the adapters.
2. **Transform gesture math** — completed in `TransformAlgorithms`; `BaseTransformTool` keeps
   only Cairo matrix construction and selection-path rendering.
3. **Selection geometry** — already implemented by `DocumentSelection`, `RectangleI` and
   `SelectionModeHandler` in Core; Cairo path construction remains platform-side.
4. **Gradient parameterization** — already implemented in `Pinta.Core/Effects/GradientRenderers`
   for linear, reflected, radial, diamond and conical coordinates; only blend operators and
   clipping remain platform-side.
5. **Viewport math** — already implemented by `DocumentViewportState` and
   `DocumentWorkspace`; native scrollbar and gesture event handling remain platform-side.
6. **Canvas grid geometry** — visibility and cell-step calculations now use `GridAlgorithms` in
   both GTK and Avalonia; path generation and drawing remain in the platform canvas.
7. **Shape rasterizer geometry** — bounds, rotated bounds, ellipse/arc/line sample counts,
   containment, dash visibility and stamping now use `ShapeAlgorithms`; pixel rasterization
   remains in Avalonia.
8. **Annotation transform geometry** — Avalonia still contains rotation/corner calculations for
   non-raster annotations; extract only if those transforms are shared with GTK.

## Intentionally platform-specific

- Cairo/GSK/Skia drawing contexts, blend modes, clipping and path construction.
- GTK/Avalonia pointer, keyboard, gesture, toolbar and settings plumbing.
- Native surface creation, invalidation and window/layout calculations.

Each remaining candidate needs a Core test before the corresponding adapter is changed. This file
is the checklist for completing the migration; it is not a claim that the remaining candidates
are already migrated.
