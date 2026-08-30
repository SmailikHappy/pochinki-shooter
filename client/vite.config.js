import { createLogger, defineConfig } from 'vite';

const viteLogger = createLogger();
const logViteError = viteLogger.error.bind(viteLogger);

export function isExpectedWebSocketDisconnect(message, options) {
  const errorCode = options?.error?.code ?? options?.error?.cause?.code;

  return errorCode === 'ECONNRESET' && message.includes('ws proxy');
}

viteLogger.error = (message, options) => {
  if (isExpectedWebSocketDisconnect(message, options)) {
    return;
  }

  logViteError(message, options);
};

const proxy = {
  '/api': {
    target: 'http://localhost:3001',
    changeOrigin: true,
  },
  // Unity Transport speaks its binary NGO protocol over this WebSocket.
  // Keeping it on the Vite origin lets the same WebGL build use ws:// locally
  // and wss:// through Cloudflare/Discord without hard-coded public URLs. The
  // path must not prefix the /ngo-spike build directory: Vite proxy keys match
  // by prefix and would otherwise forward the WebGL page itself to UTP.
  '/ngo-ws': {
    target: 'ws://localhost:7777',
    ws: true,
  },
};

export default defineConfig({
  envDir: '../',
  customLogger: viteLogger,
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
