# BoardOil

BoardOil is a self-hosted Kanban board mostly meant for my home lab environment,  I use it to plan tech projects, gardening, and even a wedding.

Key features:
Multiple boards with basic RBAC.
It's got a REST API and MCP server.

It's written in .NET and Vue3.  I deploy it myself via Docker, so that's had the most testing.

> Warning
> This project is mostly a learning experience. Use at own risk.

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
