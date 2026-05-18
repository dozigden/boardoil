# Goo + Slick Guidance

Read this before changing slick grouping visuals in `BoardOil.Web`.

## Terminology

- `Slick`
  - Domain entity: board-scoped grouping for cards (`card.slickId` optional).
  - A card can belong to at most one slick at a time.
  - Current slick styles are `solid` and `presets`.
- `Goo`
  - Presentation layer only.
  - Renders soft grouped backdrops for slick membership.
  - Must stay conceptually separate from slick domain behavior.

## Source of Truth and Flow

1. Board cards + slick catalogue are loaded into stores.
2. `BoardView.vue` builds goo descriptors from filtered columns:
   - `buildSlickGooDescriptors(...)`
   - `buildSlickGooMembershipSignature(...)`
   - `buildSlickGooStyleSignature(...)`
3. `useGooLayer(...)` tracks card DOM geometry and emits `gooGroups`.
4. Template renders goo groups/blobs in `.goo-layer` behind cards.

Key files:

- `BoardOil.Web/src/board/views/BoardView.vue`
- `BoardOil.Web/src/board/composables/useGooLayer.ts`
- `BoardOil.Web/src/board/utils/slickGooAdapter.ts`
- `BoardOil.Web/src/board/utils/gooLayout.ts`
- `BoardOil.Web/src/board/utils/gooConfig.ts`
- `BoardOil.Web/src/board/utils/gooGeometry.ts`

## Design Decisions

- Strict group separation is by `groupKey = slick-{id}`.
  - Different slicks do not share a group or merge into the same blob set.
- Goo is layered with `z-index: 2`; column/card content is layered above it with `z-index: 3`.
  - This keeps goo visible but non-interactive (`pointer-events: none`).
- Column clipping is intentional:
  - Goo blobs are clipped to `.column-content` content rects.
  - This prevents header bleed when cards scroll out of column view.
- Visual spacing between adjacent cards uses slick-boundary classes:
  - `.card--slick-gap` for slick/non-slick boundaries.
  - `.card--slick-gap-strong` for different slick-to-slick boundaries.

## Colour Rules

- Slick `presets` goo colour uses preset CSS tokens (`var(--bo-preset-{index})`).
- Slick `solid` goo colour uses the slick style background color.
- If style resolution fails or slick is missing, goo falls back to hashed colour by slick id.

## Performance Strategy

`useGooLayer` is optimized around refresh reasons:

- `structure`
  - Rebuild tracked cards/elements and geometry baseline.
  - Triggered by descriptor structure changes, resize observer, board changes.
- `styles`
  - Refresh colours only without full structure rebuild.
- `geometry`
  - Scroll path; prefers fast projection from cached geometry.

Main mechanisms:

- `requestAnimationFrame` coalescing: multiple refresh requests collapse into one frame.
- Nested scroll capture on board root:
  - listener is attached with `capture: true` to catch column scroller events.
- Fast-path projection:
  - On non-structure frames, rects are projected from cached geometry + scroll offsets.
  - Avoids repeated `getBoundingClientRect()` for every card on every scroll tick.
- Culling:
  - Cards are skipped when outside expanded clip rect.
  - Margin is derived from bridge/growth config via `resolveGooCullingMarginPx(...)`.

## Debug Mode

Perf debug logs are disabled by default and enabled by any of:

- localStorage: `boardoil:goo-perf-debug=1` (or `true`)
- query string: `?gooPerfDebug=1` (or `true`)
- env: `VITE_GOO_PERF_DEBUG=true` (or `1`)

When enabled, console logs once per second:

- `[goo-perf] avg(ms)` with average refresh/build time, average item count, and sample count.

## Tuning Knobs

Adjust only in `BoardOil.Web/src/board/utils/gooConfig.ts` for consistency.

- Blob shape/size:
  - `widthAdjustPx`, `heightAdjustPx`
  - `horizontalOffsetPx`, `verticalOffsetPx`
  - `blobBorderRadiusPx`, `minBlobSizePx`
- Goo softness/intensity:
  - `blurStdDeviation`
  - `alphaMultiplier`, `alphaOffset`
- Cross-column bridge behavior:
  - `bridgeMaxGapPx`, `bridgeMaxVerticalDeltaPx`
  - `bridgeOverlapPx`, `bridgeHeightRatio`
- Clip behavior:
  - `clipHorizontalInsetPx`

Prefer config tuning over ad-hoc component CSS for goo behavior changes.

## Guardrails for Changes

- Keep slick domain logic and goo rendering logic separate.
- Do not move goo calculations into broad reactive paths without profiling.
- If you change grouping, clipping, bridging, or fast-path geometry:
  - update unit tests in:
    - `gooLayout.test.ts`
    - `gooGeometry.test.ts`
    - `slickGooAdapter.test.ts`
    - `slickCardBoundary.test.ts`
- If you change class names or layer structure in `BoardView.vue`, verify:
  - per-column scroll alignment
  - board horizontal scroll alignment
  - no header bleed
  - distinct slick separation

## Known Constraints

- Goo is frontend-only visual behavior.

