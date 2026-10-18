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
2. `useBoardMotion(...)` builds goo descriptors from filtered columns supplied by `BoardSurface.vue`:
   - `buildSlickGooDescriptors(...)`
   - `buildSlickGooMembershipSignature(...)`
   - `buildSlickGooStyleSignature(...)`
3. `useGooLayer(...)` tracks card DOM geometry and emits `gooGroups`.
4. `BoardSurface.vue` renders goo groups/blobs in `.goo-layer` behind cards.

`BoardView.vue` owns screen-level filters, selection actions and dialogs. `BoardSurface.vue` owns board rendering and drag interaction. `useBoardInlineCreation(...)` owns draft/focus/validation/save state and uses semantic creation lifecycle operations on `useBoardMotion(...)`; DOM lookup, handoff sequencing and animation-to-goo updates stay inside the motion layer. Low-level box effects for creation, editor handoff and removal remain in `useCardLayoutAnimation(...)`.

`useBoardMotion` owns one `createBoardAnimationRunner(...)` instance shared by card layout and movement. The runner owns the 200 ms easing, reduced-motion preference listener, native animation registration and a single animation frame loop refreshing slick/selection goo. Effects retain geometry, Vue transition callbacks and DOM cleanup. Finish/cancel handlers run once; reduced motion finishes effects and skips new native animations; disposing the runner cancels active effects and its pending frame. Scrolling/resizing settles movement effects without finishing unrelated creation effects. Animation-driven goo measurements consume pending goo refresh requests while retaining their dirty flags; idle scrolling still uses cached geometry.

During a local create, the motion layer holds new card arrivals in that column out of `renderedColumns` until the request settles, so an early realtime event cannot display a second card below the editor. Store data stays live; existing cards and other columns continue updating. Creation holds are shared by rendering, drag targets, goo and visible-card selection; departing snapshots are excluded from interactive consumers.

The known-card snapshot comes from the unfiltered card store, so changing filters during a save can reveal existing cards immediately.

The motion layer uses one watcher for canonical membership/order, filtered source columns and filter state to identify visible arrivals and moves together. Initial mounting and filter reveals stay immediate. Cards held for a local save use the editor handoff instead. Arrival effects collapse vertical padding/borders along with height and compensate for changed slick-boundary margins, keeping neighbouring cards and goo aligned.

Canonical card removal uses `useBoardCardRemovals(...)` to retain visible departing card snapshots only in the presentation projection. Local delete, archive/transfer and realtime removal share this path without delaying stores or repeating HTTP/realtime effects. A departing card is inert, excluded from counts, selection and drop targets, and retains its displayed thumbnail until collapse finishes. Slick goo follows the retained geometry in both renderers; a card already floating keeps the established Full/Lite travel behaviour. Live cards use their final slick-boundary classes while retained cards keep their prior boundary classes. Each projected column array owns a precomputed boundary map, so repeated goo-driven renders use constant-time class lookups; live-card order and slick-membership changes create a fresh projection. Removal collapses height, padding, margins and opacity; horizontal borders are temporarily painted as inset shadows to avoid the browser's minimum 1px border producing a final 2px jump. The coordinator recalculates disappearing flex-gap compensation on interruption, including when subsequent removals empty a column. Changes to effective filter values settle movement and departing snapshots immediately (catalogue refreshes that recreate equivalent filter objects do not); reduced motion skips retention or finishes active effects. Teardown clears presentation state and effects.

`useBoardMotion` batches layout work for the current render. Before rendering it captures displayed movement geometry and unfinished box-effect styles before cancelling either effect. After rendering it prepares all creation effects and temporarily applies their final styles to measure final destinations/column heights, then restores the styles and starts creation. Same-column transforms are calculated against creation's starting layout so growing space is not counted twice; floating cards and column heights use the measured final layout. Interrupted effects retarget from their displayed positions over a fresh 200 ms. Draft opening/cancellation and saved-card handoff join this same pass through semantic calls from inline creation; focus and typing remain immediate. The movement snapshot also captures column scroll offsets before rendering. Restore those offsets before preparing effects unless a newly focused input has intentionally revealed a different viewport, and restore scrolled ancestors after temporary endpoint styles are removed, so native scroll anchoring/clamping cannot move the starting viewport. Queued scroll events with an unchanged restored offset do not settle movement; changed offsets retain the normal scroll-settling behaviour. Pending work is discarded on teardown.

Movement detection uses canonical column membership/order, independently of filtering. `findMovedColumns` compares cards present in both snapshots so creation/removal alone and duplicate HTTP/realtime delivery do not trigger movement. Creation effects prepare their geometry separately from playback; unfinished effects retain their completion callback and can rebind to a remounted card after crossing columns.

Within-column effects transform the real card. Cross-column effects use an inert floating copy in the board's motion layer while the real destination card reserves its layout space. Column heights animate with their cards so shrinking columns do not prematurely clip displaced neighbours. The coordinator passes prepared box-effect endpoints to movement; floating copies mirror padding, borders and opacity along with their own position/size, so a departing copy can collapse completely and a growing copy retains its current box styles. The HTML renderer includes travelling cards in its existing board-level slick/selection goo groups, preserving connections to stationary cards. Lite keeps its existing column SVG layers: cross-column travelling cards have no slick/selection goo until they land. Movement owns a reactive map of floating card elements. Both goo layers observe that map directly and rebuild tracking when an override is registered, replaced or removed. Goo resolves registered floating elements before actual column cards, taking clipping from the resolved element; floating copies carry only their moving-card identity rather than a duplicate `data-card-id`. Landing returns tracking to the actual destination card and Lite rebuilds its column goo. Slick membership in the store stays intact throughout. `BoardSurface` resolves the renderer mode once and shares it with motion and rendering. Scrolling/resizing settles movement; offscreen cross-column endpoints remain clipped rather than floating over headers. Reduced motion skips/finishes effects, and filtering/removal/teardown cleans up copies and restores destination visibility. Both renderer modes can be exercised in Chromium, but Safari behaviour still needs real-world browser testing.

Key files:

- `BoardOil.Web/src/board/components/BoardSurface.vue`
- `BoardOil.Web/src/board/composables/useBoardMotion.ts`
- `BoardOil.Web/src/board/composables/useBoardInlineCreation.ts`
- `BoardOil.Web/src/board/composables/useCardLayoutAnimation.ts`
- `BoardOil.Web/src/board/composables/useBoardCardRemovals.ts`
- `BoardOil.Web/src/board/composables/useCardMovementAnimation.ts`
- `BoardOil.Web/src/board/utils/boardAnimationRunner.ts`
- `BoardOil.Web/src/board/utils/cardMovement.ts`
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
- `scroll`
  - Scroll path; prefers fast projection from cached geometry.
- `layout`
  - Remeasure the tracked elements' bounds during animation, retaining their membership and element tracking.

Main mechanisms:

- `requestAnimationFrame` coalescing: multiple refresh requests collapse into one frame.
- Nested scroll capture on board root:
  - listener is attached with `capture: true` to catch column scroller events.
- Fast-path projection:
  - Scroll and style refreshes project rects from cached geometry + scroll offsets; layout refreshes measure current bounds.
  - Avoids repeated `getBoundingClientRect()` for every card on every scroll tick.
- Culling:
  - Cards are skipped when outside expanded clip rect.
  - Margin is derived from bridge/growth config via `resolveGooCullingMarginPx(...)`.

## Debug Mode

Perf debug logs are disabled by default and enabled by any of:

- query string: `?gooPerfDebug=1` (or `true`)
- env: `VITE_GOO_PERF_DEBUG=true` (or `1`)

Renderer overrides are temporary query-string diagnostics: `?gooRenderer=full` or `?gooRenderer=lite`.
Goo diagnostics must not use browser storage.

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
- If you change class names or layer structure in `BoardSurface.vue`, verify:
  - per-column scroll alignment
  - board horizontal scroll alignment
  - no header bleed
  - distinct slick separation

## Known Constraints

- Goo is frontend-only visual behavior.
