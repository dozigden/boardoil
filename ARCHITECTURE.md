# Architecture Notes

This file captures the intended structure of BoardOil so future work follows consistent patterns.

## Layering

- `BoardOil.Api`
  - HTTP transport and routing only.
  - Authn/authz policy wiring.
  - Endpoint handlers should stay thin and delegate business rules.
- `BoardOil.Services`
  - Business logic and invariants.
  - Service interfaces in `Abstractions/`.
  - Service implementations in `Implementations/`.
  - DTO/request contracts in `Contracts/`.
  - Entity-to-contract mapping in `Mappings/`.
- `BoardOil.Ef`
  - EF Core entities, DbContext, migrations.

## Established Backend Pattern

Use this flow for domain features:

1. Endpoint maps route and auth policy.
2. Endpoint calls service interface (`I*Service`).
3. Service uses repository interfaces (`I*Repository`) for persistence.
4. Service maps entities to contracts through mapping extensions (`To*Dto`).
5. Service returns `ApiResult`/`ApiResult<T>`.

Current examples:
- Columns/Cards already follow this pattern.
- User admin now follows this pattern.

## Auth Boundary Split

Treat auth/session and user administration as separate domains:

- Auth/session concerns:
  - `register-initial-admin`, `login`, `refresh`, `logout`, `me`, `csrf`
  - token/cookie lifecycle
- User admin concerns:
  - list users
  - create user
  - update role
  - activate/deactivate
  - last-admin protection rules

Reason:
- Different responsibilities and change rates.
- Cleaner policy and test boundaries.
- Easier future extraction of `IAuthService` without entangling admin CRUD.

## User Admin Implementation Decisions (2026-03-16)

Implemented:
- `IUserAdminService` + `UserAdminService`
- `IUserRepository` + `UserRepository`
- `UserAdminContracts` (`ManagedUserDto`, user-admin requests)
- `ToManagedUserDto` mapping extension
- Auth endpoints delegate `/api/users*` to `IUserAdminService`

Conventions adopted:
- Prefer implicit `ApiResult<T>` conversions for successful returns:
  - `return dto;`
  - `return list;`
- Prefer `ApiErrors.*` for failure returns in service methods:
  - `return ApiErrors.BadRequest(...)`
  - `return ApiErrors.NotFound(...)`

## Frontend Auth Notes

- Auth state is managed in `authStore`.
- Router guard enforces auth/admin access.
- HTTP layer attaches CSRF header for state-changing requests when token is present.

## Realtime Notes

- Realtime events are incremental updates.
- Clients should resync after reconnect.
- Delivery is best-effort; source of truth is API state.

## Open Hardening Follow-ups

See `TODO.md` for active auth follow-ups (hub authorization, secure cookies, typing identity trust, 401 handling behavior).
