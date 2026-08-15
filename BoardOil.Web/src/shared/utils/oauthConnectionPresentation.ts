export type AgentOAuthScope = 'global' | 'project';

export function buildAgentOAuthPrompt(resourceUrl: string, scope: AgentOAuthScope) {
  const normalizedResourceUrl = resourceUrl.replace(/\/+$/, '');
  let scopeInstruction = 'Configure it globally so it is available in every project.';
  if (scope === 'project') {
    scopeInstruction = 'Configure it for the current project only.';
  }

  return `Connect to the BoardOil MCP server at ${normalizedResourceUrl} using OAuth.
${scopeInstruction}
Start authentication when ready.`;
}

export function buildCodexOAuthConfig(resourceUrl: string) {
  const normalizedResourceUrl = resourceUrl.replace(/\/+$/, '');
  return `[mcp_servers.boardoil]
url = "${normalizedResourceUrl}"
auth = "oauth"`;
}

export function buildClaudeCodeOAuthCommand(resourceUrl: string) {
  const normalizedResourceUrl = resourceUrl.replace(/\/+$/, '');
  return `claude mcp add --transport http --scope user boardoil "${normalizedResourceUrl}"`;
}
