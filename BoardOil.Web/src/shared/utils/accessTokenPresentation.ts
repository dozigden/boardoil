export function buildClaudeCodeAccessTokenCommand(resourceUrl: string) {
  const normalizedResourceUrl = resourceUrl.replace(/\/+$/, '');
  return `claude mcp add --transport http --scope user boardoil "${normalizedResourceUrl}" \\
  --header 'Authorization: Bearer \${BOARDOIL_MCP_TOKEN}'`;
}

export function buildCodexAccessTokenConfig(resourceUrl: string) {
  const normalizedResourceUrl = resourceUrl.replace(/\/+$/, '');
  return `[mcp_servers.boardoil]
url = "${normalizedResourceUrl}"
bearer_token_env_var = "BOARDOIL_MCP_TOKEN"`;
}
