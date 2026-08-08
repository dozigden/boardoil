import { loadEnv } from 'vite';
import { configDefaults, defineConfig } from 'vitest/config';
import vue from '@vitejs/plugin-vue';

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
