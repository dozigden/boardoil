import { describe, expect, it } from 'vitest';
import mcpHelpMarkdown from 'virtual:boardoil-mcp-help';
import {
  buildClaudeCodeAccessTokenCommand,
  buildCodexAccessTokenConfig
} from '../../shared/utils/accessTokenPresentation';
import {
  buildClaudeCodeOAuthCommand,
  buildCodexOAuthConfig,
  buildVsCodeOAuthConfig
} from '../../shared/utils/oauthConnectionPresentation';
import { resolveMcpHelpContent } from '../utils/mcpHelpContent';

describe('MCP help content', () => {
  it('bundles the canonical MCP guide without unresolved local documentation links', () => {
    expect(mcpHelpMarkdown).toContain('# Connect an MCP Client to BoardOil');
    expect(mcpHelpMarkdown).toContain('## VS Code and GitHub Copilot');
    expect(mcpHelpMarkdown).toContain('## Codex');
    expect(mcpHelpMarkdown).toContain('## Claude Code');
    expect(mcpHelpMarkdown).toContain('## Other MCP clients');
    expect(mcpHelpMarkdown).toContain('## After connecting');
    expect(mcpHelpMarkdown).toContain('## Access token fallback');
    expect(mcpHelpMarkdown).not.toContain('### Ask your agent');
    expect(mcpHelpMarkdown).not.toContain('| Method | Endpoint | Use it when |');
    expect(mcpHelpMarkdown).not.toContain('ADVANCED_INSTALLATION.md');
  });

  it('presents client-specific instructions before generic and fallback guidance', () => {
    const sectionPositions = [
      '## VS Code and GitHub Copilot',
      '## Codex',
      '## Claude Code',
      '## Other MCP clients',
      '## After connecting',
      '## Access token fallback'
    ].map(section => mcpHelpMarkdown.indexOf(section));

    expect(sectionPositions.every(position => position >= 0)).toBe(true);
    expect(sectionPositions).toEqual([...sectionPositions].sort((left, right) => left - right));
  });

  it('uses the canonical OAuth resource for both MCP endpoints', () => {
    const resolvedMarkdown = resolveMcpHelpContent(
      mcpHelpMarkdown,
      'https://mcp.example.com/boardoil/mcp/oauth/'
    );

    expect(resolvedMarkdown).toContain('https://mcp.example.com/boardoil/mcp/oauth');
    expect(resolvedMarkdown).toContain('https://mcp.example.com/boardoil/mcp');
    expect(resolvedMarkdown).not.toContain('https://your-boardoil-address');
  });

  it('keeps generated OAuth and access-token snippets aligned with the guide', () => {
    const oauthResourceUrl = 'https://mcp.example.com/boardoil/mcp/oauth';
    const accessTokenResourceUrl = 'https://mcp.example.com/boardoil/mcp';
    const resolvedMarkdown = resolveMcpHelpContent(mcpHelpMarkdown, oauthResourceUrl);

    expect(resolvedMarkdown).toContain(buildVsCodeOAuthConfig(oauthResourceUrl));
    expect(resolvedMarkdown).toContain(buildCodexOAuthConfig(oauthResourceUrl));
    expect(resolvedMarkdown).toContain(buildClaudeCodeOAuthCommand(oauthResourceUrl));
    expect(resolvedMarkdown).toContain(
      buildClaudeCodeAccessTokenCommand(accessTokenResourceUrl)
    );
    expect(resolvedMarkdown).toContain(buildCodexAccessTokenConfig(accessTokenResourceUrl));
  });

  it('rejects an unexpected OAuth resource path', () => {
    expect(() => resolveMcpHelpContent(mcpHelpMarkdown, 'https://boardoil.example.com/mcp'))
      .toThrow('The MCP OAuth resource URL must end with /oauth.');
  });
});
