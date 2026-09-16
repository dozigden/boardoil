# Connect an MCP Client to BoardOil

BoardOil includes a Streamable HTTP MCP server that lets agents work with your boards. Choose your client below and use its recommended OAuth setup. OAuth opens BoardOil in your browser so you can sign in, review the requested permissions, and approve the connection.

## VS Code and GitHub Copilot

Use VS Code's built-in MCP configuration so VS Code handles OAuth discovery, client registration, and the browser callback:

1. Open the Command Palette and run **MCP: Add Server**.
2. Choose **HTTP** as the server type.
3. Enter `https://your-boardoil-address/mcp` as the server URL.
4. Name the server `boardoil`.
5. Choose **Global** to use BoardOil in every project or **Workspace** for the current project only.
6. Start the server and complete sign-in when VS Code opens BoardOil in your browser.

To configure `mcp.json` manually instead, add:

```json
{
  "servers": {
    "boardoil": {
      "type": "http",
      "url": "https://your-boardoil-address/mcp"
    }
  }
}
```

Run **MCP: Open User Configuration** for your global configuration or **MCP: Open Workspace Folder Configuration** for the current project. Merge the `boardoil` entry into an existing `servers` object rather than replacing other configured servers.

Keep only one BoardOil server entry. Do not add an `oauth.clientId`; let VS Code register and manage its own OAuth client.

See the [VS Code MCP documentation](https://code.visualstudio.com/docs/agent-customization/mcp-servers) for current configuration and troubleshooting controls.

## Codex

Create or update `~/.codex/config.toml`:

```toml
[mcp_servers.boardoil]
url = "https://your-boardoil-address/mcp"
auth = "oauth"
```

Then start the OAuth login:

```sh
codex mcp login boardoil
```

Follow the browser flow to sign in to BoardOil and approve the connection. Restart Codex if the new server is not visible in the current session.

For an advanced project-specific setup, put the same configuration in `.codex/config.toml` inside a trusted project. The Codex CLI, Codex IDE extension, and ChatGPT desktop app share MCP configuration on the same host. In the graphical clients you can instead add a Streamable HTTP server, enter the OAuth endpoint, and select **Authenticate** when prompted.

See the [official Codex MCP documentation](https://learn.chatgpt.com/docs/extend/mcp?surface=cli) for current client-specific controls.

## Claude Code

Add BoardOil for your user so it is available in every project:

```sh
claude mcp add --transport http --scope user boardoil "https://your-boardoil-address/mcp"
```

Then start the OAuth login:

```sh
claude mcp login boardoil
```

Follow the browser flow to sign in to BoardOil and approve the connection. If your Claude Code version does not provide `claude mcp login`, open Claude Code, run `/mcp`, select BoardOil, and authenticate there.

Use `--scope local` instead of `--scope user` for a private configuration tied to the current project. Use `--scope project` for a shared `.mcp.json` configuration that can be committed with the project.

See the [Claude Code MCP documentation](https://code.claude.com/docs/en/mcp) for current client-specific controls.

## Other MCP clients

For another client that supports remote Streamable HTTP servers and OAuth:

1. Add a remote Streamable HTTP server named `boardoil`.
2. Use `https://your-boardoil-address/mcp` as its URL.
3. Select OAuth authentication if the client asks for an authentication method.
4. Let the client discover BoardOil's authorization server and register its own OAuth client. Do not manually provide a client ID unless the client explicitly requires one.
5. Complete the BoardOil sign-in and approval flow in your browser.

If the client cannot perform OAuth discovery, dynamic client registration, or a browser callback, use the access-token fallback below.

## After connecting

BoardOil advertises these OAuth scopes:

- `mcp:read` permits reading boards and cards.
- `mcp:write` permits creating, updating, moving, commenting on, and deleting cards, and uploading or deleting attachments.

Confirm that the client lists the BoardOil tools after connecting. Clients that allow a specific tool to be called can use `identity_get` to show the current BoardOil user, authentication type, and granted scopes.

OAuth credentials are stored and refreshed by the client. You can inspect or revoke a connection under **User settings → Authentication → OAuth** in BoardOil. Use the client's authentication controls to clear its locally stored credentials when necessary.

## Card attachments

- `card_get` includes an `attachments` array of metadata for the live card. File bytes are never included automatically. Mutation responses and board-wide snapshots do not load attachment lists.
- `card_attachment_delete` accepts `boardId`, `cardId` and `id` (the attachment ID from `card_get`). It permanently deletes that live-card attachment. Archived attachments are read-only.
- Reads require `mcp:read`; upload and deletion require `mcp:write`. The user's normal board permissions still apply. Missing cards/attachments return `not_found`; invalid identifiers return `validation_failed`; denied access returns `forbidden`.

Tool results include the same JSON in both `structuredContent` and a text content block, for clients that consume either representation. Errors include their code, status and any field-level validation details in both formats.

Attachment byte transfer does not require an MCP-specific resource or filesystem integration. Any agent or MCP client that can make an ordinary HTTPS `GET` or `PUT` request and set the returned headers can use it. The transfer request is independent of the MCP session after issuance; do not substitute the MCP bearer token, cookies or query-string credentials.

### Download original files

Call `card_attachment_download` with `boardId` and an attachment `id` from `card_get`. This requires authenticated MCP with `mcp:read`. It returns `url`, `method` (`GET`), a complete `headers.Authorization` value using the dedicated `BoardOilAttachment` scheme, and `expiresAtUtc`. Use those values unchanged in an ordinary HTTP client to download the original bytes; the client does not need access to your MCP connection credentials. Keep the returned header secret, out of URLs, logs and shared transcripts. Do not forward it to another host or follow redirects with it.

Tickets allow repeated downloads for up to 2 minutes, capped by the originating credential's expiry. A transfer admitted before expiry may finish afterwards. Each request rechecks the originating PAT/OAuth token and authorization, the active user, board access and the original attachment owner. Revocation, deletion, archive/restore or transfer to another board can invalidate a ticket earlier. Request a new ticket after expiry or an ownership change. Ticket credentials cannot authenticate other API or MCP operations.

The response forces file download as `application/octet-stream`, with `nosniff` and private/no-store caching. No file conversion occurs. Ticket records contain only a hash of the secret; expired records are removed at application startup, without scheduled jobs. Issuance and valid-ticket redemption outcomes are retained in a queryable audit table for 14 days and purged at startup; unknown IDs and wrong secrets are not persisted. Operational logs contain IDs rather than credentials. MCP SDK trace payload logging is suppressed because it would expose ticket responses.

Transfers require HTTPS. Behind a TLS-terminating reverse proxy, configure trusted forwarded headers so the application recognises HTTPS, and set the public MCP base URL when needed. The existing explicit `BoardOilAuth:AllowInsecureCookies` development opt-out also permits local HTTP transfers; do not enable it in a deployed environment. Proxy/client logging must also omit Authorization headers and MCP response bodies.

The configured public MCP base URL is also the base of returned attachment URLs, including any path prefix. The proxy must route both the public MCP URL and every returned attachment-transfer URL to the same BoardOil instance, preserve the `Authorization`, `Content-Type` and `Content-Length` headers, and avoid redirecting transfer requests to another host.

BoardOil deliberately does not apply an additional IP-based rate limit to transfer endpoints. Agents commonly share proxy addresses, and guessed ticket IDs must not be able to consume another caller's allowance. Exposure is instead bounded by the unguessable operation-specific secret, 2-minute admission window, source-credential and permission rechecks, configured per-file size limit, and atomic single-claim upload rule. Deployments that need aggregate bandwidth protection can add it at their trusted reverse proxy, without logging credentials; download retries within the ticket window are part of the supported contract.

### Upload original files

Call `card_attachment_upload` with `boardId`, live `cardId`, `fileName`, exact `byteLength`, and optional `contentType`. It requires authenticated MCP with `mcp:write` and returns a `url`, `method` (`PUT`), exact `Authorization` and `Content-Type` headers, `byteLength`, and `expiresAtUtc`. Send the unmodified file bytes as the raw request body; do not use multipart encoding or put the ticket secret in the URL.

Issuance reserves the case-insensitive filename with a pending attachment record and rejects names already used on the card. The declared length must fit the configured attachment limit. The HTTP request's `Content-Length` and normalised `Content-Type` must match the ticket before it is claimed, and the bytes actually read must match the declared length before publication.

Each upload ticket permits one claimed attempt. Concurrent requests fail once one request has claimed it. A retry after successful completion returns the existing attachment metadata without reading or replacing the file. Failed or interrupted attempts require a new ticket. The originating credential, user, current card-update permission and exact live-card owner are checked before admission and again before publication. Archive, transfer, deletion or credential revocation prevents publication. Incomplete rows and staged files are recovered during application startup; no timer, lease, resumable upload or chunked upload protocol is used.

## Access token fallback

Use an access token only when the client cannot complete OAuth or when you deliberately need a manually managed credential. An access token acts with its owner's BoardOil permissions.

1. In BoardOil, open **User settings → Authentication → Access tokens**.
2. Create an access token and grant only the scopes the client needs: `mcp:read`, `mcp:write`, or both.
3. Copy the token when it is shown and store it securely. BoardOil does not show the complete token again.
4. Configure the client to use `https://your-boardoil-address/mcp` and send the token as an `Authorization: Bearer` credential.

Set `BOARDOIL_MCP_TOKEN` in the environment used to launch your client.

For Claude Code, add a user-scoped server whose authorization header reads the environment variable at connection time:

```sh
claude mcp add --transport http --scope user boardoil "https://your-boardoil-address/mcp" \
  --header 'Authorization: Bearer ${BOARDOIL_MCP_TOKEN}'
```

For Codex, keep the token outside `config.toml` and reference an environment variable:

```toml
[mcp_servers.boardoil]
url = "https://your-boardoil-address/mcp"
bearer_token_env_var = "BOARDOIL_MCP_TOKEN"
```

Never commit an access token to source control or paste it into an agent conversation. Revoke it under **User settings → Authentication → Access tokens** when it is no longer needed or may have been exposed.

### Local unauthenticated mode

BoardOil can expose MCP without authentication for a tightly controlled local integration. This gives every caller the permissions of the configured BoardOil user and must not be exposed to an untrusted network.
