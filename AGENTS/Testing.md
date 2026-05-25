# Testing Guidance

This document defines how to split test coverage between service tests and API integration tests so we avoid duplicated effort and keep runtime under control.

## Goals

- Keep confidence high without duplicating the same business-rule matrix in multiple layers.
- Keep API integration suites focused on HTTP contract and runtime wiring behaviour.
- Keep business logic and edge-case matrices focused in service tests.

## Local Fast Loop

Use repository scripts to keep local feedback fast and consistent:

- `node scripts/test-fast.mjs`
  - default mode is changed-area detection from git diff
  - runs only impacted fast suites/checks (API, Services, Web)
  - excludes slow API integration classes by design
  - supports overrides: `--api-only`, `--services-only`, `--web-only`, `--backend-only`, `--full`
- `node scripts/test-full.mjs`
  - CI-like full local run (backend restore/build/tests + web check/test)
  - supports `--backend-only` and `--web-only`

Convenience wrappers:
- `scripts/test-fast.sh`, `scripts/test-full.sh`, `scripts/test-fast.ps1`, and `scripts/test-full.ps1` delegate to the `.mjs` scripts.

Recommended flow:

1. During implementation, run `node scripts/test-fast.mjs`.
2. Before pushing risky backend/API auth/MCP/migration changes, run `node scripts/test-full.mjs --backend-only`.
3. Before pushing mixed backend+frontend changes, run full `node scripts/test-full.mjs`.
4. Avoid ad-hoc direct test commands during normal iteration; use the repository scripts so behavior stays consistent.

## Ownership by Layer

- Service tests (`BoardOil.Services.Tests`) own:
  - business rules and invariants
  - validation matrices and edge-case permutations
  - ordering/reassignment semantics
  - import/export/archive behaviour details
  - authorization decision logic at service boundary
- API integration tests (`BoardOil.Api.Tests`) own:
  - endpoint contract shape (route, status code, envelope shape)
  - auth boundary behaviour (anonymous/forbidden/allowed)
  - model binding/serialization/middleware behaviour (CSRF, cookies, multipart binding)
  - API-only cross-cutting wiring (Swagger schema exposure, endpoint registration)

## API Integration Scope Rules

For each endpoint family, target one test per endpoint concern, not one test per business-rule permutation.

Keep in API integration:

- one happy-path contract test per route family
- one or more permission boundary tests where policy differs by actor
- one validation mapping test per unique request-shape pattern
- middleware/wiring tests that cannot be proven in service tests

Move to service tests (or avoid adding in API integration):

- additional permutations of the same validation rule
- detailed state transition matrices already covered in services
- repeated business-rule checks that only differ by data setup

## Duplication Check Before Adding Tests

Before adding an API integration test:

1. Search `BoardOil.Services.Tests` for an equivalent business rule.
2. If service coverage exists, add API integration coverage only if we still need one of:
   - endpoint contract proof
   - permission boundary proof
   - middleware/binding proof
3. If none apply, add/extend service tests instead of API integration tests.

## Pruning Guidance

When runtime becomes slow, prune in this order:

1. remove duplicated API integration business-rule permutations
2. keep API contract/auth/middleware representatives intact
3. keep migrations/realtime/MCP integration suites intact unless explicitly superseded

## Auth and MCP Policy Patterns

For API auth coverage, prefer policy-level sentinels over endpoint-by-endpoint `401/403` assertions:

- Keep a small set of representative auth boundary tests per policy shape (`authenticated`, `admin`, board membership variations).
- Avoid duplicating anonymous/forbidden assertions on every endpoint when the route uses an already-covered policy.
- Keep one endpoint metadata guard test that inspects mapped `/api/*` routes and fails if protected routes are missing authorization metadata.

For security-behaviour suites, keep integration depth where transport/auth semantics are the behaviour:

- Keep MCP bearer/path/auth-mode suites as integration tests.
- Keep PAT scope behaviour and machine-token auth flow suites as integration tests.
- Keep CSRF and internal API-key boundary checks as integration tests.

## Changed-Area Mapping

`scripts/test-fast.mjs` maps changed paths to suites:

- `BoardOil.Services/**` -> `BoardOil.Services.Tests`
- `BoardOil.Api/**` or `BoardOil.Api.Tests/**` -> `BoardOil.Api.Tests`
- `BoardOil.Web/**` -> `npm run check` and `npm test` (in `BoardOil.Web`)
- shared backend layers (`BoardOil.Contracts`, `BoardOil.Abstractions`, `BoardOil.Ef`, `BoardOil.Data.Abstractions`, `BoardOil.Mcp.Contracts`) -> API + Services tests
- global tooling/workflow files (`BoardOil.slnx`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `NuGet.config`, `.github/workflows/*`, `scripts/*`) -> API + Services tests
- unknown/non-code paths default to no tests in fast mode

## Naming Expectations

Use names that make ownership clear:

- Contract/middleware intent in API tests (for example `...ShouldReturnBadRequest`, `...ShouldReturnForbidden`, `...ShouldMark...`)
- Rule/invariant intent in service tests (for example `...ShouldReassign...`, `...ShouldBeAtomic...`)

## Board Package Schema Compatibility

- Treat currently supported board package schema versions as a compatibility set unless a story explicitly introduces versioned divergence.
- When adding fields to board package payloads without a schema bump, add service tests that exercise those fields across all currently supported schema versions.
- For BoardOil board package imports where schema `1` and `2` are intentionally equivalent, preserve regression coverage for both versions before introducing `3`.
