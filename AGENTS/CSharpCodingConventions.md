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
- For style payloads (`stylePropertiesJson`), keep runtime backend validation transport-level unless a story explicitly requires otherwise:
  - validate allowed `styleName`
  - validate `stylePropertiesJson` is valid JSON object text
  - do not add service/import schema checks for style-internal keys

## Test Style

- Prefer a single clear `Arrange` / `Act` / `Assert` flow per test method.
- If a test needs multiple independent act/assert phases, split it into separate tests.
