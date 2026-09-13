import { expect, test } from '@playwright/test'
import { vaoHeThong } from './tro-giup'

/** Lưu + xác nhận. Hộp "Xác nhận lưu" thêm 07/09/2026 cho MỌI thao tác ghi. */
async function luuVaXacNhan(page: import('@playwright/test').Page) {
  await page.getByRole('button', { name: /^lưu$/i }).click()
  await page.getByRole('button', { name: 'Đồng ý' }).click()
}

/**
 * FR-25 — cấp tài khoản học viên **từ CRM** (13/09/2026).
 *
 * Thay cho màn `/lms/hoc-vien` đã bỏ: học viên quản lý tập trung ở CRM. Điểm quan trọng nhất
 * mà test này canh: tạo từ đây thì hồ sơ **tự nối** với khách hàng, nên cột "Học viên" của
 * khách chuyển từ "chưa vào học" sang tên hồ sơ. Nối hỏng thì một con người thành hai hồ sơ ở
 * hai hệ thống — đúng rủi ro khiến chúng tôi gom việc tạo hồ sơ về một chỗ.
 */
test('FR-25: cấp tài khoản học viên từ màn Khách hàng, hồ sơ tự nối', async ({ page, request }) => {
  await vaoHeThong(page, request, 'cap-tk-crm')

  // Tạo khách hàng
  await page.goto('/crm/khach-hang')
  await page.getByRole('button', { name: /thêm/i }).first().click()
  await page.locator('#hoTen').fill('Khách mua khoá online')
  await page.locator('#soDienThoai').fill('0911222333')
  await luuVaXacNhan(page)
  await expect(page.getByText('Khách mua khoá online')).toBeVisible({ timeout: 10000 })

  // Trước khi cấp: khách chưa có hồ sơ học viên
  const dong = page.locator('tr', { hasText: 'Khách mua khoá online' })
  await expect(dong).toContainText(/chưa vào học/i)

  // Cấp tài khoản học viên từ menu thao tác
  await dong.getByRole('button', { name: /thao tác/i }).click()
  await page.getByRole('menuitem', { name: /cấp tài khoản học viên/i })
    .or(page.getByText(/cấp tài khoản học viên/i))
    .first()
    .click()

  await page.locator('#username').fill('hv.online.01')
  await page.locator('#matKhau').fill('matkhau123')
  await luuVaXacNhan(page)

  // SAU khi cấp: hồ sơ phải TỰ NỐI — cột Học viên hiện tên, không còn "chưa vào học".
  await expect(dong).not.toContainText(/chưa vào học/i, { timeout: 10000 })
  await expect(dong).toContainText('Khách mua khoá online')
})

/** Menu LMS không còn mục Học viên, và route cũ không còn. */
test('FR-25: module Học viên đã bỏ khỏi LMS', async ({ page, request }) => {
  await vaoHeThong(page, request, 'bo-hoc-vien')

  await page.goto('/lms/lop-hoc')
  await page.waitForTimeout(1000)

  const sidebar = page.locator('aside')
  await expect(sidebar.getByText('Lớp học', { exact: true })).toBeVisible()
  await expect(sidebar.getByText('Học viên', { exact: true })).toHaveCount(0)
})
