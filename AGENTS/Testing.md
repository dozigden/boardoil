# Testing Guidance

This document defines how to split test coverage between service tests and API integration tests so we avoid duplicated effort and keep runtime under control.

## Goals

- Keep confidence high without duplicating the same business-rule matrix in multiple layers.
- Keep API integration suites focused on HTTP contract and runtime wiring behaviour.
- Keep business logic and edge-case matrices focused in service tests.

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

## Naming Expectations

Use names that make ownership clear:

- Contract/middleware intent in API tests (for example `...ShouldReturnBadRequest`, `...ShouldReturnForbidden`, `...ShouldMark...`)
- Rule/invariant intent in service tests (for example `...ShouldReassign...`, `...ShouldBeAtomic...`)
