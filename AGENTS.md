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

## Documentation Scope

- Do not update `README.md` unless the user explicitly asks for a README change.

## MCP Board Workflow

- For MCP board actions, prefer `./scripts/board-mcp.sh` instead of ad-hoc `curl` one-liners.
- Production board auth lives in global Codex config, not this repo:
  - Read `~/.codex/config.toml` under `[mcp_servers.boardoil]` for the MCP `url` and PAT value.
  - Pass both explicitly to `board-mcp.sh` (for example `--mcp-url ... --token ...`) when `BOARDOIL_MCP_TOKEN` is not set.
- To avoid repeated elevation prompts, request one persistent approval prefix rule for:
  - `["./scripts/board-mcp.sh"]`
- Prefer script subcommands (`board-get`, `board-cards`, `card-move`, `card-description-set`) for routine board updates.
- When a session is board-driven, treat the board as the source of truth for execution order and progress.
- Before starting implementation:
  - Read the board state with MCP.
  - Confirm the next story to work from `Todo` (unless the user explicitly reprioritises).
- As work starts on a story:
  - Move the story card to `In Progress` before code changes.
  - Add a brief status line in the card description so progress is visible without opening chat history.
- During implementation:
  - Keep the story description updated at meaningful milestones (design decided, implementation done, tests running, etc.).
  - Use concise, outcome-focused updates.
- When the story is complete:
  - Update the card description with final outcomes (what changed + validation done).
  - Move the card to `Done`.
- If temporary/spike/smoke cards are created during testing, move them out of `Todo` once they are no longer actionable.
