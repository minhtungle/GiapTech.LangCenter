import path from 'node:path'
import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'

export default defineConfig({
  /*
    Vitest dùng chung cấu hình này (14/09/2026 — test frontend đầu tiên của dự án).

    `include` giới hạn vào `src/`: thư mục `e2e/` là của Playwright, để vitest quét vào đó thì
    nó cố chạy test trình duyệt trong môi trường Node và đỏ 10 file không liên quan.
  */
  test: {
    include: ['src/**/*.test.ts'],
  },
  plugins: [react()],
  resolve: {
    alias: { '@': path.resolve(__dirname, './src') },
  },
  server: {
    port: 5173,
    // Proxy /api sang backend khi dev: frontend và API cùng origin nên không vướng CORS,
    // giống hệt lúc chạy thật sau Caddy (xem docs/07-ha-tang/README.md).
    proxy: {
      '/api': {
        target: 'http://localhost:5229',
        changeOrigin: true,
        secure: false,
      },
    },
  },
})
