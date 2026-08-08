import { describe, expect, it } from 'vitest';
import { buildCodexOAuthConfig } from './oauthConnectionPresentation';

describe('oauthConnectionPresentation', () => {
  it('builds a project-specific secret-free Codex configuration', () => {
    const config = buildCodexOAuthConfig('https://boardoil.example.com/deployment/mcp/oauth/');

    expect(config).toBe(`[mcp_servers.boardoil]
url = "https://boardoil.example.com/deployment/mcp/oauth"
auth = "oauth"`);
    expect(config).not.toContain('token');
    expect(config).not.toContain('secret');
    expect(config).not.toContain('code =');
  });
});
