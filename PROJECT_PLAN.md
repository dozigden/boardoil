# BoardOil v1 Project Plan

## Scope changes
These changes came out of review of current v1 status and are now incorporated into this plan.
- Typing presence is narrowed for v1:
  - Show only as a small `...` pill in two places:
    - board view, against the card title
    - card edit dialog title
  - Remove other frontend typing indicator uses.
  - Backend realtime behavior updated to card-level typing presence.
- Authentication is in scope for v1:
  - Local self-hosted accounts.
  - Roles: `admin`, `standard`.
  - API-level enforcement:
    - Only admins can manage column configuration (create/update/delete/reorder columns).
    - Only admins can add/manage users.
    - Board access requires authentication.
  - Future MCP integration should support auth without storing a user password in other projects.

## Summary
BoardOil is a lightweight self-hosted Kanban app for small homelab teams.  
v1 is a single-board product with realtime collaboration (board updates + typing presence), JWT auth with local accounts and roles, SQLite persistence, and single-container deployment.

## Product Decisions (Locked)
- Product name: `BoardOil`
- Target: small trusted self-hosted setups
- Board model: exactly one board in v1
- Auth model: local accounts + JWT in v1
- Token model: short-lived access token + refresh token via `HttpOnly` cookies
- CSRF model: enforce anti-CSRF protections for cookie-authenticated state-changing requests
- Bootstrap model: open registration only while user count is `0`; first user is admin
- Roles: `admin`, `standard`
- Persistence: SQLite via EF Core
- Realtime scope: card/column changes + card-level typing presence
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
  - JSON API for auth and user management
  - SignalR hub for realtime events
  - static file hosting for Vue frontend
- Add EF Core + SQLite with database file at `/data/boardoil.db`.
- Apply migrations safely on startup (idempotent).
- Expose config for host/port/data path and explicit LAN exposure override.
- Configure JWT issuing/validation and API authorization policies.
- Enforce CSRF protections for state-changing endpoints when using cookie auth.

### 2) Data Model (v1)
- `Board`: single seeded record
- `Column`: board-linked, ordered by sort key (position is computed in API DTOs)
- `Card`: column-linked, ordered by sort key (position is computed in API DTOs), includes title/description
- `User`: local account identity, password hash, role, active status
- `RefreshToken` (or equivalent): rotation/revocation support
- No create/delete board UI or API in v1.

### Position Handling (Current Backend Rules)
- `position = null` means append to end for creates and cross-column card moves.
- Provided positions are clamped into valid range (`0..count` for inserts, `0..count-1` for reorders).
- Column ordering is persisted via sortable keys; API responses expose zero-based computed `position` values from that order.
- Card ordering is persisted via sortable keys; API responses expose zero-based computed `position` values from that order.

### 3) Frontend (Vue + TypeScript)
- One board view with ordered columns and cards.
- CRUD for columns/cards based on role permissions.
- Drag/drop card movement and reorder.
- Login/logout/session handling for authenticated users.
- Typing indicator UI only in:
  - board card title pill
  - card editor dialog title pill

### 4) Realtime Behavior
- Broadcast create/update/delete/move events for columns and cards.
- Broadcast card-level typing presence with TTL-based expiry.
- On reconnect, client fetches latest snapshot and reconciles state.

### 5) Packaging and Ops
- Multi-stage Docker build producing one runtime image.
- Persist DB with mounted `/data` volume.
- Document safe exposure defaults and override behavior.
- Document auth bootstrap flow (first admin registration) and JWT config requirements.

### 6) Development Environment
- Standardize local development with host tooling for .NET SDK and Node.js.
- Ensure frontend and backend can run together in local development.
- Keep runtime production image concerns separated from local development setup.

## API and Event Surface (v1)
### REST API
- `GET /api/board` (admin, standard)
- `POST /api/columns` (admin)
- `PATCH /api/columns/{id}` (admin)
- `DELETE /api/columns/{id}` (admin)
- `POST /api/cards` (admin, standard)
- `PATCH /api/cards/{id}` (admin, standard)
- `DELETE /api/cards/{id}` (admin, standard)

### Auth API
- `POST /api/auth/register-initial-admin` (only when no users exist)
- `POST /api/auth/login`
- `POST /api/auth/refresh`
- `POST /api/auth/logout`
- `GET /api/auth/me`

### User Management API (admin only)
- `GET /api/users`
- `POST /api/users`
- `PATCH /api/users/{id}/role`
- `PATCH /api/users/{id}/status`

### SignalR
- Server events: `ColumnCreated`, `ColumnUpdated`, `ColumnDeleted`, `CardCreated`, `CardUpdated`, `CardDeleted`, `CardMoved`, `TypingChanged`
- Client events: `TypingStarted(cardId, userLabel)`, `TypingStopped(cardId, userLabel)`

## Test Plan
- Backend:
  - CRUD + ordering invariants for columns/cards
  - cross-column move behavior
  - persistence across restart
  - localhost default binding behavior
  - auth flows (login, refresh, logout, invalid credentials)
  - initial-admin registration allowed only when no users exist
  - role authorization matrix and API-level enforcement (`401`/`403`)
  - CSRF protections enforced for state-changing cookie-auth requests
- Realtime:
  - two-client sync for create/edit/move/delete
  - typing indicator lifecycle (start/stop/TTL expiry) at card level
  - concurrent edit stability under last-write-wins
- Frontend:
  - core workflow happy path
  - drag/drop state consistency
  - reconnect snapshot recovery
  - login/logout/session handling
  - role-based UI gating and forbidden-action handling
  - typing pills shown only in the two approved UI locations

## v1 Done Criteria
- A single Docker image runs BoardOil end-to-end.
- Data persists through `/data` volume.
- Small team can collaborate live on one board.
- JWT auth and role-based API enforcement are operational.
- Setup and exposure guidance is documented in README.
- Baseline automated tests pass.

## Out of Scope for v1
- External identity providers (OAuth/OIDC/LDAP/SAML)
- Multi-board UX
- Labels/due dates/swimlanes
- Mobile-specific optimization

## Status Log
- 2026-03-10: Planning baseline created.
- 2026-03-10: Replaced with decision-complete BoardOil v1 plan.
- 2026-03-16: Scope updated to include JWT auth/roles and narrowed typing indicator behavior.
