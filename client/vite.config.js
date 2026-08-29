import { defineConfig } from 'vite';

const proxy = {
  '/api': {
    target: 'http://localhost:3001',
    changeOrigin: true,
  },
  '/ws': {
    target: 'ws://localhost:3001',
    ws: true,
  },
};

export default defineConfig({
  envDir: '../',
  server: {
    port: 5173,
    strictPort: true,
    allowedHosts: ['.trycloudflare.com'],
    proxy,
    // Discord sends the SDK READY handshake only once per mounted Activity iframe.
    // A Vite HMR reload reuses that iframe and leaves discordSdk.ready() hanging.
    hmr: false,
  },
  preview: {
    port: 5173,
    strictPort: true,
    allowedHosts: ['.trycloudflare.com'],
    proxy,
  },
});
