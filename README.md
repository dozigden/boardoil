# BoardOil

BoardOil is a lightweight self-hosted Kanban board for small trusted teams.

See [`TODO.md`](TODO.md) for the current implementation backlog.

## v1 Direction
- Single board Kanban workflow
- .NET backend + Vue (TypeScript) frontend
- Realtime updates and typing indicators
- SQLite persistence via EF Core
- Single Docker container deployment

## Repository Layout
- `BoardOil.Api`: ASP.NET Core API (v1 scaffold)
- `BoardOil.Services`: service layer and business logic
- `BoardOil.Ef`: EF Core SQLite data access and entities
- `BoardOil.Web`: Vue + TypeScript (Vite) app (v1 scaffold)
- `dev-startall.sh`: local dev launcher (API + frontend)
- `Dockerfile`: single-image production build (frontend + backend)
- `docker-compose.yml`: default local/prod-like container runtime

## Development
1. Restore dependencies:
```bash
dotnet restore BoardOil.slnx
cd BoardOil.Web && npm install
```
2. Start backend + frontend together:
```bash
./dev-startall.sh
```
3. Or start each process manually:
```bash
dotnet run --project BoardOil.Api/BoardOil.Api.csproj --urls http://127.0.0.1:5000
cd BoardOil.Web
npm run dev
```
4. Open:
- frontend: `http://localhost:5173`
- API health: `http://localhost:5000/api/health`
- Frontend uses same-origin `/api` + `/hubs` by default (Vite proxy in dev). Set `VITE_API_BASE` only when overriding API host.

## Realtime Surface
- Hub endpoint: `/hubs/board`
- Server events: `ColumnCreated`, `ColumnUpdated`, `ColumnDeleted`, `CardCreated`, `CardUpdated`, `CardDeleted`, `CardMoved`, `TypingChanged`
- Client events: `TypingStarted(cardId, field, userLabel)`, `TypingStopped(cardId, field, userLabel)`

## Runtime Configuration
- `BoardOil:DataPath` (default `/data/boardoil.db`)
- `BoardOil:ExposeLan` (default `false`)
- `BoardOil:Port` (default `5000`)
- `BoardOil:TypingTtlSeconds` (default `5`)
- `ASPNETCORE_URLS` still overrides listen URL when set explicitly.

## Docker (First-Class Path)
Build + run with persistent SQLite storage (Compose v2):
```bash
docker compose up --build -d
```

If your machine uses legacy Compose:
```bash
docker-compose up --build -d
```

Open:
- app + API: `http://localhost:5000`
- health: `http://localhost:5000/api/health`

Stop (Compose v2):
```bash
docker compose down
```

Stop (legacy Compose):
```bash
docker-compose down
```

Logs (Compose v2):
```bash
docker compose logs -f boardoil
```

Logs (legacy Compose):
```bash
docker-compose logs -f boardoil
```

Named volume (`boardoil-data`) persists data at `/data/boardoil.db` between restarts.

### Raw `docker run` Alternative
```bash
docker build -t boardoil:dev .
docker run --rm -p 5000:5000 -v boardoil-data:/data boardoil:dev
```
