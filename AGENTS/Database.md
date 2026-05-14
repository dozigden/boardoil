# Database Guidance

This file explains how persistence is structured in BoardOil and the conventions agents should follow.

## Persistence Architecture

- `BoardOilDbContext` (in `BoardOil.Ef`) defines sets, table mappings, indexes, and relationships.
- Repository interfaces live in `BoardOil.Persistence.Abstractions`.
- Repository implementations live in `BoardOil.Ef/Repositories`.
- Services (in `BoardOil.Services`) orchestrate workflows and call repositories.

## Repository and Service Responsibilities

- Repositories should provide entity-level CRUD/query behaviour only.
- Repositories should not contain orchestration logic (`CreateMissing`, policy decisions, merge/dedupe strategy, validation rules).
- Services coordinate multi-step workflows, validation, policy, and cross-repository operations.
- Services own `SaveChangesAsync()` through db scopes.

## DbContext Scope Pattern

- Use `IDbContextScopeFactory.Create()` for write flows.
- Use `IDbContextScopeFactory.CreateReadOnly()` for read flows.
- Ambient scope resolution is required before repository use.
- Startup/bootstrap migrations use `IDbContextFactory` directly because request scopes are not active there.

## Conventions

- Entity classes are named with `Entity*` and live in `BoardOil.Persistence.Abstractions/Entities`.
- Tables are explicitly mapped in `OnModelCreating`.
- Primary keys use `int Id` consistently across tables/entities.
- Naming convention preference in code/contracts/schema terms is British English where applicable (for example `NormalisedName`).
- Add migrations for schema changes in `BoardOil.Ef/Migrations`; do not change schema without migration coverage.
- For runtime raw SQL write paths (for example `UPDATE`/`DELETE` via `ExecuteSqlRaw*` in services), ensure timestamp semantics are preserved:
  - if mutating entities that support `UpdatedAtUtc`, explicitly set `UpdatedAtUtc` in the SQL statement
  - if this is intentionally not required, document the reason in a code comment at the call site
- EF CLI commands should use the repo-managed local tool manifest (`.config/dotnet-tools.json`):
  - run `dotnet tool restore` from repo root before EF commands
  - prefer `dotnet tool run dotnet-ef ...` for explicit local-tool usage
  - in this environment, run `dotnet build -maxcpucount:1 -nodeReuse:false` first, then use `dotnet ef ... --no-build` to avoid intermittent EF internal build failures
