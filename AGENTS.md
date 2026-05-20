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

- Board MCP operations:
  - use direct MCP board operations
  - this repository uses BoardOil Development (`boardId: 1`) as the default board.  You should not work on stories on other boards, if you are given a story number on a different board - confirm before taking any action.
  - repository proxy scripts for board MCP operations have been removed
  - for card description-only updates via `card.update`, include full required payload (`boardId`, `id`, `cardTypeId`, `slickName`, `title`, `description`, `tagNames`)
- For any changes under `BoardOil.Web`, run `npm run check` in `BoardOil.Web` before committing.
- For CSS in `BoardOil.Web`: only put shared/global classes in `src/style.css` or `src/styles/*.css`; keep page/component-specific classes in the relevant `.vue` file (`<style scoped>`).
- For `dotnet` commands in this environment, prefer `-maxcpucount:1 -nodeReuse:false` to avoid named-pipe issues.
- If `dotnet test` fails with sandbox socket/pipe permission errors (for example `SocketException (13): Permission denied`), rerun with escalation.
- For local iteration, prefer `scripts/test-fast.sh` (changed-area detection); before push, run `scripts/test-full.sh` (`--backend-only` is acceptable for backend-only changes).
- Follow C# coding conventions in `AGENTS/CSharpCodingConventions.md`.
- Do not use nested ternary expressions. Use explicit branching (`if`/`switch`) or small helper functions instead.
- Style handling policy:
  - backend runtime validation for `stylePropertiesJson` is transport-only: valid `styleName` + valid JSON object text.
  - backend services/import must not enforce style-internal JSON schema keys (`backgroundColor`, `presetIndex`, `textColorMode`, etc.).
  - style-internal interpretation belongs to frontend style modules; backend should only inspect style JSON internals in migration/upgrade code.
- Do not update `README.md` unless the user explicitly asks for a README change.
- If the user gives you a number, eg #123 it is probably refering to a story on the board oil mcp server, look there first.
