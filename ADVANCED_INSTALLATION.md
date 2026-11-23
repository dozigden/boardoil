# Advanced BoardOil Installation

These instructions use the `boardoil` service and `./boardoil-data` directory from [Getting Started](GETTING_STARTED.md). After changing `compose.yml`, run `docker compose up -d boardoil` to apply the change.

## HTTPS

Configure your reverse proxy to forward requests to BoardOil on port 5000, preserve the public `Host`, set `X-Forwarded-Proto` to `https`, and pass WebSocket upgrades. In the `boardoil` service, change the cookie setting to require HTTPS.

```yaml
environment:
  BoardOilAuth__AllowInsecureCookies: "false"
```

## Back up and restore

Compose mounts `./boardoil-data` at `/data` in the container. It holds the SQLite database (`boardoil.db`), uploaded images (`images/`), private card attachments (`attachments/`) and the authentication signing key (`boardoil-auth-signing-key`). Use one BoardOil instance per data directory.

**Back up.** Run `docker compose stop boardoil`, copy the entire data directory to your backup location, then run `docker compose start boardoil`. Include any attachment directory mounted separately.

**Restore.** Stop BoardOil, move the current data directory aside, and restore the saved directory with its ownership and permissions. Restore any separate attachment directory from the same backup before starting BoardOil. For a rollback, select the matching image version in `compose.yml` and run `docker compose up -d boardoil`.

## Card attachments

| Compose environment setting | Default | Purpose |
| --- | --- | --- |
| `BoardOil__AttachmentRootPath` | `/data/attachments` | Private file storage inside the container |
| `BoardOil__AttachmentMaxByteLength` | `10485760` | Maximum file size in bytes (10 MiB) |
| `BoardOil__AttachmentImageMaxPixelCount` | `20000000` | Maximum pixel count for inline previews |
| `BoardOil__AttachmentImageMaxEdgeLength` | `10000` | Maximum width or height in pixels for inline previews |

## MCP configuration

Point the client at `https://your-boardoil-address/mcp` and sign in with OAuth. For client-specific instructions or access-token setup, see [Connect an MCP Client to BoardOil](MCP.md).

- If BoardOil advertises an internal hostname, set the MCP public base URL under **System admin → Configuration**.
- **Legacy HTTP+SSE client.** Set `BoardOilMcp__TransportMode` to `both` and connect the client to `/mcp/sse`.
- **Unauthenticated local integration.** On an isolated network, set `BoardOilMcp__AuthMode` to `none` and `BoardOilMcp__AnonymousActorUserId` to the intended user’s ID. Anyone who can reach the endpoint can then act as that user.
