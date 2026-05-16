# Testing Guidance

This document defines how to split test coverage between service tests and API integration tests so we avoid duplicated effort and keep runtime under control.

## Goals

- Keep confidence high without duplicating the same business-rule matrix in multiple layers.
- Keep API integration suites focused on HTTP contract and runtime wiring behaviour.
- Keep business logic and edge-case matrices focused in service tests.

## Local Fast Loop

Use repository scripts to keep local feedback fast and consistent:

- `scripts/test-fast.sh`
  - default mode is changed-area detection from git diff
  - runs only impacted test/check suites (API, Services, Web)
  - supports overrides: `--api-only`, `--services-only`, `--web-only`, `--backend-only`, `--full`
- `scripts/test-full.sh`
  - CI-like full local run (backend restore/build/tests + web check/test)
  - supports `--backend-only` and `--web-only`

Recommended flow:

1. During implementation, run `scripts/test-fast.sh`.
2. Before pushing risky backend/API auth/MCP/migration changes, run `scripts/test-full.sh --backend-only`.
3. Before pushing mixed backend+frontend changes, run full `scripts/test-full.sh`.

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

`scripts/test-fast.sh` maps changed paths to suites:

- `BoardOil.Services/**` -> `BoardOil.Services.Tests`
- `BoardOil.Api/**` or `BoardOil.Api.Tests/**` -> `BoardOil.Api.Tests`
- `BoardOil.Web/**` -> `npm run check` and `npm test` (in `BoardOil.Web`)
- shared backend layers (`BoardOil.Contracts`, `BoardOil.Abstractions`, `BoardOil.Ef`, `BoardOil.Data.Abstractions`) -> API + Services tests
- global tooling/workflow files (`BoardOil.slnx`, `Directory.*`, `global.json`, `NuGet.config`, `.github/workflows/*`, `scripts/*`) -> API + Services tests

## Naming Expectations

Use names that make ownership clear:

- Contract/middleware intent in API tests (for example `...ShouldReturnBadRequest`, `...ShouldReturnForbidden`, `...ShouldMark...`)
- Rule/invariant intent in service tests (for example `...ShouldReassign...`, `...ShouldBeAtomic...`)
