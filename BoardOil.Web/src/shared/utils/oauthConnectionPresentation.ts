export function buildCodexOAuthConfig(resourceUrl: string) {
  const normalizedResourceUrl = resourceUrl.replace(/\/+$/, '');
  return `[mcp_servers.boardoil]
url = "${normalizedResourceUrl}"
auth = "oauth"`;
}
