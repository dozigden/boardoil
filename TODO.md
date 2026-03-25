# TODO

## Features

### Frontend Legal Notices

Add a frontend Legal / Open Source notices view and reference `BoardOil.Web/THIRD_PARTY_NOTICES.md`.

## Quality

### Test Structure Audit

Review all test projects and align tests to a single clear `Arrange` / `Act` / `Assert` flow per test method.
Split tests that currently contain multiple independent `Act` / `Assert` phases into focused tests.

### Repository Review

Revisit `CardRow` usage in `CardRepository` and simplify the card mapping shape if possible.

### Repository Structure Follow-up

Refactor repositories toward one repository per entity type (or aggregate boundary), and introduce a generic `RepositoryBase<TEntity>` for shared CRUD/query patterns where it improves clarity.

### Transaction Scope Audit

Review all `IDbContextScope.Transaction(...)` usages and confirm each one is justified versus a standard single-save scope.

### Card Tag Handling Improvements

Review `CardService` tag handling to reduce duplication and ambiguity between request-order tags and stored normalised tags.
Consider a single shared helper for tag normalisation/ordering and a batched repository lookup to avoid per-tag existence queries.
