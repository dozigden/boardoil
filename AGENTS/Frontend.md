# Frontend Guidance

This file documents the current frontend store pattern and behaviour conventions.

## Dependency Installation Policy

- Run `npm ci` from `BoardOil.Web` to restore the committed lockfile. The project `.npmrc` sets `ignore-scripts=true` for local installs, CI and demo builds; Docker copies it before installing dependencies.
- Keep dependency lifecycle scripts disabled. There are currently no approved exceptions. If an essential dependency needs an install script, review its exact package/version and introduce a narrow allowlist with strict enforcement using a supported npm version; do not enable all scripts as a workaround.
- Explicit commands such as `npm run build`, `npm test` and `npx playwright install chromium` still execute. Automatic pre/post hooks are disabled, so required preparation belongs in the explicit command.
- This policy limits install-time execution. Build tools, tests and bundled browser code still execute dependencies and require dependency review and appropriate credential isolation.
- The current locked install scripts belong only to optional macOS `fsevents` packages. Keep scripts disabled on macOS too; validate file watching if those packages or the watcher tooling change.

### Dependency Updates and Release Age

- Use Node 24 and the exact npm version in `BoardOil.Web/package.json` under `engines` (currently 11.19.0). `engine-strict=true` rejects incompatible versions during installation. CI and Docker install npm from this same pin before `npm ci`; changing the pin requires checking the release-age behaviour again.
- To set up npm locally, run the following from `BoardOil.Web`, then check `npm --version`:

  ```sh
  BOARDOIL_NPM_VERSION="$(node -p "require('./package.json').engines.npm")"
  npm install --global --ignore-scripts "npm@$BOARDOIL_NPM_VERSION"
  ```

- The project `.npmrc` sets `min-release-age=7` in days. When resolving registry dependencies, `npm install` and `npm update` select eligible direct and transitive releases; a newly requested exact version inside the window fails. Use targeted updates such as `npm update <package>` and review the resulting manifest and lockfile diff.
- `npm ci` restores the reviewed lockfile without rechecking publication ages. An existing lockfile can therefore contain a newer release admitted under an exception. Lockfile review is part of the policy; regenerating or accepting a lockfile produced with different settings needs the same review.
- For an urgent security fix, record the advisory, exact package/version and reason in the story and obtain review of the exception. Use a one-command package-specific exception, for example `npm install <package>@<version> --min-release-age-exclude=<package>`. The package's dependencies retain the cooldown unless separately reviewed. Do not persist broad exclusions, disable the cooldown globally or use `--force` to bypass policy.
- Review newly introduced transitive packages, registry/source changes, integrity changes and install scripts alongside version changes. Direct Git, URL and local-file dependencies do not have the same npm-registry publication-age guarantee and require explicit source review.
- A cooldown delays admission; it does not prove a package is safe. Keep dependency scripts disabled and follow the existing review, validation and main-only source-control workflow.
- `scripts/npm-release-age-policy.test.mjs` exercises the installed npm CLI against a temporary local registry with controlled release dates. It covers fresh resolution, updates, exact-version rejection, a named exception, locked CI resolution and npm-version enforcement without downloading packages from the public registry.

### Docker Publication Gate

- The nightly and tagged-release Docker workflows call `.github/workflows/npm-publication-gate.yml` before either architecture builds or uploads an image.
- The gate audits the checked-out npm lockfile, including development, optional and peer dependencies, using the pinned npm version. High/critical findings and audit errors block publication; lower-severity findings remain visible in the job log.
- Run `npm audit --package-lock-only --include=dev --include=optional --include=peer --audit-level=high` from `BoardOil.Web` to reproduce the check. This command does not install application packages or apply fixes.
- Fix a blocking dependency or explicitly agree a narrowly scoped exception before changing the gate. An audit-service failure calls for a retry once the service recovers, not a silent bypass. The gate has no automatic override.
- Dependabot owns ongoing alerts. This gate does not run in ordinary CI, local builds or static demo publication, and does not scan NuGet or container OS packages.

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
- Do not use `$event` in Vue templates. Bind named handlers (for example `@change="onRoleChange"`) and parse event details in script.
- For delete operations where the request only needs identity, pass the primitive id (for example `userId`) instead of introducing single-field `*RemoveModel` types.
- Do not use nested ternary expressions in frontend code. Prefer explicit `if`/`switch` branches or helper functions.

## Style Architecture Conventions

- Treat style payloads as discriminated style models as early as possible in frontend flows.
- Keep style responsibilities split by module boundary:
  - persistence modules parse/serialize style JSON and handle fallback policy.
  - draft adapter modules handle UI draft defaults and mode-switch defaults.
  - renderer modules consume typed style models only and decide classes/inline style output.
- Keep semantic styles (`auto`, `presets`) class-driven in UI rendering. Avoid introducing inline color authority for those modes.
- Keep manual styles (`solid`, `gradient`) as the only modes that emit inline color presentation from user-authored values.
- If parsing fails, fall back to `auto` style with no properties.

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

## API Trust and Defensive Coding

- `slickStore` owns the shared slick catalogue. Card read payloads embed a full `slick` definition; card responses and slick create/update responses use the same `upsertSlick` action to insert or replace definitions by ID. Card writes continue to use `slickName`.

- Treat backend API contracts as authoritative for frontend read/write flows.
- Do not add client-side fallback/normalization code that re-derives API fields “just in case” without a concrete, current failure mode.
- If a guard is required, document the exact reason in code (what can fail, where it was observed) and keep the guard narrowly scoped.
- Prefer removing speculative defensive code when it only adds complexity and duplicates backend guarantees.

## Realtime Conventions

- `boardStore` owns realtime connect/disconnect for board workspace views.
- Realtime handlers apply incremental upserts/removals.
- On resync events, reload board snapshot to recover consistency.
