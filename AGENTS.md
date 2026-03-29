# Agent Notes

Read area guidance before working in that part of the system:

- [AGENTS/Architecture.md](AGENTS/Architecture.md)
- [AGENTS/Database.md](AGENTS/Database.md)
- [AGENTS/Frontend.md](AGENTS/Frontend.md)
- [AGENTS/StoryBoardAndSourceControl.md](AGENTS/StoryBoardAndSourceControl.md)

`README` files are for human user information, not agent execution guidance.

## Always-On Rules

- For any changes under `BoardOil.Web`, run `npm run check` in `BoardOil.Web` before committing.
- For `dotnet` commands in this environment, prefer `-maxcpucount:1 -nodeReuse:false` to avoid named-pipe issues.
- If `dotnet test` fails with sandbox socket/pipe permission errors (for example `SocketException (13): Permission denied`), rerun with escalation.
- Prefer British English spellings in code/contracts/schema names unless external APIs require otherwise.
- Repositories should remain entity-level CRUD/query only; orchestration belongs in services.
- In tests, prefer a single clear `Arrange` / `Act` / `Assert` flow per test.
- Do not update `README.md` unless the user explicitly asks for a README change.
