# Advanced BoardOil Installation

This guide covers operating and securing a self-hosted BoardOil installation. Complete [Getting Started](GETTING_STARTED.md) first unless you already have BoardOil running with Docker Compose.

## HTTPS

The Getting Started configuration allows authentication cookies over plain HTTP. When BoardOil is served over HTTPS, change this setting:

```yaml
environment:
  BoardOilAuth__AllowInsecureCookies: "false"
```

BoardOil respects forwarded headers and uses WebSockets for real-time board updates. If HTTPS terminates before reaching BoardOil, the original protocol must be forwarded as `X-Forwarded-Proto` and WebSocket connections must be supported.

## Persistent data and backups

The `/data` volume contains all persistent installation state:

- `boardoil.db`: the SQLite database
- `images/`: uploaded images
- `attachments/`: private, original card attachment files and temporary board packages
- `boardoil-auth-signing-key`: the generated authentication signing key
- `backups/`: automatic database backups made before schema migrations

Keep the whole volume, including the signing key, together. Recreating the container is safe as long as it is attached to the same volume. Removing the volume removes the BoardOil installation data.

BoardOil retains automatic pre-migration database backups for 30 days. These are a recovery aid, not a replacement for regular backups of the complete `/data` volume.

### Restore an automatic database backup

BoardOil's automatic backups are complete SQLite database files named `boardoil-backup-<timestamp>.db` in `/data/backups`.

To restore one, stop BoardOil and preserve the current database before copying the selected backup over `/data/boardoil.db`. Remove `boardoil.db-wal` and `boardoil.db-shm` if either is present, then start BoardOil again.

BoardOil will apply any migrations required by the running image when it starts. If you are recovering from a failed migration, use the earlier BoardOil image that matches the backup or the same migration will be attempted again.

Automatic backups contain only the database; uploaded images, card attachment files, and the authentication signing key are not included. Back up and restore the database and attachment files together, with BoardOil stopped. Restoring an older database alone may reference attachments that have since been deleted.

### Card attachment storage and limits

Card attachments accept any file type and preserve the original bytes. They are served only through authenticated download endpoints, never through the public image directory. The default private directory is `attachments/` beside the SQLite database (`/data/attachments` in the container).

Optional environment settings:

```yaml
environment:
  BoardOil__AttachmentRootPath: "/data/attachments"
  BoardOil__AttachmentMaxByteLength: "10485760" # 10 MiB per file
  BoardOil__AttachmentImageMaxPixelCount: "20000000"
  BoardOil__AttachmentImageMaxEdgeLength: "10000"
```

The image limits apply when an attachment is shown inline in a card description. They protect image decoding independently of the attachment byte limit; an attachment that exceeds them remains available through the normal authenticated download action.

If you change the attachment root, move the existing directory while BoardOil is stopped and include the new location in persistent storage and backups. Do not place it inside a publicly served directory. Allow disk capacity for original files, independent duplicate/import copies, and temporary packages. There is no application-imposed whole-package, JSON-entry-size or ZIP-entry-count limit; imported attachments must meet the current per-file limit. Duplicating existing attachments is not restricted by a subsequently lowered upload limit. Configure any reverse proxy's request limit to accommodate uploads, including multipart overhead.

Attachment records are saved as `Pending` before files are written and become `Ready` only when the operation commits. Filenames are unique within a card, ignoring case. Pending attachments reserve their name but are not shown or downloadable. Failed uploads release the name for retry.

Deletion changes the same attachment row to `PendingDeletion` and clears its owner before the card or board can be removed. File deletion is attempted after committing that change; the row is removed only when the file is gone. Failed deletions retain the row and its last error for inspection and retry. A deleted filename can be reused even if its old file still awaits cleanup.

Recovery runs once at application startup, after database initialisation and before requests are accepted. It queries `Pending` and `PendingDeletion` attachments and the separate `TemporaryBoardPackages` table, removes their files if present, then removes the records. Failed deletions are logged and retained for the next startup. There are no attachment timers, leases or filesystem scans. Temporary package files and their tracking rows are removed on close; startup recovery handles interrupted operations and failed cleanup.

Run only one application instance against a given database and attachment directory. Startup recovery assumes no other instance has active uploads or imports. Failed work can remain on disk until the next restart.

## MCP configuration

OAuth is the preferred way to connect an MCP client to BoardOil. Point the client at:

```text
https://your-boardoil-address/mcp
```

Compatible clients will discover BoardOil's OAuth configuration and ask you to sign in and authorize the connection. BoardOil supports Dynamic Client Registration for clients without a preregistered identity. Authorized connections can be viewed and revoked under **User settings → Authentication → OAuth**.

If a client does not support OAuth, create an access token under **User settings → Authentication → Access tokens** and connect it to:

```text
https://your-boardoil-address/mcp
```

Send the token as `Authorization: Bearer <YOUR_ACCESS_TOKEN>`.

BoardOil uses the Streamable HTTP transport and supports the current MCP `2026-07-28` protocol. Modern clients use `server/discover` and sessionless requests. Initialize-based clients using MCP `2025-11-25` or an earlier supported revision can use the same endpoint without any configuration change.

Protocol compatibility is separate from transport selection. The older HTTP+SSE transport remains disabled by default. Enable it only for clients that cannot use Streamable HTTP by setting `BoardOilMcp__TransportMode` to `both`. Clients then use `/mcp/sse` and `/mcp/message`.

Authentication on the `/mcp` endpoint can be disabled for a tightly controlled local integration:

```yaml
environment:
  BoardOilMcp__AuthMode: "none"
  BoardOilMcp__AnonymousActorUserId: "1"
```

`BoardOilMcp__AuthMode=none` allows unauthenticated MCP access with the permissions of the selected user. Only use it on a trusted local network.

Administrators can set the MCP public base URL under **System admin → Configuration** if BoardOil cannot determine its public address correctly. The automatic setting is preferred when it produces the correct OAuth URLs.
