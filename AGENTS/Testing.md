# Testing Guidance

This document defines how to split test coverage between service tests and API integration tests so we avoid duplicated effort and keep runtime under control.

## Goals

- Keep confidence high without duplicating the same business-rule matrix in multiple layers.
- Keep API integration suites focused on HTTP contract and runtime wiring behaviour.
- Keep business logic and edge-case matrices focused in service tests.

## Browser Test Principles

Browser tests protect critical user journeys; they are not the default place to test business-rule permutations.

### Structure by Responsibility

- Specs describe the user journey and expected outcome.
- Fixtures provide authenticated contexts, reusable capabilities, and isolated test data.
- UI drivers encapsulate repeated interactions and stable locators.
- API helpers arrange prerequisites quickly through supported application APIs.
- Runner and configuration code owns temporary storage, ports, server lifecycle, profiles, timeouts, and diagnostic artifacts.

Keep browser-test code organised by those responsibilities. Do not mix environment orchestration, API setup, detailed selectors, and outcome assertions into each spec.

### Maintainability Rules

1. Keep specs readable.
   - A spec should read as a short user journey.
   - Do not expose API payloads, server orchestration, or detailed selectors in the scenario.

2. Use small workflow objects.
   - Introduce focused drivers such as `BoardPage` or `CardEditor` only where interactions are reused or complex.
   - Do not mirror the Vue component tree, create a universal base page, or build page-object inheritance hierarchies.
   - Keep meaningful outcome assertions visible in the spec.

3. Prefer semantic locators.
   - Prefer roles, labels, and visible text.
   - Use an existing stable semantic attribute where appropriate.
   - Add a test id only when no meaningful accessible selector exists.
   - Do not couple tests to styling classes, DOM ancestry, generated ids, or positional selectors.

4. Keep every test independent.
   - Tests must run individually and in any order.
   - Use fresh browser contexts and unique, readable test data.
   - Do not depend on state created by another test.
   - Use a disposable database for the run and remove the complete environment afterwards instead of relying on fragile per-test cleanup.

5. Use fixtures for capabilities.
   - Good fixtures provide capabilities such as an authenticated page, test board, second browser context, or API client.
   - Scenario-specific arrangements remain visible in the spec.
   - Avoid fixtures whose names hide an entire preconstructed scenario.

6. Wait for behaviour, never time.
   - Use Playwright's web-first assertions, URLs, API responses, and observable application state.
   - Do not use arbitrary sleeps to stabilise tests.

7. Maintain a strict smoke budget.
   - A test belongs in the GitHub smoke profile only when its failure materially undermines confidence in everyday BoardOil use.
   - Broader coverage belongs in the Jenkins regression profile.

8. Treat flakiness as a defect.
   - Begin with zero retries.
   - Do not hide flakes with blanket retries or larger global timeouts.
   - Diagnose failures with traces and fix the cause.
   - Any temporarily disabled browser test must reference a board bug.

9. Test at the lowest appropriate layer.
   - Business-rule matrices, API authorization permutations, and store edge cases remain in backend or Vitest suites.
   - Browser tests are reserved for browser behaviour and multi-layer composition such as focus/caret handling, cookies, routing, drag-and-drop, canvas, realtime, and critical user journeys.

10. Make failures readable.
    - Use concise `test.step` phases for longer journeys.
    - Retain actionable traces and screenshots on failure.
    - Prefer behavioural assertions over broad visual snapshots.

### Running the Browser Smoke Profile

- Install Chromium once from `BoardOil.Web` with `npx playwright install chromium`.
- Run the profile from `BoardOil.Web` with `npm run test:e2e:smoke`.
- Name smoke specs `*.smoke.spec.ts`; the smoke command selects only that suffix.
- Pass Playwright CLI options after `--` when narrowing or repeating a run.
- The runner creates its own temporary SQLite database, image directory, API port, and frontend port, then removes the temporary environment.
- Browser tests remain separate from `test-fast.mjs`, `test-full.mjs`, and the normal Vitest run.

### Running the Browser Regression Profile

- Run every browser spec from `BoardOil.Web` with `npm run test:e2e:regression`.
- Princess Posse Jenkins owns the scheduled regression run; GitHub Actions continues to run only the smoke profile.
- Name broader or longer-running specs `*.regression.spec.ts` so their intended ownership is obvious.
- Set `BOARDOIL_E2E_JUNIT_OUTPUT` when the caller needs a JUnit XML report; normal local and GitHub smoke runs retain the list reporter only.
- Slow clean-build hosts may set `BOARDOIL_E2E_STARTUP_TIMEOUT_MS` to a positive integer; the default API/frontend readiness deadline remains 60000 ms.

## Local Fast Loop

Use repository scripts to keep local feedback fast and consistent:

- `node scripts/test-fast.mjs`
  - default mode is changed-area detection from git diff
  - runs only impacted fast suites/checks (API, Dev orchestrator, Services, Web)
  - excludes slow API integration classes by design
  - supports suite overrides: `--api-only`, `--dev-only`, `--services-only`, `--web-only`, `--backend-only`, `--full`
  - supports output overrides: `--compact`, `--verbose`
- `node scripts/test-full.mjs`
  - CI-like full local run (backend restore/build/tests + web check/test)
  - supports suite overrides: `--backend-only`, `--web-only`
  - supports output overrides: `--compact`, `--verbose`

Convenience wrappers:
- `scripts/test-fast.sh`, `scripts/test-full.sh`, `scripts/test-fast.ps1`, and `scripts/test-full.ps1` delegate to the `.mjs` scripts.

Recommended flow:

1. During implementation, run `node scripts/test-fast.mjs`.
2. If you changed only one area and want an explicit lane, use `node scripts/test-fast.mjs --api-only`, `--dev-only`, `--services-only`, `--web-only`, or `--backend-only`.
3. Before pushing risky backend/API auth/MCP/migration changes, run `node scripts/test-full.mjs --backend-only`.
4. Before pushing mixed backend+frontend changes, run full `node scripts/test-full.mjs`.
5. Avoid ad-hoc direct test commands during normal iteration; use the repository scripts so behavior stays consistent.

## Test Output Modes

The repository test scripts default to compact output when they detect an agent or CI environment (`CI`, `GITHUB_ACTIONS`, `CODEX_CI`, `CODEX_THREAD_ID`, or `CLAUDECODE`). Compact mode:

- hides successful restore/build/check command output
- prints concise pass summaries for xUnit and Vitest test runs
- replays full stdout/stderr for any failed command before reporting the failed suite
- passes `--no-progress --no-ansi` to xUnit v3 test applications
- lets Vitest use its `agent` reporter with `silent: 'passed-only'`
- suppresses Vite's successful build asset table in agent/CI runs while preserving warnings and errors

Use `--verbose` or `BOARDOIL_TEST_OUTPUT=verbose` when investigating flaky tests, slow tests, or build output details. Use `--compact` or `BOARDOIL_TEST_OUTPUT=compact` to force low-noise output in a normal local shell.

`npm test` and the Vite build wrapper in `BoardOil.Web` also detect these environment variables, so direct web test/build runs stay compact in agent/CI environments. Prefer the repository scripts for normal iteration because they also choose the right backend/frontend lanes.

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
- `BoardOil.Dev/**` or `BoardOil.Dev.Tests/**` -> `BoardOil.Dev.Tests`
- `BoardOil.Web/**` -> `npm run check` and `npm test` (in `BoardOil.Web`)
- shared backend layers (`BoardOil.Contracts`, `BoardOil.Abstractions`, `BoardOil.Ef`, `BoardOil.Data.Abstractions`, `BoardOil.Mcp.Contracts`) -> API + Services tests
- global tooling/workflow files (`BoardOil.slnx`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `NuGet.config`, `.github/workflows/*`, `scripts/*`) -> API + Dev orchestrator + Services tests
- unknown/non-code paths default to no tests in fast mode

## Naming Expectations

Use names that make ownership clear:

- Contract/middleware intent in API tests (for example `...ShouldReturnBadRequest`, `...ShouldReturnForbidden`, `...ShouldMark...`)
- Rule/invariant intent in service tests (for example `...ShouldReassign...`, `...ShouldBeAtomic...`)

## Board Package Schema Compatibility

- Treat currently supported board package schema versions as a compatibility set unless a story explicitly introduces versioned divergence.
- When adding fields to board package payloads without a schema bump, add service tests that exercise those fields across all currently supported schema versions.
- For BoardOil board package imports where schema `1` and `2` are intentionally equivalent, preserve regression coverage for both versions before introducing `3`.
