const OAUTH_ENDPOINT_PLACEHOLDER = 'https://your-boardoil-address/mcp/oauth';
const ACCESS_TOKEN_ENDPOINT_PLACEHOLDER = 'https://your-boardoil-address/mcp';
const OAUTH_ENDPOINT_SUFFIX = '/oauth';

export function resolveMcpHelpContent(markdown: string, mcpOAuthResourceUrl: string) {
  const normalizedOAuthResourceUrl = mcpOAuthResourceUrl.replace(/\/+$/, '');
  if (!normalizedOAuthResourceUrl.endsWith(OAUTH_ENDPOINT_SUFFIX)) {
    throw new Error('The MCP OAuth resource URL must end with /oauth.');
  }

  const accessTokenEndpoint = normalizedOAuthResourceUrl.slice(0, -OAUTH_ENDPOINT_SUFFIX.length);
  return markdown
    .split(OAUTH_ENDPOINT_PLACEHOLDER)
    .join(normalizedOAuthResourceUrl)
    .split(ACCESS_TOKEN_ENDPOINT_PLACEHOLDER)
    .join(accessTokenEndpoint);
}
