import { describe, expect, it } from 'vitest';
import {
  buildClaudeCodeOAuthCommand,
  buildCodexOAuthConfig,
  buildVsCodeOAuthConfig
} from './oauthConnectionPresentation';

describe('oauthConnectionPresentation', () => {
  it('builds a VS Code remote HTTP server configuration', () => {
    const config = buildVsCodeOAuthConfig(
      'https://boardoil.example.com/deployment/mcp/oauth/'
    );

    expect(JSON.parse(config)).toEqual({
      servers: {
        boardoil: {
          type: 'http',
          url: 'https://boardoil.example.com/deployment/mcp/oauth'
        }
      }
    });
    expect(config).not.toContain('token');
    expect(config).not.toContain('secret');
  });

  it('builds a secret-free Codex configuration', () => {
    const config = buildCodexOAuthConfig('https://boardoil.example.com/deployment/mcp/oauth/');

    expect(config).toBe(`[mcp_servers.boardoil]
url = "https://boardoil.example.com/deployment/mcp/oauth"
auth = "oauth"`);
    expect(config).not.toContain('token');
    expect(config).not.toContain('secret');
    expect(config).not.toContain('code =');
  });

  it('builds a user-scoped Claude Code command', () => {
    const command = buildClaudeCodeOAuthCommand(
      'https://boardoil.example.com/deployment/mcp/oauth/'
    );

    expect(command).toBe(
      'claude mcp add --transport http --scope user boardoil "https://boardoil.example.com/deployment/mcp/oauth"'
    );
    expect(command).not.toContain('token');
    expect(command).not.toContain('secret');
  });
});
