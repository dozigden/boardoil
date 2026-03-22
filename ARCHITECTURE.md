# Architecture Notes

This file captures the current structure of BoardOil after the data-tier refactor.

## Solution Layers

- `BoardOil.Api`
  - HTTP transport, endpoint wiring, auth policies, and runtime hosting concerns.
  - Endpoints stay thin and delegate behaviour to services.
- `BoardOil.Contracts`
  - API contracts (`*Request`, `*Dto`, `ApiResult`, `ValidationError`) shared across API/services/tests.
- `BoardOil.Abstractions`
  - Cross-cutting service and infrastructure abstractions.
  - Includes ambient DbContext scope contracts (`IDbContextScopeFactory`, `IAmbientDbContextLocator`, etc.) and service interfaces (`I*Service`).
- `BoardOil.Persistence.Abstractions`
  - Persistence-facing contracts and EF CLR entity types.
  - Owns `Entity*` classes and repository interfaces (`IAuthUserRepository`, `ICardRepository`, `ITagRepository`, etc.).
  - Owns persistence enums such as `UserRole`.
- `BoardOil.Ef`
  - EF Core implementation details: `BoardOilDbContext`, migrations, ambient scope implementation, and repository implementations.
- `BoardOil.Services`
  - Business workflows, invariants, validation orchestration, mapping to contracts, and realtime event publishing.

## Request/Write Flow

1. API endpoint invokes a service (`I*Service`).
2. Service creates a DbContext scope (`Create()` for writes, `CreateReadOnly()` for reads).
3. Service runs validation and repository operations.
4. Service commits once with `scope.SaveChangesAsync()`.
5. Service returns `ApiResult`/`ApiResult<T>` (or implicit success conversions).

Default rule: use a single save per service operation. The explicit `IDbContextScope.Transaction(...)` path exists for exceptional multi-save cases and should be rare.

## Data Access Pattern

- Repositories are EF implementations in `BoardOil.Ef/Repositories` and inherit `RepositoryBase<TEntity>`.
- `RepositoryBase` resolves DbContext from the ambient scope locator and fails fast if no ambient scope exists.
- Repositories provide entity-level persistence operations and queries only.
- Repositories do not own commits and do not contain orchestration/policy logic.

## Entity and Table Conventions

- All EF CLR entity types are in `BoardOil.Persistence.Abstractions/Entities` and use the `Entity*` prefix.
- Database table names remain stable and are explicitly mapped in `OnModelCreating` via `ToTable(...)`.
- CLR naming and table naming are intentionally decoupled.

## Validation Boundaries

- Validators return `ValidationError` collections.
- Services convert validation failures to `ApiErrors.BadRequest(...)`.
- For cards, create/update validation includes both shape rules and data-backed checks (column/tag existence) in `CardValidator`.

## Domain Split: Auth vs User Admin

- Auth/session domain: register initial admin, login, refresh, logout, `me`, CSRF/session token lifecycle.
- User admin domain: list/create users, role changes, activation/deactivation, last-admin protection.
- Keep these responsibilities separate even though they both touch user persistence.

## Startup and Bootstrap

- Startup initialisation uses `IDbContextFactory` directly for migration/bootstrap compatibility outside request scopes.
- Initialisation flow:
  - migrate/ensure-created database
  - seed default board/columns only when no board exists
- Bootstrap uses a single-save scope path.

## Testing Strategy

- Tests are wired through DI to match production registrations.
- Data tests use SQLite in-memory with shared open connections so ambient-created contexts share one in-memory database.
- Convention tests guard entity/table naming and mapping expectations.

## Frontend and Realtime Notes

- Frontend auth state is managed in `authStore`; router guards enforce auth/admin routes.
- HTTP client attaches CSRF header for state-changing requests when token is present.
- Realtime events are incremental and best-effort; clients should resynchronise after reconnect.

## Follow-ups

See `TODO.md` for current hardening and refactor follow-ups.
