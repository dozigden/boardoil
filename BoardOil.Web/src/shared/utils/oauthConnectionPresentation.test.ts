import { describe, expect, it } from 'vitest';
import {
  buildClaudeCodeOAuthCommand,
  buildCodexOAuthConfig,
  buildVsCodeOAuthConfig,
  buildVsCodeOAuthUrl
} from './oauthConnectionPresentation';

describe('oauthConnectionPresentation', () => {
  it('builds a VS Code remote HTTP server configuration', () => {
    const config = buildVsCodeOAuthConfig(
      'https://boardoil.example.com/deployment/mcp/'
    );

    expect(JSON.parse(config)).toEqual({
      servers: {
        boardoil: {
          type: 'http',
          url: 'https://boardoil.example.com/deployment/mcp'
        }
      }
    });
    expect(config).not.toContain('token');
    expect(config).not.toContain('secret');
  });

  it('builds the canonical VS Code OAuth URL', () => {
    expect(buildVsCodeOAuthUrl('https://boardoil.example.com/deployment/mcp/'))
      .toBe('https://boardoil.example.com/deployment/mcp');
  });

  it('builds a secret-free Codex configuration', () => {
    const config = buildCodexOAuthConfig('https://boardoil.example.com/deployment/mcp/');

    expect(config).toBe(`[mcp_servers.boardoil]
url = "https://boardoil.example.com/deployment/mcp"
auth = "oauth"`);
    expect(config).not.toContain('token');
    expect(config).not.toContain('secret');
    expect(config).not.toContain('code =');
  });

  it('builds a user-scoped Claude Code command', () => {
    const command = buildClaudeCodeOAuthCommand(
      'https://boardoil.example.com/deployment/mcp/'
    );

    expect(command).toBe(
      'claude mcp add --transport http --scope user boardoil "https://boardoil.example.com/deployment/mcp"'
    );
    expect(command).not.toContain('token');
    expect(command).not.toContain('secret');
  });
});
