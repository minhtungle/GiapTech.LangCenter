import { expect, test } from '@playwright/test'

/**
 * Cảnh báo trùng số điện thoại **ngay khi gõ**, trước khi bấm Lưu (14/09/2026).
 *
 * Trước đây người dùng phải bấm Lưu mới biết trùng, rồi tự đóng form đi tìm khách đó bằng tay.
 * Nay gõ xong số là hiện cảnh báo kèm nút mở thẳng hồ sơ — việc tiếp theo của họ luôn là xem
 * người đã có ấy.
 *
 * Chạy trên dữ liệu mẫu của `W686AE9`; cần `scripts/tao-du-lieu-mau.py` đã chạy.
 */
test('cảnh báo trùng sđt ngay khi gõ', async ({ page }) => {
  await page.goto('/dang-nhap')
  await page.fill('#maTrungTam', 'W686AE9')
  await page.fill('#username', 'admin')
  await page.fill('#matKhau', 'Admin@12345')
  await page.click('button[type=submit]')
  await page.waitForURL((u) => u.pathname === '/', { timeout: 15000 })

  await page.goto('/crm/khach-hang')
  await page.getByRole('button', { name: /thêm khách hàng/i }).click()
  await page.fill('#hoTen', 'Người mới hoàn toàn')
  await page.fill('#soDienThoai', '0900002025')
  await page.waitForTimeout(1500)
  await page.screenshot({ path: 'trung.png' })

  // Cảnh báo hiện TRONG dialog, kèm tên khách đã có
  const hop = page.locator('dialog[open]')
  await expect(hop.getByText(/Số này đã là của Lê Ngọc An/)).toBeVisible({ timeout: 5000 })

  // Nút mở hồ sơ đi đúng tới view chi tiết của người đó
  await hop.getByRole('button', { name: /Mở hồ sơ khách này/ }).click()
  await expect(page).toHaveURL(/\/crm\/khach-hang\/[0-9a-f-]{36}/, { timeout: 10000 })
})
