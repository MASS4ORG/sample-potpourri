# UI textures — drop files here

All PNG, RGBA, straight (non-premultiplied) alpha.

| File | Size | 9-slice border | Notes |
|---|---|---|---|
| `button_9slice_normal.png` | ~48×48 | 12 px all sides | rest state |
| `button_9slice_hover.png` | ~48×48 | 12 px | brighter / accent edge |
| `button_9slice_pressed.png` | ~48×48 | 12 px | inset / darker |
| `button_9slice_disabled.png` | ~48×48 | 12 px | desaturated *(optional)* |
| `panel_9slice.png` | ~64×64 | 16 px | window / card background *(optional — theme.uss draws one procedurally)* |
| `scrollbar_thumb_9slice.png` | ~16×16 | 6 px | *(optional)* |
| `icon_power.png` | ~32×32 | — | *(optional; FontAwesome is bundled)* |
| `icon_refresh.png` | ~32×32 | — | *(optional)* |
| `icon_cpu.png` | ~32×32 | — | *(optional)* |

The `nine-slice="12,12,12,12"` attribute in the `.ui` files is `left,top,right,bottom`
in source pixels; keep the corner regions inside those insets free of stretchable detail.
