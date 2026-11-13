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

Saved-card content edits join that same layout pass, with or without slick membership. Compare title, card type, tag membership/order, assignee display name and checklist counts before rendering, then animate only actual height differences. Ordinary resizing changes only height so CSS boundary margins stay intact; editor handoff separately interpolates from its slot's zero bottom margin to the card's natural margin. Interrupted effects keep their existing completion callbacks and retarget from the displayed box. Neighbours, column heights and goo share the same timing. Duplicate HTTP/realtime content, unchanged heights, initial mounting and filter reveals stay immediate. Effective filter changes finish saved-card box effects while preserving inline-editor transitions. Restore captured scroll after starting height effects as well as before measurement, because a natural shrink can clamp the scroller until its old height is reinstated. Catalogue-only label changes and asynchronous thumbnail loading remain immediate in this slice.

Canonical card removal uses `useBoardCardRemovals(...)` to retain visible departing card snapshots only in the presentation projection. Local delete, archive/transfer and realtime removal share this path without delaying stores or repeating HTTP/realtime effects. A departing card is inert, excluded from counts, selection and drop targets, and retains its displayed thumbnail until collapse finishes. Slick goo follows the retained geometry in both renderers; a card already floating keeps the established Full/Lite travel behaviour. Live cards use their final slick-boundary classes while retained cards keep their prior boundary classes. Each projected column array owns a precomputed boundary map, so repeated goo-driven renders use constant-time class lookups; live-card order and slick-membership changes create a fresh projection. Removal collapses height, padding, margins and opacity; horizontal borders are temporarily painted as inset shadows to avoid the browser's minimum 1px border producing a final 2px jump. The coordinator recalculates disappearing flex-gap compensation on interruption, including when subsequent removals empty a column. Changes to effective filter values settle movement and departing snapshots immediately (catalogue refreshes that recreate equivalent filter objects do not); reduced motion skips retention or finishes active effects. Teardown clears presentation state and effects.

`useBoardMotion` batches layout work for the current render. Before rendering it captures displayed movement geometry and unfinished box-effect styles before cancelling either effect. After rendering it prepares all creation effects and temporarily applies their final styles to measure final destinations/column heights, then restores the styles and starts creation. Same-column transforms are calculated against creation's starting layout so growing space is not counted twice; floating cards and column heights use the measured final layout. Interrupted effects retarget from their displayed positions over a fresh 200 ms. Draft opening/cancellation and saved-card handoff join this same pass through semantic calls from inline creation; focus and typing remain immediate. The movement snapshot also captures column scroll offsets before rendering. Restore those offsets before preparing effects unless a newly focused input has intentionally revealed a different viewport, and restore scrolled ancestors after temporary endpoint styles are removed, so native scroll anchoring/clamping cannot move the starting viewport. Queued scroll events with an unchanged restored offset do not settle movement; changed offsets retain the normal scroll-settling behaviour. Pending work is discarded on teardown.

Movement detection uses canonical column membership/order, independently of filtering. `findMovedColumns` compares cards present in both snapshots so creation/removal alone and duplicate HTTP/realtime delivery do not trigger movement. Creation effects prepare their geometry separately from playback; unfinished effects retain their completion callback and can rebind to a remounted card after crossing columns.

Within-column effects transform the real card. Cross-column effects use an inert floating copy in the board's motion layer while the real destination card reserves its layout space. Column heights animate with their cards so shrinking columns do not prematurely clip displaced neighbours. The coordinator passes prepared box-effect endpoints to movement; floating copies mirror padding, borders and opacity along with their own position/size, so a departing copy can collapse completely and a growing copy retains its current box styles. The HTML renderer includes travelling cards in its existing board-level slick/selection goo groups, preserving connections to stationary cards. Lite keeps its existing column SVG layers: cross-column travelling cards have no slick/selection goo until they land. Movement owns a reactive map of floating card elements. Both goo layers observe that map directly and rebuild tracking when an override is registered, replaced or removed. Goo resolves registered floating elements before actual column cards, taking clipping from the resolved element; floating copies carry only their moving-card identity rather than a duplicate `data-card-id`. Landing returns tracking to the actual destination card and Lite rebuilds its column goo. Slick membership in the store stays intact throughout. `BoardSurface` resolves the renderer mode once and shares it with motion and rendering. Scrolling/resizing settles movement; offscreen cross-column endpoints remain clipped rather than floating over headers. Reduced motion skips/finishes effects, and filtering/removal/teardown cleans up copies and restores destination visibility. Both renderer modes can be exercised in Chromium, but Safari behaviour still needs real-world browser testing.

Key files:

- `BoardOil.Web/src/board/components/BoardSurface.vue`
- `BoardOil.Web/src/board/composables/useBoardMotion.ts`
- `BoardOil.Web/src/board/composables/useBoardColumnClipping.ts`
- `BoardOil.Web/src/board/utils/gooViewport.ts`
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
  - The top always clips below the header. Card and goo shapes continue offscreen without shrinking bridges as cards leave view.
  - A column clips at its content bottom only when it fills the available stack height (after the tail drop target and gap). Short column backgrounds are not lower viewport boundaries.
  - `useBoardColumnClipping` observes layout, opens short-column overflow while preserving scrollbar spacing, and shares board clip bounds and short-column extensions with every renderer. Full-height columns retain native scrolling. Board-level goo is clipped after filtering/drawing; do not paint bottom masks over it.
  - Native Smooth/Lite outlines keep their scrolling coordinates through short/full-height transitions. Verify card width, horizontal position, filtering and resize transitions when changing this handling.
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

## Experimental Renderers

- `authStore.experimentalFeaturesEnabled` gates the floating board-page picker. Opted-in users start with `Smooth` and can select `Liquid`; the choice is local to the mounted board surface. Opting out restores the existing Full/Lite browser selection.
- `ExperimentalGooLayer.vue` measures actual rendered cards, including `useBoardMotion`'s floating-card overrides. Ordinary scrolling projects cached positions by the changed column offset. Smooth’s column-local SVG outlines scroll natively with the cards; only cross-column bridges and travelling cards redraw on the board canvas in the scroll handler. Liquid keeps its existing animation-frame callback and uninterrupted simulation cadence. Resizing, catalogue/content changes and the motion runner's `gooLayoutVersion` invalidate those measurements. Theme changes refresh colours. Keep projected scrolling equivalent to a full DOM measurement. The hidden Full/Lite slick layer receives no descriptors while the experimental renderer is active; selection remains separate.
- The independent implementation lives in `src/board/goo`; it has no runtime dependency on `experiments/goo-playground`. Keep the saved playground available for visual comparison.
- `GooScene` owns Liquid simulation, residency, settling and drawing. `SmoothLayers` owns the active Smooth renderer, including the reduced-motion fallback. `ExperimentalGooLayer.vue` selects between them; production Full/Lite rendering remains separate.
- Smooth caches unfiltered SVG outlines in each column’s scrolling content, rebuilding only when column-local geometry, membership or colours change. Card-to-card blending retains inward side indents; bounded canvas patches add the softer cross-column shoulders. Keep these SVGs out of flex layout and verify that they do not extend column scroll ranges. Smooth scrolling does not transport dormant Liquid particles; switching back projects them to the current geometry. Liquid retains nearby particles across scrolling and card movement; membership changes rebuild only affected slick pools. `LiquidResidency` seeds newly revealed regions within the 120px simulation margin and discards particles beyond a further 40px retention margin. Card-relative coverage prevents reseeding retained sections during independent column scrolling or small reversals; returning distant sections get the default coating and bridge seeds. Its solver uses merged stack bodies while its minimum visible coating keeps individual card indents.
- Liquid runs its fixed-step physics in one module worker, with at most one batch in flight. The UI thread owns card geometry, scroll transport, viewport residency, outline drawing and settling. Transfer double-precision particle snapshots; apply returned physics displacement on top of intervening scroll movement. Ignore results for replaced pools, changed layout revisions, sleeping pools, paused modes and disposed boards; never replace current coordinates with an old worker snapshot. Newly seeded particles wait for the next batch. Pause pending results for Smooth, reduced motion and hidden tabs; terminate the worker on teardown. Keep a synchronous fallback for unavailable or failed workers. Verify animation cadence, continuous independent column scrolling, mode changes, idle shutdown and the emitted worker bundle when changing this boundary.
- Liquid uses numeric neighbour-cell keys with a stride derived from each active pool’s width, so wide boards cannot alias buckets. Its contour grid retains the global 3px sampling phase but covers only the slick’s cards/particles inside the padded viewport; retain kernel/coating padding and verify clipped outlines when changing those bounds.
- Contour tracing shares reusable numeric scratch buffers across synchronous calls. Grid edges map to compact contour nodes with two neighbours each; preserve connection order and the saddle decision so loop coordinates, starts and winding remain unchanged. Return independent point arrays because sleeping outlines outlive subsequent traces. Scratch storage grows with the largest grid/contour encountered, rather than allocating a full graph for every pool on every frame.
- Liquid reuses awake raw fields and resolved outlines when both the applied solver step count and the scene's surface revision are unchanged. Surface revisions include scroll changes as well as layout/viewport changes; they are separate from the worker's layout revision. A changed rival field still invalidates the shared contact outline. Per-pool card samples cache double-precision coating/clearance values by card geometry and clipped world-grid coordinates, independently of particle-field extents. Drop samples for cards no longer used by a field. Colour-only redraws reuse geometry.
- Liquid resolves overlapping render fields together into touching contact boundaries. Bounded density plus card-coating strength decides ownership, independently of paint order; the particle pools and solver remain separate. Cache both the raw field and its resolved outline. An awake neighbour may change a sleeping pool’s contact edge without waking its solver, and removing a neighbour must restore the unclipped outline. Only intersecting grids take part in contact resolution. Within their actual shared area, eight small relaxation passes soften the ownership boundary while pinning stronger card coatings. Both colours use the same cached pair boundary; outer silhouettes, empty pockets and particle physics remain unchanged. The relaxation stencil contains only movable contact cells, not the whole overlapping rectangle.
- Liquid leaves a 6px rendered clearance around cards with no slick, so passing liquid cannot imply membership. This is separate from contact ownership between coloured slicks.
- Liquid freezes particles in the outer retention margin and discards more distant particles, sleeps each settled visible outline and applies a ten-second running-time limit since its last geometry change. Hidden tabs and Smooth do not spend that allowance. Once all slicks sleep, its animation loop stops. Reduced motion renders the Smooth outline without running the solver.
- Scroll wakes defer settling comparisons until 100ms of running time without another geometry-changing scroll. Then start a fresh visible-outline observation window. Drawing, physics and the ten-second limit continue normally; layout wakes keep immediate observation.
- The existing selection highlight remains separate from experimental slick rendering. Keep selection behaviour and slick membership independent.

## Known Constraints

- Goo is frontend-only visual behavior.
