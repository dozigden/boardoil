import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { loadEnv } from 'vite';
import { configDefaults, defineConfig } from 'vitest/config';
import vue from '@vitejs/plugin-vue';

const MCP_HELP_MODULE_ID = 'virtual:boardoil-mcp-help';
const MCP_HELP_RESOLVED_MODULE_ID = `\0${MCP_HELP_MODULE_ID}`;
const MCP_HELP_FILE_PATH = fileURLToPath(new URL('../MCP.md', import.meta.url));

function mcpHelpPlugin() {
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

function isTruthyEnv(value: string | undefined): boolean {
  if (!value) {
    return false;
  }

  const normalised = value.trim().toLowerCase();
  return normalised === '1' || normalised === 'true';
}

function useCompactTestOutput(): boolean {
  const configuredMode = process.env.BOARDOIL_TEST_OUTPUT?.trim().toLowerCase();
  if (configuredMode === 'compact') {
    return true;
  }

  if (configuredMode === 'verbose') {
    return false;
  }

  return isTruthyEnv(process.env.CI)
    || isTruthyEnv(process.env.GITHUB_ACTIONS)
    || isTruthyEnv(process.env.CODEX_CI)
    || isTruthyEnv(process.env.CLAUDECODE)
    || Boolean(process.env.CODEX_THREAD_ID);
}

export default defineConfig(({ command, mode }) => {
  const env = loadEnv(mode, process.cwd(), '');
  if (command === 'build') {
    const version = env.VITE_BO_VERSION?.trim();
    if (!version) {
      throw new Error(
        'Missing required build env var VITE_BO_VERSION. '
        + 'Set it explicitly or run npm scripts that provide it.'
      );
    }
  }

  const apiProxyTarget = env.VITE_BO_API_PROXY_TARGET?.trim() || 'http://localhost:5000';
  const oauthProxyTarget = env.VITE_BO_OAUTH_PROXY_TARGET?.trim() || apiProxyTarget;

  const config = {
    plugins: [
      mcpHelpPlugin(),
      vue({
        template: {
          compilerOptions: {
            isCustomElement: tag => tag === 'emoji-picker'
          }
        }
      })
    ],
    server: {
      proxy: {
        '/api': apiProxyTarget,
        '/images': apiProxyTarget,
        '/.well-known': {
          target: oauthProxyTarget,
          changeOrigin: true,
          secure: false
        },
        '/hubs': {
          target: apiProxyTarget,
          ws: true
        }
      }
    },
    test: {
      exclude: [...configDefaults.exclude, 'e2e/**']
    }
  };

  const compactTestOutput = useCompactTestOutput();
  if (!compactTestOutput) {
    return config;
  }

  return {
    ...config,
    test: {
      ...config.test,
      reporters: ['agent'],
      silent: 'passed-only'
    }
  };
});
