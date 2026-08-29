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
    // Discord sends the SDK READY handshake only once per mounted Activity iframe.
    // A Vite HMR reload reuses that iframe and leaves discordSdk.ready() hanging.
    hmr: false,
  },
  preview: {
    port: 5173,
    strictPort: true,
    allowedHosts: ['.trycloudflare.com'],
    proxy: {
      '/api': {
        target: 'http://localhost:3001',
        changeOrigin: true,
      },
    },
  },
});
