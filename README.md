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
- If running `BoardOil.Mcp.Server` as a separate process/container, it must use the same `BoardOilAuth__Issuer`, `BoardOilAuth__Audience`, and `BoardOilAuth__SigningKey` values as the API.

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

## MCP HTTP Server
`BoardOil.Mcp.Server` is HTTP-only (streamable HTTP) and serves MCP on `/mcp`.

### MCP environment variables
- `BOARDOIL_MCP_HTTP_URLS` (default `http://0.0.0.0:5001`)
- `BOARDOIL_MCP_CONNECTION_STRING` (required)
- `BOARDOIL_MCP_EVENTS_API_BASE_URL` (optional; enables MCP->API realtime forwarding)

Optional relay key mode is still supported via `BOARDOIL_MCP_EVENTS_API_KEY`, but Docker uses source-IP trust by default and does not require a shared relay key.

### Non-Docker run
Start API first, then MCP:

```bash
dotnet run --project BoardOil.Api/BoardOil.Api.csproj -maxcpucount:1 -nodeReuse:false
```

```bash
export BOARDOIL_MCP_CONNECTION_STRING="Data Source=boardoil.dev.db"
export BOARDOIL_MCP_HTTP_URLS="http://127.0.0.1:5001"
export BOARDOIL_MCP_EVENTS_API_BASE_URL="http://127.0.0.1:5000"
export BoardOilAuth__Issuer="boardoil-dev"
export BoardOilAuth__Audience="boardoil-dev"
export BoardOilAuth__SigningKey="boardoil-dev-signing-key-change-me-1234567890"
dotnet run --project BoardOil.Mcp.Server/BoardOil.Mcp.Server.csproj -maxcpucount:1 -nodeReuse:false
```

### Machine token flow for MCP clients
MCP requires `Authorization: Bearer <jwt>` for all requests.

1. Bootstrap an initial admin (first run only):

```bash
curl -X POST http://localhost:5000/api/auth/register-initial-admin \
  -H "Content-Type: application/json" \
  -d '{"userName":"admin","password":"Password1234!"}'
```

2. Get machine tokens:

```bash
curl -X POST http://localhost:5000/api/auth/machine/login \
  -H "Content-Type: application/json" \
  -d '{"userName":"admin","password":"Password1234!"}'
```

3. Refresh machine tokens:

```bash
curl -X POST http://localhost:5000/api/auth/machine/refresh \
  -H "Content-Type: application/json" \
  -d '{"refreshToken":"<refresh-token>"}'
```

### MCP HTTP examples
List tools:

```bash
curl -X POST http://localhost:5001/mcp \
  -H "Authorization: Bearer <access-token>" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":"tools-list","method":"tools/list","params":{}}'
```

Create a card:

```bash
curl -X POST http://localhost:5001/mcp \
  -H "Authorization: Bearer <access-token>" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":"card-create","method":"tools/call","params":{"name":"card.create","arguments":{"boardId":1,"boardColumnId":1,"title":"From MCP","description":"Created over HTTP","tagNames":[]}}}'
```

## Docker (API + MCP)
`docker compose up --build` runs both:
- API/Web on `http://localhost:5000`
- MCP on `http://localhost:5001/mcp`

Compose is configured for source-IP trusted realtime relay from `boardoil-mcp` to `boardoil` and does not require a shared relay key.

Run the end-to-end Docker smoke test:

```bash
./scripts/mcp-docker-smoke.sh
```
