# Brand assets

Pointy-top hexagon, soft diagonal gradient, white rounded M.

## Palette

| role | hex |
|------|-----|
| purple gradient | `#9760f5` → `#6a2ed3` |
| purple flat (badges, dots) | `#7c3aed` |
| orange gradient | `#ffa14a` → `#f2640a` |
| orange flat (badges, dots) | `#f2640a` |
| night background (banners) | `#150e2a` |
| letter | `#ffffff` |

Zone semantics: purple marks the sterile zone (`src/`, `apps/Cli`, `apps/Tui`);
orange marks everything outside the purity gate (`apps/Gui.Demo`, `tests/`).

## Files

- `icon-purple.svg` — primary mark (README hero, app icon, social preview)
- `icon-orange.svg` — secondary mark for outside-the-gate contexts
- `hex-purple.svg`, `hex-orange.svg` — 24px zone dots for tables and dividers
- `favicon.ico` — 16/32/48/64/128/256 multi-size, built from the purple icon.
  The two large entries matter: without them Explorer and the taskbar upscale the
  64px one and the mark looks soft.
- `favicon-{16,32,48,64,128,256}.png` — raster renditions

The white letter sits on the colored field, so both icons work unchanged
on light and dark backgrounds; no `prefers-color-scheme` swap needed.
