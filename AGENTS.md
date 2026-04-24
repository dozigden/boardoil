# Agent Notes

Read area guidance before working in that part of the system:

- [AGENTS/Architecture.md](AGENTS/Architecture.md)
- [AGENTS/CSharpCodingConventions.md](AGENTS/CSharpCodingConventions.md)
- [AGENTS/Database.md](AGENTS/Database.md)
- [AGENTS/Frontend.md](AGENTS/Frontend.md)
- [AGENTS/StoryBoardAndSourceControl.md](AGENTS/StoryBoardAndSourceControl.md)
- [AGENTS/Testing.md](AGENTS/Testing.md)

`README` files are for human user information, not agent execution guidance.

## Always-On Rules

- Board MCP operations:
  - use direct MCP board operations
  - repository proxy scripts for board MCP operations have been removed
  - for card description-only updates via `card.update`, include full required payload (`boardId`, `id`, `cardTypeId`, `title`, `description`, `tagNames`)
- For any changes under `BoardOil.Web`, run `npm run check` in `BoardOil.Web` before committing.
- For CSS in `BoardOil.Web`: only put shared/global classes in `src/style.css` or `src/styles/*.css`; keep page/component-specific classes in the relevant `.vue` file (`<style scoped>`).
- For `dotnet` commands in this environment, prefer `-maxcpucount:1 -nodeReuse:false` to avoid named-pipe issues.
- If `dotnet test` fails with sandbox socket/pipe permission errors (for example `SocketException (13): Permission denied`), rerun with escalation.
- Follow C# coding conventions in `AGENTS/CSharpCodingConventions.md`.
- Do not update `README.md` unless the user explicitly asks for a README change.
