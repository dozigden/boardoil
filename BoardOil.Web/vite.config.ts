import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';

export default defineConfig({
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
      '/hubs': {
        target: 'http://localhost:5000',
        ws: true
      }
    }
  }
});
