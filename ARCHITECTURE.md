# Architecture Notes

This file captures the intended structure of BoardOil so future work follows consistent patterns.

## Layering

- `BoardOil.Api`
  - HTTP transport and routing only.
  - Authn/authz policy wiring.
  - Endpoint handlers should stay thin and delegate business rules.
- `BoardOil.Abstractions`
  - Cross-project auth abstractions and shared auth/user entity types.
  - Keep this focused; do not turn it into a generic dumping ground.
- `BoardOil.Services`
  - Business logic and invariants.
  - Organize by top-level feature folder (for example `Auth/`, `Board/`, `Column/`, `Card/`).
  - Prefer flat feature folders (avoid extra `Abstractions/Contracts/Implementations/Mappings` nesting inside each feature).
  - Keep service-layer abstractions that depend on service contracts in `BoardOil.Services` (example: `IAuthService`).
- `BoardOil.Ef`
  - EF Core DbContext, migrations, and repository implementations.
  - Concrete repositories that use EF should live here (example: `AuthRepository` in `Repositories/`).

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

## Desired Structure Baseline (Before Next Refactors)

Use Auth as the template for upcoming feature refactors:

- `BoardOil.Services/Auth` is the model feature folder shape (flat files, single feature namespace).
- `BoardOil.Abstractions/Auth` holds reusable auth interfaces used across projects:
  - `IAuthRepository`
  - `IAccessTokenIssuer`
  - `IPasswordHashService`
- `BoardOil.Services/Auth/IAuthService` remains in Services for now because it depends on service contracts and `ApiResult`.
- `BoardOil.Ef/Repositories` contains EF-backed repository implementations (currently `AuthRepository`).

When refactoring board/column/card, follow this same structure direction unless we explicitly revise it first.

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
