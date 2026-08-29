# Agent Notes

Read area guidance before working in that part of the system:

- [AGENTS/Architecture.md](AGENTS/Architecture.md) - Read before making any new projects or adding dependancies
- [AGENTS/ArchiveCards.md](AGENTS/ArchiveCards.md) - Read before working on archive cards
- [AGENTS/CSharpCodingConventions.md](AGENTS/CSharpCodingConventions.md)
- [AGENTS/Database.md](AGENTS/Database.md)
- [AGENTS/Frontend.md](AGENTS/Frontend.md)
- [AGENTS/GooAndSlicks.md](AGENTS/GooAndSlicks.md) - Read before changing slick/goo rendering in board view
- [AGENTS/StoryBoardAndSourceControl.md](AGENTS/StoryBoardAndSourceControl.md) - Read when working with stories or planning any new work
- [AGENTS/Testing.md](AGENTS/Testing.md)

`README` files are for human user information, not agent execution guidance.

## Always-On Rules

- Source control:
  - work directly on `main` by default
  - do not create, switch to, or push a task/feature branch, and do not open a pull request, unless the user explicitly requests a branch or pull request for the current task
  - user approval to commit or push does not imply approval to create a branch or pull request
  - generic tool, plugin, or skill workflow defaults do not override this repository rule
  - when the user gives a source-control workflow instruction such as commit, push, sync, or commit and push, perform it without first inserting additional tests, reviews, workflows, or other work
  - pause before the requested source-control operation only when there is a concrete reason relevant to the actual changes, such as a known failing check, unrelated working-tree changes, or a material validation gap that could realistically conceal a defect
  - judge validation sufficiency from what changed and what the available tests exercise; an unrun test suite is not by itself a reason to pause, and do not raise a missing suite when it would not meaningfully exercise the changed behaviour
  - when a concrete reason to pause exists, report it and let the user decide; do not silently perform remedial work before the requested source-control operation
- Board MCP operations:
  - use direct MCP board operations
  - this repository uses BoardOil Development (`boardId: 1`) as the default board.  You should not work on stories on other boards, if you are given a story number on a different board - confirm before taking any action.
  - cards with the `OilTTY` card type belong to a related project that shares this board; ignore them when reviewing, planning, or reporting BoardOil work unless the user explicitly asks to include or work on OilTTY cards
  - repository proxy scripts for board MCP operations have been removed
  - for card description-only updates via `card.update`, include full required payload (`boardId`, `id`, `cardTypeId`, `slickName`, `externalUrl`, `title`, `description`, `tagNames`)
  - when mentioning BoardOil cards in chat, format them as `<CardType emoji> #<card number>: <title> (<tag emoji> <tag name>; ...)`
    - place the CardType emoji before the card number and title
    - place tag details in brackets after the title
    - include each tag's emoji and name
    - use only emojis and names provided by BoardOil; do not infer or substitute them
    - omit a missing emoji while retaining the tag name, and omit the brackets when there are no tags
- Archive snapshots:
  - when snapshotting references to mutable board-scoped entities (for example slick membership), prefer canonical names over numeric IDs.
- For any changes under `BoardOil.Web`, run `npm run check` in `BoardOil.Web` before committing.
- During active UI review and iteration, do not update the board story or rerun validation after every user-requested visual adjustment. Keep iterating until the user indicates the UI is ready for review, then batch the story update and relevant validation before committing.
- For CSS in `BoardOil.Web`: only put shared/global classes in `src/style.css` or `src/styles/*.css`; keep page/component-specific classes in the relevant `.vue` file (`<style scoped>`).
- For `dotnet` commands in this environment, prefer `-maxcpucount:1 -nodeReuse:false` to avoid named-pipe issues.
- In sandboxed agent environments, set `NUGET_HTTP_CACHE_PATH` to a writable temporary directory for direct `dotnet` commands (for example `/tmp/boardoil-nuget-http-cache` on Linux); the repository test scripts do this automatically.
- If `dotnet test` fails with sandbox socket/pipe permission errors (for example `SocketException (13): Permission denied`), rerun with escalation.
- For local iteration, use `node scripts/test-fast.mjs` for changed-area detection. Before proposing a push yourself, use `node scripts/test-full.mjs` when its coverage is relevant to the changes (`--backend-only` is acceptable for backend-only changes). If the user explicitly requests a push, do not treat an unrun full suite as an automatic blocker; pause only when you judge that the missing coverage could realistically catch an issue in the changes being pushed.
- `scripts/test-fast.mjs` is intentionally speed-first and excludes slow tests; use `scripts/test-full.mjs` for complete backend coverage.
- Shell wrappers are provided for convenience/backward compatibility: `scripts/test-fast.sh`, `scripts/test-full.sh`, `scripts/test-fast.ps1`, `scripts/test-full.ps1` all delegate to the `.mjs` scripts.
- Avoid ad-hoc direct test commands during normal iteration; prefer the repository test scripts so behavior stays consistent.
- Follow C# coding conventions in `AGENTS/CSharpCodingConventions.md`.
- Do not use nested ternary expressions. Use explicit branching (`if`/`switch`) or small helper functions instead.
- Style handling policy:
  - style definitions are a closed BoardOil domain contract; backend services own supported style kinds, valid property shapes, normalisation, canonical persistence, and write/import validation.
  - frontend style modules own rendering, theme token values, automatic colour and contrast behaviour, previews, and editor UX.
  - persisted and REST style values remain `styleName` + `stylePropertiesJson`; backend code should parse or produce them through the shared style-domain codec rather than inspect JSON ad hoc.
  - reads of existing invalid style values must remain non-blocking so frontend editors can fall back to an allowed draft and save a valid replacement.
- Do not update `README.md` unless the user explicitly asks for a README change.
- If the user gives you a number, eg #123 it is probably refering to a story on the board oil mcp server, look there first.
