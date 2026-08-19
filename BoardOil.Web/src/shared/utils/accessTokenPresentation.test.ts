import { describe, expect, it } from 'vitest';
import {
  buildClaudeCodeAccessTokenCommand,
  buildCodexAccessTokenConfig
} from './accessTokenPresentation';

describe('accessTokenPresentation', () => {
  it('builds a user-scoped Claude Code command with an environment reference', () => {
    const command = buildClaudeCodeAccessTokenCommand(
      'https://boardoil.example.com/deployment/mcp/'
    );

    expect(command).toBe(`claude mcp add --transport http --scope user boardoil "https://boardoil.example.com/deployment/mcp" \\
  --header 'Authorization: Bearer \${BOARDOIL_MCP_TOKEN}'`);
  });

  it('builds a Codex configuration with an environment reference', () => {
    const config = buildCodexAccessTokenConfig(
      'https://boardoil.example.com/deployment/mcp/'
    );

    expect(config).toBe(`[mcp_servers.boardoil]
url = "https://boardoil.example.com/deployment/mcp"
bearer_token_env_var = "BOARDOIL_MCP_TOKEN"`);
  });
});
