const MCP_ENDPOINT_PLACEHOLDER = 'https://your-boardoil-address/mcp';

export function resolveMcpHelpContent(markdown: string, mcpResourceUrl: string) {
  const normalizedResourceUrl = mcpResourceUrl.replace(/\/+$/, '');
  if (!normalizedResourceUrl.endsWith('/mcp')) {
    throw new Error('The MCP resource URL must end with /mcp.');
  }

  return markdown
    .split(MCP_ENDPOINT_PLACEHOLDER)
    .join(normalizedResourceUrl);
}
