import { defineConfig } from 'vite';

export default defineConfig({
  envDir: '../',
  server: {
    port: 5173,
    strictPort: true,
    allowedHosts: ['.trycloudflare.com'],
    proxy: {
      '/api': {
        target: 'http://localhost:3001',
        changeOrigin: true,
      },
    },
    hmr: {
      clientPort: 443,
    },
  },
});
