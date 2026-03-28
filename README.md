# BoardOil

BoardOil is a self-hosted Kanban board.

> Warning
> This project is just me playing. Nothing to see here.

## Quick Start (Docker)
Run from repository root:

```bash
docker compose up --build -d
```

Open: `http://localhost:5000`

Stop:

```bash
docker compose down
```

## Required Production Setup
Before real deployment, set a strong signing key:
- `BoardOilAuth__SigningKey` must be at least 32 characters.
- Set `BoardOilAuth__AllowInsecureCookies: "false"` and configure https.

## Data & Persistence
Docker compose uses a named volume:
- `boardoil-data` mounted at `/data`
- SQLite file: `/data/boardoil.db`

Back up by copying the SQLite file while the app is stopped.

## Troubleshooting
`401` right after login (especially from another device):
- If running plain HTTP with secure cookies, session cookies will not be sent.
- Enable `BoardOilAuth__AllowInsecureCookies=true` for HTTP mode.

`CSRF validation failed` during setup/login flow:
- Clear `boardoil_access`, `boardoil_refresh`, `boardoil_csrf` cookies and retry.

## Development
Restore/install:

```bash
dotnet restore BoardOil.slnx
cd BoardOil.Web && npm install
```

Run backend + frontend:

```bash
./dev-startall.sh
```

## MCP for Agents

Use the MCP server from your agent client as a local command (stdio).

### 1) Build the MCP server

```bash
dotnet build BoardOil.Mcp.Server/BoardOil.Mcp.Server.csproj -maxcpucount:1 -nodeReuse:false
```

### 2) Configure your agent to start the MCP server

Set the database connection string first:

```bash
export BOARDOIL_MCP_CONNECTION_STRING="Data Source=/data/boardoil.db"
```

Then configure your agent MCP client to launch:

- command: `dotnet`
- args: `run --project BoardOil.Mcp.Server/BoardOil.Mcp.Server.csproj -maxcpucount:1 -nodeReuse:false`

Example MCP client entry:

```json
{
  "mcpServers": {
    "boardoil": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "BoardOil.Mcp.Server/BoardOil.Mcp.Server.csproj",
        "-maxcpucount:1",
        "-nodeReuse:false"
      ],
      "env": {
        "BOARDOIL_MCP_CONNECTION_STRING": "Data Source=/data/boardoil.db"
      }
    }
  }
}
```

### 2.1) Optional: Realtime forwarding for MCP-driven changes (non-Docker)

By default, MCP uses a no-op board event publisher (tool writes still persist, but SignalR clients will not get live push updates from MCP actions).

To enable realtime push from MCP:

1. Start API with an internal relay key configured:

```bash
export BoardOilInternal__McpEventRelayApiKey="replace-with-a-long-random-secret"
dotnet run --project BoardOil.Api/BoardOil.Api.csproj -maxcpucount:1 -nodeReuse:false
```

2. Start MCP with matching relay settings:

```bash
export BOARDOIL_MCP_CONNECTION_STRING="Data Source=boardoil.dev.db"
export BOARDOIL_MCP_EVENTS_API_BASE_URL="http://127.0.0.1:5000"
export BOARDOIL_MCP_EVENTS_API_KEY="replace-with-a-long-random-secret"
dotnet run --project BoardOil.Mcp.Server/BoardOil.Mcp.Server.csproj -maxcpucount:1 -nodeReuse:false
```

Notes:
- If either `BOARDOIL_MCP_EVENTS_API_BASE_URL` or `BOARDOIL_MCP_EVENTS_API_KEY` is set without the other, MCP startup will fail fast.
- If neither is set, MCP starts normally with realtime forwarding disabled.

### 3) Verify tools exposed to the agent
Tool names are defined in:
- `BoardOil.Mcp.Contracts/ToolNames.cs`
- `BoardOil.Mcp.Contracts/ToolCatalogue.cs`

Schemas and payload contracts are defined in:
- `BoardOil.Mcp.Contracts/Schemas/ToolSchemas.cs`
- `BoardOil.Mcp.Contracts/Models.cs`

## Docker Compose Status (MCP)
`docker compose up --build` currently builds and runs only the `boardoil` API/web service.
It does **not** run `BoardOil.Mcp.Server` yet.

Current compose/runtime files:
- `docker-compose.yml` defines only `boardoil`.
- `Dockerfile` publishes only `BoardOil.Api` and starts `BoardOil.Api.dll`.

If you want MCP in Compose, add a separate `boardoil-mcp` service and publish target for `BoardOil.Mcp.Server`.
