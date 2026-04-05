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

## Endpoint Verb Preference

- For update endpoints, generally prefer `PUT` over `PATCH`.
- Use `PATCH` only when the endpoint is intentionally a partial-update contract.

## Contract Design Guidance

- Prefer endpoint-specific DTOs when read and write concerns diverge; do not force one contract shape to serve both equally if that makes either side awkward.
- Full-update (`PUT`) contracts should remain cheap for clients to round-trip. If a client edits one field and keeps the rest unchanged, it should not need complex shape conversion just to resend the untouched fields.
- Denormalised read models are acceptable when they materially improve consumer ergonomics (for example richer one-hit payloads for integrations), but treat that as a convenience projection rather than proof that the denormalised payload is the only source of truth.
- When a read model duplicates data from a catalogue or parent entity, explicitly document the intended authority model:
  - which payload/store is authoritative for live updates and mutations
  - which payload is convenience/snapshot data for easier reads
- Do not introduce partial-update contracts just to paper over awkward DTO design. Prefer improving the contract shape first; adopt `PATCH` only when partial semantics are genuinely required.

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
