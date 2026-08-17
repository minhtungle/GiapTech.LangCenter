import { defineConfig, devices } from '@playwright/test'

/**
 * Cấu hình test E2E.
 *
 * **Chạy trên bản build thật qua Caddy** (`localhost:8080`), không phải `vite dev`: lỗi chỉ
 * xuất hiện ở bản build đã xảy ra thật trong dự án này — `tsc --noEmit` báo sạch trong khi
 * `vite build` bắt 5 lỗi, và Recharts v3 đổi API khiến bản build đỏ mà dev vẫn chạy.
 *
 * Test cần API + PostgreSQL + MinIO đang chạy: `docker compose up -d` trước khi chạy.
 * Không dùng `webServer` của Playwright vì nó chỉ dựng được frontend, còn cả cụm backend thì
 * không — dựng nửa vời sẽ cho cảm giác an toàn sai.
 */
export default defineConfig({
  testDir: './e2e',
  // Chạy tuần tự: các test dùng chung một CLB thật trong DB, chạy song song sẽ tranh nhau
  // sửa cùng bản ghi. Đổi được khi nào mỗi test tự tạo CLB riêng.
  fullyParallel: false,
  workers: 1,
  // Không cho phép `test.only` lọt vào CI — nó làm CI xanh trong khi chỉ chạy một test.
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  timeout: 30_000,

  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:8080',
    // Chỉ giữ vết khi lỗi: ảnh và trace của mọi lần chạy sẽ ngốn hàng trăm MB.
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
    locale: 'vi-VN',
  },

  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],
})
