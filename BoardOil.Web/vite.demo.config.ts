import { readFileSync } from 'node:fs';
import { fileURLToPath, URL } from 'node:url';
import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';

const demoHeaders = readFileSync(new URL('./demo/_headers', import.meta.url), 'utf8');

export default defineConfig({
  root: fileURLToPath(new URL('./demo', import.meta.url)),
  base: './',
  define: {
    'import.meta.env.VITE_BO_BROWSER_STORAGE_MODE': JSON.stringify('disabled')
  },
  publicDir: fileURLToPath(new URL('./public', import.meta.url)),
  plugins: [
    {
      name: 'demo-cloudflare-headers',
      generateBundle() {
        this.emitFile({
          type: 'asset',
          fileName: '_headers',
          source: demoHeaders
        });
      }
    },
    vue({
      template: {
        compilerOptions: {
          isCustomElement: tag => tag === 'emoji-picker'
        }
      }
    })
  ],
  build: {
    outDir: fileURLToPath(new URL('./dist-demo', import.meta.url)),
    emptyOutDir: true
  },
  server: {
    port: 5174
  }
});
