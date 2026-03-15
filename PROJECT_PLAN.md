# BoardOil v1 Project Plan

## Summary
BoardOil is a lightweight self-hosted Kanban app for small homelab teams.
v1 will be a single-board product with realtime collaboration (board updates + typing indicators), no auth, SQLite persistence, and single-container deployment.

## Product Decisions (Locked)
- Product name: `BoardOil`
- Target: small trusted self-hosted setups
- Board model: exactly one board in v1
- Auth model: none in v1 (trusted network only)
- Persistence: SQLite via EF Core
- Realtime scope: card/column changes + typing indicators
- Conflict model: last write wins
- Delete semantics: idempotent across the solution (repeating deletes, including missing target IDs, returns success)
- Development workflow: local host development
- Packaging: single Docker image
- Network safety default: bind localhost (`127.0.0.1`) unless explicitly overridden
- Title validation policy: all title fields use the same validation rules (trimmed required value, max length 200, allowed chars limited to alphanumeric plus spaces and `. , - _ & ' ( ) ! ? : /`)

## Implementation Plan
### 1) Backend (.NET)
- Build ASP.NET Core app with:
  - JSON API for board/column/card operations
  - SignalR hub for realtime events
  - static file hosting for Vue frontend
- Add EF Core + SQLite with database file at `/data/boardoil.db`.
- Apply migrations safely on startup (idempotent).
- Expose config for host/port/data path and explicit LAN exposure override.

### 2) Data Model (v1)
- `Board`: single seeded record
- `Column`: board-linked, ordered by `position`
- `Card`: column-linked, ordered by sort key (position is computed in API DTOs), includes title/description
- No create/delete board UI or API in v1.

### Position Handling (Current Backend Rules)
- `position = null` means append to end for creates and cross-column card moves.
- Provided positions are clamped into valid range (`0..count` for inserts, `0..count-1` for reorders).
- Column ordering is persisted with stable integer `position` values and reindexed after inserts/deletes/reorders.
- Card ordering is persisted via sortable keys; API responses expose zero-based computed `position` values from that order.

### 3) Frontend (Vue + TypeScript)
- One board view with ordered columns and cards.
- CRUD for columns/cards.
- Drag/drop card movement and reorder.
- Typing indicator UI for card fields (title/description context).

### 4) Realtime Behavior
- Broadcast create/update/delete/move events for columns and cards.
- Broadcast typing presence with TTL-based expiry.
- On reconnect, client fetches latest snapshot and reconciles state.

### 5) Packaging and Ops
- Multi-stage Docker build producing one runtime image.
- Persist DB with mounted `/data` volume.
- Document safe exposure defaults and override behavior.

### 6) Development Environment
- Standardize local development with host tooling for .NET SDK and Node.js.
- Ensure frontend and backend can run together in local development.
- Keep runtime production image concerns separated from local development setup.

## API and Event Surface (v1)
### REST API
- `GET /api/board`
- `POST /api/columns`
- `PATCH /api/columns/{id}`
- `DELETE /api/columns/{id}`
- `POST /api/cards`
- `PATCH /api/cards/{id}`
- `DELETE /api/cards/{id}`

### SignalR
- Server events: `ColumnCreated`, `ColumnUpdated`, `ColumnDeleted`, `CardCreated`, `CardUpdated`, `CardDeleted`, `CardMoved`, `TypingChanged`
- Client events: `TypingStarted(cardId, field, userLabel)`, `TypingStopped(cardId, field, userLabel)`

## Test Plan
- Backend:
  - CRUD + ordering invariants for columns/cards
  - cross-column move behavior
  - persistence across restart
  - localhost default binding behavior
- Realtime:
  - two-client sync for create/edit/move/delete
  - typing indicator lifecycle (start/stop/TTL expiry)
  - concurrent edit stability under last-write-wins
- Frontend:
  - core workflow happy path
  - drag/drop state consistency
  - reconnect snapshot recovery

## v1 Done Criteria
- A single Docker image runs BoardOil end-to-end.
- Data persists through `/data` volume.
- Small team can collaborate live on one board.
- Setup and exposure guidance is documented in README.
- Baseline automated tests pass.

## Out of Scope for v1
- Auth and permissions
- Multi-board UX
- Labels/due dates/swimlanes
- Mobile-specific optimization

## Status Log
- 2026-03-10: Planning baseline created.
- 2026-03-10: Replaced with decision-complete BoardOil v1 plan.
