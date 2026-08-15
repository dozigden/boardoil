import { describe, expect, it } from 'vitest';
import {
  buildAgentOAuthPrompt,
  buildClaudeCodeOAuthCommand,
  buildCodexOAuthConfig
} from './oauthConnectionPresentation';

describe('oauthConnectionPresentation', () => {
  it('builds a global agent setup prompt', () => {
    const prompt = buildAgentOAuthPrompt(
      'https://boardoil.example.com/deployment/mcp/oauth/',
      'global'
    );

    expect(prompt).toBe(`Connect to the BoardOil MCP server at https://boardoil.example.com/deployment/mcp/oauth using OAuth.
Configure it globally so it is available in every project.
Start authentication when ready.`);
    expect(prompt).not.toContain('token');
    expect(prompt).not.toContain('secret');
  });

  it('builds a project-specific agent setup prompt', () => {
    const prompt = buildAgentOAuthPrompt(
      'https://boardoil.example.com/deployment/mcp/oauth/',
      'project'
    );

    expect(prompt).toBe(`Connect to the BoardOil MCP server at https://boardoil.example.com/deployment/mcp/oauth using OAuth.
Configure it for the current project only.
Start authentication when ready.`);
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
