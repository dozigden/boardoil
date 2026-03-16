# Agent Notes

## Frontend Checks

- For any changes under `BoardOil.Web`, run `npm run check` in `BoardOil.Web` before committing.

## .NET Command Reliability

- In this environment, prefer single-process MSBuild flags for `dotnet` commands to avoid named-pipe failures:
  - `-maxcpucount:1 -nodeReuse:false`
- If `dotnet test` fails with socket/pipe permission errors in sandbox (for example `SocketException (13): Permission denied`), rerun with escalation so the testhost can open local communication sockets.
