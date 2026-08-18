import { describe, expect, it } from 'vitest';
import mcpHelpMarkdown from 'virtual:boardoil-mcp-help';
import { resolveMcpHelpContent } from '../utils/mcpHelpContent';

describe('MCP help content', () => {
  it('bundles the canonical MCP guide without unresolved local documentation links', () => {
    expect(mcpHelpMarkdown).toContain('# Connect an MCP Client to BoardOil');
    expect(mcpHelpMarkdown).toContain('## Recommended OAuth setup');
    expect(mcpHelpMarkdown).toContain('### VS Code and GitHub Copilot');
    expect(mcpHelpMarkdown).not.toContain('### Ask your agent');
    expect(mcpHelpMarkdown).not.toContain('| Method | Endpoint | Use it when |');
    expect(mcpHelpMarkdown).not.toContain('ADVANCED_INSTALLATION.md');
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

  it('rejects an unexpected OAuth resource path', () => {
    expect(() => resolveMcpHelpContent(mcpHelpMarkdown, 'https://boardoil.example.com/mcp'))
      .toThrow('The MCP OAuth resource URL must end with /oauth.');
  });
});
