# Frontend Guidance

This file documents the current frontend store pattern and behaviour conventions.

## Store Pattern

BoardOil frontend state uses Pinia stores with a small set of focused stores:

- `authStore`
  - Session lifecycle, bootstrap state, role checks (`isAuthenticated`, `isAdmin`), csrf token setup.
- `boardStore`
  - Active board state, column/card operations, optimistic/incremental state updates, realtime integration.
- `boardCatalogueStore`
  - Board list retrieval and create operations for board selection/navigation context.
- `tagStore`
  - Tag catalogue load/create/update/delete and tag lookup helpers.
- `uiFeedbackStore`
  - User-facing error message state shared across stores/views.

## Typical Data Flow

1. View/component calls a store action.
2. Store action calls typed API client (`createBoardApi`, `createAuthApi`, etc.).
3. Store updates local state from API result.
4. Store writes user-visible errors via `uiFeedbackStore` on failure.

## Behaviour Conventions

- Keep actions explicit and predictable (load, create, update, delete, move).
- Use shared `busy` flags for operation progress.
- Clear feedback errors on successful operations.
- Route guards and auth checks should remain centralised through store/router integration.
- Prefer canonical route paths in the router. Do not add legacy/back-compat or convenience redirect/alias routes; 
- Avoid introductory “wall of text” at the top of pages; interfaces should generally stand on their own unless short context is essential.
- Use the shared button styles in `BoardOil.Web/src/styles/buttons.css` (`.btn`, `.btn--secondary`, `.btn--danger`, etc.) instead of creating one-off button variants per view.
- Prefer `.btn.btn--tab` for tab toggles.
- Prefer `.btn.btn--toolbar` for markdown toolbar actions/mode toggles.
- Prefer `.btn.btn--menu-item` for menu-panel button actions.
- For management/list pages with repeated row patterns (for example Boards/Tags/Users/Columns manager views), use the shared entity-row styles in `BoardOil.Web/src/styles/entity-rows.css`:
  - page shell: `.entity-rows-page` (or `.entity-rows-page--compact` for narrower pages)
  - list container: `.entity-rows-list`
  - row container: `.entity-row`
  - row content/action slots: `.entity-row-main`, `.entity-row-actions`
  - row title/badge helpers: `.entity-row-title`, `.entity-row-badges`, `.entity-row-action-icon`
- Keep this entity-row pattern as the default for new management-style rows; only add view-specific row classes when behaviour or visuals are genuinely unique.
- Keep shared/global classes in shared stylesheets (`BoardOil.Web/src/style.css`, `BoardOil.Web/src/styles/*.css`) only when they are reused across views/components or define app-wide layout/theme behavior.
- Keep page-specific/component-specific classes in the relevant Vue file (`<style scoped>`), not in global stylesheets.
- Keep non-`.btn` controls limited to intentional interaction widgets:
  - chip/suggestion controls inside tag editors (`.tag-pill-remove`, `.card-tag-editor-suggestion`)
  - inline title edit trigger (`.card-title-button`)

## Contract and Store Authority

- Be explicit about which client store is authoritative for a given kind of data.
- A denormalised field on an entity read model can exist for convenience without becoming the authoritative source for live rendering or mutation flows.
- When backend contracts expose both:
  - rich embedded read data for convenience
  - and a separate catalogue/store with the same underlying metadata
  document and preserve which one the UI should treat as authoritative.
- Prefer this pattern when it avoids broad fan-out updates:
  - integrations can consume rich embedded data in one hit
  - the web app can still rely on a dedicated catalogue store for live shared metadata such as styling or labels that affect many entities at once
- Keep full-update form/edit flows cheap:
  - if writes remain full replacement updates, stores/components should be able to round-trip unchanged fields without projection-heavy conversion work
  - avoid introducing client complexity just because a richer read model exists
- For future entity/store design (not just tags), treat “authoritative source” and “convenience read shape” as separate design decisions and record both when adding new contracts.

## Realtime Conventions

- `boardStore` owns realtime connect/disconnect for board workspace views.
- Realtime handlers apply incremental upserts/removals.
- On resync events, reload board snapshot to recover consistency.
