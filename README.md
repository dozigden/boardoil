# BoardOil

BoardOil is a lightweight self-hosted Kanban board for small trusted teams.

## v1 Direction
- Single board Kanban workflow
- .NET backend + Vue (TypeScript) frontend
- Realtime updates and typing indicators
- SQLite persistence via EF Core
- Dev Container-based local development workflow
- Single Docker container deployment

## Repository Layout
- `BoardOil.Api`: ASP.NET Core API (v1 scaffold)
- `BoardOil.Services`: service layer and business logic
- `BoardOil.Ef`: EF Core SQLite data access and entities
- `BoardOil.Web`: Vue + TypeScript (Vite) app (v1 scaffold)
- `.devcontainer`: development container configuration
- `Dockerfile`: single-image production build (frontend + backend)

## Development (Dev Container)
1. Open the repository in a Dev Container.
2. The container runs `dotnet restore` and `npm install` automatically.
3. Start backend:
```bash
dotnet run --project BoardOil.Api/BoardOil.Api.csproj --urls http://0.0.0.0:5000
```
4. Start frontend (new terminal):
```bash
cd BoardOil.Web
npm run dev
```
5. Open:
- frontend: `http://localhost:5173`
- API health: `http://localhost:5000/api/health`

## Production Container Baseline
Build:
```bash
docker build -t boardoil:dev .
```

Run (safe default, bound to `127.0.0.1` inside container):
```bash
docker run --rm -p 5000:5000 boardoil:dev
```
Use this mode only for locked-down behavior checks.

Run with explicit LAN/public bind override:
```bash
docker run --rm -p 5000:5000 -e ASPNETCORE_URLS=http://0.0.0.0:5000 boardoil:dev
```
