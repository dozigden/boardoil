# Connect an MCP Client to BoardOil

BoardOil includes a Streamable HTTP MCP server for agents to access your boards. OAuth is the recommended authentication method. Access tokens remain available as a manual fallback.

## Choose an authentication method

**OAuth (recommended):** Use `https://your-boardoil-address/mcp/oauth` when your client supports OAuth.

**Access token:** Use `https://your-boardoil-address/mcp` when your client cannot complete the OAuth flow or you need an explicitly managed token.

OAuth opens BoardOil in your browser so you can sign in, review the requested scopes, and authorize the connection. The client stores and refreshes its OAuth credentials. You can inspect or revoke the connection under **User settings → Authentication → OAuth**.

An access token is a manually managed secret that acts with its owner's BoardOil permissions. Store it as a credential, never commit it to source control, and do not paste it into an agent conversation.

## Recommended OAuth setup

### VS Code and GitHub Copilot

Use VS Code's built-in MCP configuration so VS Code performs OAuth discovery, registration, and browser callback handling:

1. Open the Command Palette and run **MCP: Add Server**.
2. Choose **HTTP** as the server type.
3. Enter `https://your-boardoil-address/mcp/oauth` as the server URL.
4. Name the server `boardoil`.
5. Choose **Global** to use BoardOil in every project or **Workspace** for the current project only.
6. Start the server and complete sign-in when VS Code opens BoardOil in your browser.

To configure `mcp.json` manually instead, add:

```json
{
  "servers": {
    "boardoil": {
      "type": "http",
      "url": "https://your-boardoil-address/mcp/oauth"
    }
  }
}
```

Run **MCP: Open User Configuration** for the global file or **MCP: Open Workspace Folder Configuration** for the project file. Merge the `boardoil` entry into an existing `servers` object rather than replacing other configured servers.

Keep only one BoardOil server entry. If an earlier setup added an `oauth.clientId`, remove that property and let VS Code register its own client automatically.

### Codex

Create or update `~/.codex/config.toml`:

```toml
[mcp_servers.boardoil]
url = "https://your-boardoil-address/mcp/oauth"
auth = "oauth"
```

Run the OAuth login:

```sh
codex mcp login boardoil
```

Follow the browser flow to sign in to BoardOil and approve the connection.

For an advanced project-specific setup, put the same configuration in `.codex/config.toml` inside a trusted project instead.

Codex CLI, the Codex IDE extension, and the ChatGPT desktop app share MCP configuration on the same Codex host. You can also add BoardOil through the graphical MCP server settings by choosing Streamable HTTP, entering the OAuth endpoint, and selecting **Authenticate** when prompted. See the [official Codex MCP documentation](https://learn.chatgpt.com/docs/extend/mcp?surface=cli) for current client-specific controls.

### Claude Code

To make BoardOil available in every project, run:

```sh
claude mcp add --transport http --scope user boardoil "https://your-boardoil-address/mcp/oauth"
```

Open Claude Code, use `/mcp`, select BoardOil, and complete the browser authentication flow. See the [Claude Code MCP documentation](https://code.claude.com/docs/en/mcp) for current client-specific controls.

For an advanced project-specific setup, run the command from that project with `--scope local` instead.


## Access token fallback

Use an access token only when OAuth is not supported or when you deliberately need a manually managed credential.

1. In BoardOil, open **User settings → Authentication → Access tokens**.
2. Create an access token and grant only the scopes the client needs:
   - `mcp:read` for reading boards and cards.
   - `mcp:write` for creating, updating, moving, commenting on, or deleting cards.
3. Copy the token when it is shown and store it securely. BoardOil does not show the complete token again.
4. Configure the client to use `https://your-boardoil-address/mcp` with the token as a Bearer credential.

For Codex, keep the token outside `config.toml` and reference an environment variable:

```toml
[mcp_servers.boardoil]
url = "https://your-boardoil-address/mcp"
bearer_token_env_var = "BOARDOIL_MCP_TOKEN"
```

Set `BOARDOIL_MCP_TOKEN` in the environment used to launch Codex. Do not include the token value in a repository file or in the prompt you send to the agent.

Access-token connections can use the same `identity_get` verification as OAuth connections. Revoke an access token under **User settings → Authentication → Access tokens** when it is no longer needed or may have been exposed.

### Local unauthenticated mode

BoardOil can expose MCP without authentication for a tightly controlled local integration. This gives every caller the permissions of the configured BoardOil user and must not be exposed to an untrusted network.
