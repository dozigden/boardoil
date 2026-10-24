import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import type { Plugin } from 'vite';

const MCP_HELP_MODULE_ID = 'virtual:boardoil-mcp-help';
const MCP_HELP_RESOLVED_MODULE_ID = `\0${MCP_HELP_MODULE_ID}`;
const MCP_HELP_FILE_PATH = fileURLToPath(new URL('../../MCP.md', import.meta.url));

export function mcpHelpPlugin(): Plugin {
  return {
    name: 'boardoil-mcp-help',
    resolveId(id: string) {
      if (id === MCP_HELP_MODULE_ID) {
        return MCP_HELP_RESOLVED_MODULE_ID;
      }

      return null;
    },
    load(id: string) {
      if (id !== MCP_HELP_RESOLVED_MODULE_ID) {
        return null;
      }

      const markdown = readFileSync(MCP_HELP_FILE_PATH, 'utf8');
      return `export default ${JSON.stringify(markdown)};`;
    }
  };
}
