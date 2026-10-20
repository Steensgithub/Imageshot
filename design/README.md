ImageShot captures screenshots on Windows and in Chrome/Edge. Version 2 of its look keeps the ImageShot blue and the viewfinder mark, but moves the neutrals from cool grey to warm stone and raises contrast everywhere: every text pair reads at 4.9:1 or better, body and secondary text at 7:1 or better, and every control has a visible 1.5px edge.

## Content fundamentals

- Write short, plain sentence case. Name the thing the person sees: "Visible area", "Select area", "Full page", "All tabs".
- Address the reader as "you" only when you must explain something: "To capture “All tabs”, ImageShot needs permission to read the pages in your open tabs."
- Buttons say what happens: "Allow and capture", "Cancel". Toasts say what happened, in past tense: "Copied to the clipboard".
- Errors name what failed and the result: "Access was not granted, so no tabs were captured."
- Use typographic quotes “ ” and the en dash – in option labels: "WebP – small files". Use × for sizes: "1920 × 1080".
- No emoji. The UI ships in Danish, English, German and Spanish, so leave room for 30% longer labels.

## Visual foundations

**Color.** Set windows on `bg`, cards and bars on `surface`, wells (inputs, selects, capture buttons) on `surface-sunk`. Text is `ink`; secondary text is `muted`. `primary` is the one action color: the primary button, the slider fill, links, the selected tool and the focus ring. `accent` (warm amber) marks the live value: the resolution percentage and the region size readout. Use it once or twice per window, never for actions. `danger` only for errors and destructive actions, always with a word. Hairlines are `line`; control edges are `border-strong`.

**Annotation colors.** `mark-red`, `mark-amber`, `mark-blue`, `mark-black` and `mark-white` are drawn into the user's screenshot. They are the same in both themes so a shared image looks the same everywhere.

**Type.** `Schibsted Grotesk` (Google Fonts) for all UI, falling back to Segoe UI. Pixel sizes and coordinates use `JetBrains Mono` in the `readout` style. Window titles use `display`, the popup header uses `heading`, card titles use `title`, running text `body`, buttons `label`, explanations `hint`, file details `caption`. Weights are heavier than v1 (titles 700, header 800) for contrast.

**Spacing.** Four-pixel steps: `space-1` between tools, `space-2` inside the capture grid, `space-3` between cards, `space-4` card and window padding, `space-5` for dialogs, `space-6` between sections.

**Shape.** `radius-lg` (16px) for cards and screenshot tiles, `radius-md` (10px) for buttons and fields, `radius-sm` for tool buttons, `radius-xs` for crop handles, `radius-pill` for toasts and the slider.

**Depth.** Flat by default: cards separate from `bg` by ground and `line`, not by shadow. Shadows only for what floats: `shadow-toast`, `shadow-shot` on thumbnails, `shadow-stage` on the editor image.

**States.** Hover: `primary-soft` ground and `primary` edge. Selected: the same, plus `primary` glyph. Focus: a solid 2px `focus` outline with 2px offset on every control. Disabled: 55% opacity (35% for tool buttons).

**Imagery.** Screenshots sit on a checkerboard of `surface-sunk` and `surface` (16px squares) so transparency shows. The crop and draw stage is `scrim` in both themes.

## Iconography

Capture modes and tools use Unicode glyphs (▭ ⬚ ▯ ❐ ↗ ◯ T ↶) in `primary` or `ink`; the Windows app draws its own vector icons in `Icons.cs`. Every icon-only button carries an accessible name. The app mark is in `assets/Logos/`.

## Light and dark

Both themes follow the system setting, and the Windows app's Light/Dark/Auto option. Dark is warm brown-black, not blue-grey. In dark, `primary` turns pale blue and `on-primary` turns deep navy, so primary buttons have dark text.

## Not synced from the code

The v1 values in `extension/ui.css` and `windows/src/ImageShot/Theme.cs` are the starting point; this system deliberately changes them (warmer, higher contrast). The Windows toast and region-overlay colors written inline in `Toast.cs` and `RegionOverlay.cs` map to `toast` and `scrim`. Components here are static HTML renditions styled by `components/bundle.css`, not the extension's script.
