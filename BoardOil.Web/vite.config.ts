import { defineConfig, loadEnv } from 'vite';
import vue from '@vitejs/plugin-vue';

export default defineConfig(({ command, mode }) => {
  if (command === 'build') {
    const env = loadEnv(mode, process.cwd(), '');
    const version = env.VITE_BO_VERSION?.trim();
    if (!version) {
      throw new Error(
        'Missing required build env var VITE_BO_VERSION. '
        + 'Set it explicitly or run npm scripts that provide it.'
      );
    }
  }

  return {
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
        '/api': 'http://localhost:5000',
        '/images': 'http://localhost:5000',
        '/hubs': {
          target: 'http://localhost:5000',
          ws: true
        }
      }
    }
  };
});
