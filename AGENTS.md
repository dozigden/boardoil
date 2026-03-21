# Agent Notes

## Frontend Checks

- For any changes under `BoardOil.Web`, run `npm run check` in `BoardOil.Web` before committing.

## .NET Command Reliability

- In this environment, prefer single-process MSBuild flags for `dotnet` commands to avoid named-pipe failures:
  - `-maxcpucount:1 -nodeReuse:false`
- If `dotnet test` fails with socket/pipe permission errors in sandbox (for example `SocketException (13): Permission denied`), rerun with escalation so the testhost can open local communication sockets.

## Language Convention

- Prefer British English spellings in code, contracts, and migration/schema names (for example `NormalisedName` rather than `NormalizedName`) unless integrating with an external API that requires a specific spelling.

## Repository Boundaries

- Repository classes should expose entity-level CRUD operations only (create/read/update/delete for whole records).
- Repositories should not contain business orchestration logic such as `CreateMissing`, policy decisions, validation rules, merge/dedupe behavior, or style-specific field mutation workflows.
- Service-layer code is responsible for higher-level workflows (for example resolving missing entities, applying partial update intent, and coordinating multiple repository calls).

## Test Structure

- In tests, prefer a single clear `Arrange`, `Act`, `Assert` flow per test method.
- If a test needs multiple independent `Act`/`Assert` phases, split it into separate tests.
