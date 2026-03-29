# Architecture Guidance

This file captures the current runtime architecture so agents can orient quickly before making design or implementation changes.

## Solution Layers

- `BoardOil.Api`
  - HTTP transport, endpoint wiring, auth policies, runtime hosting, MCP transport wiring.
  - Endpoints stay thin and delegate behaviour to services.
- `BoardOil.Contracts`
  - API contracts (`*Request`, `*Dto`, `ApiResult`, `ValidationError`) shared across API/services/tests.
- `BoardOil.Abstractions`
  - Cross-cutting service and infrastructure abstractions (service interfaces, db scope abstractions, auth abstractions).
- `BoardOil.Persistence.Abstractions`
  - Persistence-facing contracts and EF entity types (`Entity*`) plus repository interfaces.
- `BoardOil.Ef`
  - EF Core implementation (`BoardOilDbContext`), repository implementations, ambient db scope implementation, migrations.
- `BoardOil.Services`
  - Business workflows, invariants, validation orchestration, persistence coordination, mapping to contracts, realtime event publishing.
- `BoardOil.Web`
  - Vue 3 + Pinia frontend, typed API client, route guards, realtime board updates.

## Request and Write Flow

1. Endpoint/controller-equivalent in `BoardOil.Api` invokes a service (`I*Service`).
2. Service opens a db scope (`Create()` for write, `CreateReadOnly()` for read).
3. Service performs validation + repository operations.
4. Service commits with `scope.SaveChangesAsync()`.
5. Service returns `ApiResult`/`ApiResult<T>` and API layer serialises that contract.

Default: one save per service operation unless a specific transactional reason requires otherwise.

## Boundaries

- Repositories:
  - Entity-level data access only.
  - No orchestration/policy decisions.
  - No commit ownership.
- Services:
  - Own orchestration, policy, validation coordination, and cross-repository workflows.

## Realtime and Auth Notes

- Realtime notifications are emitted from services through `IBoardEvents`.
- Clients should treat realtime as incremental and resync-safe after reconnects.
- API auth uses JWT/cookies for user sessions.
- MCP endpoint auth is PAT bearer-based through MCP-specific auth handling.
