# C# Coding Conventions

Use these conventions for C# code in `BoardOil.*` projects.

## Naming and Language

- Prefer British English spellings in code, contracts, and schema names unless integrating with an external API that requires a specific spelling.

## Method Design

- Prefer return values over `out` parameters unless interop or performance constraints make `out` unavoidable.
- Keep helper method names aligned with behaviour (for example, validation helpers should validate rather than validate-and-transform).
- Do not use nested ternary expressions. Prefer explicit `if`/`switch` branches for readability.

## Service and Repository Boundaries

- Keep repository classes focused on entity-level CRUD/query responsibilities.
- Keep business orchestration and policy logic in service-layer code.
- Treat style definitions as a closed domain contract:
  - use the shared style-domain codec for parsing, validation, normalisation, defaults, and canonical serialisation
  - apply strict validation to new writes and compatibility parsing only at documented legacy/import boundaries
  - keep entity-specific allowed style kinds in the owning service
  - do not inspect style JSON ad hoc outside the shared codec or explicit migration/upgrade code

## Test Style

- Prefer a single clear `Arrange` / `Act` / `Assert` flow per test method.
- If a test needs multiple independent act/assert phases, split it into separate tests.
