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
- `mcp:write` permits creating, updating, moving, commenting on, and deleting cards.

Confirm that the client lists the BoardOil tools after connecting. Clients that allow a specific tool to be called can use `identity_get` to show the current BoardOil user, authentication type, and granted scopes.

OAuth credentials are stored and refreshed by the client. You can inspect or revoke a connection under **User settings → Authentication → OAuth** in BoardOil. Use the client's authentication controls to clear its locally stored credentials when necessary.

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
