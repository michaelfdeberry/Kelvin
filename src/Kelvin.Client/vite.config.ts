import { defineConfig } from 'vite';

// Modern browsers only - no legacy transpilation/polyfill target needed.
export default defineConfig({
  build: {
    target: 'es2022',
    outDir: '../Kelvin.Server/wwwroot',
    emptyOutDir: true,
  },
  server: {
    proxy: {
      '/api': 'http://localhost:5209',
      '/hubs': {
        target: 'http://localhost:5209',
        ws: true,
      },
    },
  },
});
