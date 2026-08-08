export function buildCodexOAuthConfig(resourceUrl: string) {
  const normalizedResourceUrl = resourceUrl.replace(/\/+$/, '');
  return `[mcp_servers.boardoil]
url = "${normalizedResourceUrl}"
auth = "oauth"`;
}

export function buildClaudeCodeOAuthCommand(resourceUrl: string) {
  const normalizedResourceUrl = resourceUrl.replace(/\/+$/, '');
  return `claude mcp add --transport http --scope local boardoil "${normalizedResourceUrl}"`;
}
