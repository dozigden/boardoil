# Pinia Store Guidance

Read this before adding or changing frontend stores, their callers, or realtime integration. These conventions apply across board, shared and system stores. The board and card store refactors establish the pattern for future work; use the examples below to understand the responsibilities, rather than copying every detail of an existing store.

General frontend rules remain in [Frontend.md](Frontend.md). Validation workflows remain in [Testing.md](Testing.md).

## Ownership and Boundaries

Give each kind of shared data one authoritative store. Put the operations that maintain it in that store, and have callers use those operations.

Current examples:

| Owner | Responsibility |
| --- | --- |
| `authStore` | Session lifecycle, bootstrap, role checks and CSRF setup. |
| `boardCatalogueStore` | Board selection catalogue and board creation. |
| `boardStore` | Active board metadata and columns, board context initialization/cleanup, realtime connection and event routing. |
| `cardStore` | Card entities, column membership, card ordering and card operations. |
| `tagStore`, `cardTypeStore`, `slickStore` | Shared board catalogues and their operations. |
| `attachmentStore` | Open attachment context, uploads/deletions and coordinating attachment changes with thumbnails. |
| `cardAttachmentThumbnailStore` | Board thumbnail state and refreshes. |
| `commentStore` | Card comment state and operations. |
| `uiFeedbackStore` | Shared user-facing errors, warnings and toasts. |

- Keep transient interaction state in the component or composable that owns the interaction. For example, `useBoardCardDragDrop` owns dragging state and passes an explicit card ID to `cardStore.moveCard`.
- Derive values from their authoritative state where possible. `boardStore.currentBoardId` is computed from `boardShell`; it is not a second independently writable ID.
- A child store may need its own active context identity to scope requests and reject stale responses. That has a separate lifecycle purpose; do not replace it with a parent-store dependency merely to eliminate an ID field.
- Compose read models instead of maintaining duplicate mutable copies. `boardStore` combines its column metadata with cards from `cardStore` when exposing the board.
- Keep dependencies directed: the context owner coordinates child stores; child stores own their data. Do not introduce circular dependencies to recover context or trigger cleanup.

## API Actions and State Updates

The usual flow is:

1. A view or composable calls a store action with explicit inputs.
2. The action calls the typed API client.
3. After success and the relevant context checks, it applies the response through shared state-update actions.
4. Failures use the established feedback path.

- Keep request actions, state-update actions and pure helpers recognisable as separate responsibilities. A server delete action and a local removal action do different work.
- Local API responses and realtime events should use the same upsert/removal implementation. Avoid parallel `...FromRealtime` implementations of the same state changes.
- Use upsert for insertion and replacement when their state work is the same. A distinct creation action is justified when it owns additional work, such as loading attachment thumbnails.
- Keep actions explicit and predictable: load, create, update, delete, move, upsert and remove. Prefer names that describe the actual scope; `unlinkCardsFromColumns` returns a column membership map excluding the specified cards. It leaves card entities and attachments untouched.
- Pass primitive IDs when identity is all an operation needs, or an ID collection for a collection operation. Do not introduce single-field removal models.
- Preserve the return contract callers use to decide whether to close an editor, display validation or continue another operation.
- Use small helpers for genuine shared work. First check whether callers can use an existing action directly before adding a generic request/orchestration wrapper.

Uppercase `VERB_NOUN` naming for state mutations is a separate proposal tracked in story #890. It has not been adopted; retain current camelCase names until that decision is made.

## Collection Operations

- Implement collection upserts/removals once. Single-entity state-update wrappers, where useful, pass `[entity]` or `[id]` to the collection implementation.
- Bulk operations call collection state updates directly. Do not loop over single-entity actions that clone and publish the full maps for every entity.
- Build the next maps once, apply the collection, and replace each affected state map once. Preserve prior state and input arrays when constructing replacements.
- Update membership for the whole collection and sort each destination collection once. In `cardStore`, unlink incoming card IDs from their old columns before adding them to their destination columns.
- Give ordering one owner. `boardStore` sorts columns; `cardStore` sorts cards during snapshot replacement and incremental updates. Do not sort cards again in a board mapper before passing them to the card store.
- Deduplicate IDs and handle empty collections at the action that performs the work. Thin delegates such as `bulkMoveCards` leave that preparation to `bulkEditCards`.
- Where the collection API supports the single-item operation, route single-item callers through it with a collection of one. The card editor uses `deleteCards([cardId])` and `archiveCards([cardId])`; redundant single-card store and frontend API methods were removed, including their demo equivalents.
- Check endpoint semantics when consolidating callers, including authorization, missing entities and return values. Bulk-delete idempotency is a known deferred bug (#891); routing card deletion through the bulk action was explicitly approved before that fix. Do not generalise that bug into the desired deletion contract.

## Related State and Side Effects

The action that applies a domain change owns its related state updates. Views and realtime registrations should not repeat a sequence of calls across stores to complete that change.

- Card removal owns card-cache removal, column membership changes, thumbnail cleanup and notification to the attachment store. Delete, archive, transfer and realtime deletion share this path.
- Attachment additions/deletions go through `attachmentStore.added`/`removed`. These update the matching open context and coordinate board thumbnail updates. Thumbnail work must still happen when no card is open or a different card is open.
- `cardStore.applyCreatedCard` combines upsert with thumbnail refresh. Realtime creation, duplication and archive restoration use it. Ordinary local creation uses upsert because it has no copied attachments to fetch; do not introduce extra requests solely to make callers look uniform.
- Keep cleanup context-aware. Removing a live card must not clear attachment state for another board/card or an archived snapshot.
- Await related asynchronous work when the caller depends on its completion. A wrapper must return the delegated promise so its caller can await it.

## Catalogue Authority and API Contracts

- Distinguish authoritative catalogue data from convenient embedded read data. A rich entity response does not become a second authority for shared labels, metadata or styling.
- Document which store drives live rendering when a contract exposes both embedded metadata and a shared catalogue. Use catalogue updates to avoid rewriting every entity that embeds the metadata.
- `slickStore` owns the slick catalogue. Card read payloads embed a full `slick`; card responses and slick create/update responses use `upsertSlick` to insert or replace catalogue entries by ID. Card writes continue to use `slickName`.
- Keep full-replacement edits easy to round-trip. Preserve unchanged fields without projection-heavy conversion simply because the read model is richer.
- Follow the API trust rules in [Frontend.md](Frontend.md): rely on backend contracts and avoid speculative fallbacks or re-deriving guaranteed fields. Keep necessary context/stale-response guards narrowly tied to the lifecycle they protect.

## Context Lifecycle and Loading

- The shared board layout owns board workspace initialization. Reuse the initialized context when moving between board and archive views for the same board.
- A child view should not reload a shared snapshot or catalogue just because it remounts. Realtime remains connected while visiting the archive, so shared metadata continues to receive resync updates.
- Run independent loads together. The tag, card-type and slick catalogue loads use `Promise.all`; keep genuinely dependent stages ordered.
- Capture the operation's context before awaiting a request and verify it before applying a result that could belong to a previous board or selection. Use request versions for load/replacement lifecycles where a newer request or disposal must invalidate an older response.
- The context owner owns teardown. `boardStore.dispose` disconnects realtime and clears its board context, including cards, comments, attachment state, thumbnails and board catalogues. App-level cleanup calls that owner instead of separately disposing the same child stores.
- Child-store disposal clears owned data and invalidates pending loads so late responses cannot repopulate a disposed context. Board snapshot load failures also clear the owned context through the shared cleanup path.
- Keep board-scoped and application-scoped lifecycles distinct. Leaving a board workspace does not imply clearing every store in the application.
- Do not add extra snapshot reloads or event buffering to cover hypothetical startup races. Introduce recovery work for a concrete requirement or observed failure; the rare initial-load/realtime race was explicitly left outside these refactors.

## Realtime Routing

- `boardStore` owns realtime connect/disconnect for the board workspace.
- Guard board-scoped callbacks at registration with the shared `forCurrentBoard` helper, then delegate to the owning action. Reuse the same handler when events have the same effect, such as card updated and moved events.
- Keep registrations small. A callback should not become a second home for card, attachment or thumbnail orchestration.
- Keep genuine multi-step recovery explicit. Resync reloads the board snapshot and board catalogues, checks context, and reloads related open state as needed; reconnect uses the resync path.
- Apply ordinary entity events incrementally. Reserve snapshot replacement for initialization and recovery rather than every mutation or navigation.

## Busy State and Feedback

- Expose operation progress through store busy/loading state and restore it in `finally`. Keep initial context loading distinct where it controls the workspace loading UI.
- Reuse the store's established request/feedback helper. Shared failures normally go through `uiFeedbackStore`; preserve deliberate inline-validation or operation-specific feedback paths.
- Successful operations clear feedback through the established path. When composing loaders, let the loaders report their own failures rather than adding duplicate error toasts in the orchestration layer.
- Treat feedback ownership for concurrent operations as an explicit design concern. These refactors do not establish a new error-list/identifier model or claim to solve concurrent error clearing.
- Keep session and permission checks centralised through auth/store/router integration.

## Validation and Reference Implementations

- Use [Testing.md](Testing.md) for commands and test scope. Store behaviour belongs in Vitest; browser tests cover relevant user journeys and browser integration.
- When changing batch updates, cover ordering, column membership, retained unrelated entities, input/prior-state preservation and related cleanup. Verify one publication per affected map when batching is the behaviour being changed.
- When changing context lifecycle, cover disposal and late responses as well as normal loads. When moving interaction ownership, cover cancellation and the next interaction.
- Reuse existing coverage where it already exercises the behaviour. Avoid tests that merely mirror a helper's implementation.
- Keep HTTP and demo implementations aligned with the shared `BoardApi` contract. Remove unused methods and update affected tests when consolidating API callers.

Reference files:

- [boardStore.ts](../BoardOil.Web/src/board/stores/boardStore.ts): context ownership, derived identity, guarded realtime routing and catalogue loading.
- [cardStore.ts](../BoardOil.Web/src/board/stores/cardStore.ts): collection updates, ordering and shared card side effects.
- [attachmentStore.ts](../BoardOil.Web/src/board/stores/attachmentStore.ts): open-context handling and thumbnail coordination.
- [useBoardCardDragDrop.ts](../BoardOil.Web/src/board/composables/useBoardCardDragDrop.ts): interaction ownership with explicit store-action inputs.
