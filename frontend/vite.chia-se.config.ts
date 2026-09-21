/*
  Cấu hình TẠM để chia sẻ bản dev qua đường hầm (20/09/2026).

  Để ở /tmp chứ KHÔNG sửa `vite.config.ts` của dự án: đây là cấu hình dùng một lần rồi đóng,
  lỡ commit vào repo thì thành cửa mở thường trực mà không ai nhớ đã bật.

  Khác bản gốc đúng hai điểm:
    · `host: true`      — nghe mọi giao diện mạng, không chỉ localhost (đường hầm cần vào được)
    · `allowedHosts`    — Vite 5+ CHẶN host lạ để chống DNS rebinding; không khai thì trình duyệt
                          chỉ nhận được "Blocked request. This host is not allowed."
*/
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
    host: true,
    allowedHosts: ['.trycloudflare.com'],
    proxy: {
      '/api': {
        target: 'http://localhost:5229',
        changeOrigin: true,
        secure: false,
      },
    },
  },
})
