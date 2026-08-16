import path from 'node:path'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: { '@': path.resolve(__dirname, './src') },
  },
  server: {
    port: 5173,
    // Proxy /api sang backend khi dev: frontend và API cùng origin nên không vướng CORS,
    // giống hệt lúc chạy thật sau Caddy (xem docs/ha-tang/README.md).
    proxy: {
      '/api': {
        target: 'http://localhost:5229',
        changeOrigin: true,
        secure: false,
      },
    },
  },
})
