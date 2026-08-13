import { readFileSync } from 'node:fs';
import { fileURLToPath, URL } from 'node:url';
import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';

const demoHeaders = readFileSync(new URL('./demo/_headers', import.meta.url), 'utf8');
const demoSourceRepository = 'https://github.com/dozigden/boardoil';
const demoSourceCommit = process.env.BOARDOIL_DEMO_SOURCE_SHA?.trim() || 'local';

if (demoSourceCommit !== 'local' && !/^[0-9a-f]{40}$/i.test(demoSourceCommit)) {
  throw new Error('BOARDOIL_DEMO_SOURCE_SHA must be a full 40-character Git commit SHA.');
}

const demoDeployment = `${JSON.stringify(
  {
    artifact: 'static-demo',
    sourceRepository: demoSourceRepository,
    sourceCommit: demoSourceCommit
  },
  null,
  2
)}\n`;
const demoDistributionNotice = `This repository is generated. Do not edit its files directly.

Source: ${demoSourceRepository}
Commit: ${demoSourceCommit}
Publication: run the Publish static demo workflow in the source repository.
`;

export default defineConfig({
  root: fileURLToPath(new URL('./demo', import.meta.url)),
  base: './',
  define: {
    'import.meta.env.VITE_BO_BROWSER_STORAGE_MODE': JSON.stringify('disabled')
  },
  publicDir: fileURLToPath(new URL('./public', import.meta.url)),
  plugins: [
    {
      name: 'demo-distribution-files',
      generateBundle() {
        this.emitFile({
          type: 'asset',
          fileName: '_headers',
          source: demoHeaders
        });
        this.emitFile({
          type: 'asset',
          fileName: 'deployment.json',
          source: demoDeployment
        });
        this.emitFile({
          type: 'asset',
          fileName: 'DISTRIBUTION_NOTICE.txt',
          source: demoDistributionNotice
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
