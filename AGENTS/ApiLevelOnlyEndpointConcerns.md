# API-Level-Only Endpoint Concerns

This inventory tracks endpoint concerns that are currently covered only in `BoardOil.Api.Tests` (not by service-level tests).

## Runtime and Host Wiring

- `GET /api/version`
  - API tests: `VersionEndpointsIntegrationTests`
  - Why API-only: reads build metadata from runtime host configuration; there is no service abstraction.

- `GET /api/health`
  - API tests: `StartupMigrationBackupIntegrationTests`
  - Why API-only: validates startup migration + backup lifecycle wiring in hosted runtime.

## Internal Relay and MCP Transport

- `POST {BoardRealtimeRelay.EndpointPath}`
  - API tests: `InternalRealtimeEndpointsTests`
  - Why API-only: validates endpoint-level API-key/IP gate and relay dispatch wiring.

- `POST /mcp` and MCP protocol path handling (`/sse*`, `/messages*`, `/v1/mcp*` unsupported aliases)
  - API tests: `McpHttpAuthAndPathIntegrationTests`, `McpToolDiscoveryIntegrationTests`, `McpToolExecutionIntegrationTests`
  - Why API-only: protocol, auth handler, middleware, and route mapping behaviour are transport concerns.

## Auth and Cookie Transport Semantics

- `POST /api/auth/register-initial-admin` (cookie/CSRF transport behaviour)
  - API tests: `AuthIntegrationTests`
  - Why API-only: stale-cookie tolerance and secure cookie flag checks are HTTP/cookie middleware behaviours.

- `POST /api/auth/machine/pat/login` (removed endpoint contract)
  - API tests: `MachinePatIntegrationTests`
  - Why API-only: negative route contract (404/405) is enforced at endpoint mapping level.

## Multipart Binding

- `POST /api/boards/import` (missing file / invalid zip mapping)
  - API tests: `BoardImportApiIntegrationTests`
  - Why API-only: multipart form binding and HTTP validation mapping are endpoint transport concerns.
