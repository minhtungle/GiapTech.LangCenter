import { expect, test } from '@playwright/test'
import { vaoHeThong } from './tro-giup'

/**
 * FR-03 → FR-06 — cụm quản trị.
 *
 * Trọng tâm: **cập nhật không làm mất dữ liệu** (quy tắc #1). Sự cố ngày 16/08 — form sửa tài
 * khoản thiếu ô địa chỉ nên âm thầm xoá địa chỉ mỗi lần lưu — lọt qua toàn bộ test backend vì
 * backend nhận đúng thứ frontend gửi. Chỉ test đi qua UI mới bắt được.
 */

/**
 * Bấm Lưu và CHỜ xác nhận trước khi reload.
 *
 * Reload ngay sau click sẽ huỷ request PUT đang bay — test đỏ trong khi ứng dụng hoàn toàn
 * đúng. Chờ chỉ dấu "Đã lưu" là cách chắc chắn nhất.
 */
async function luuThietLap(page: import('@playwright/test').Page) {
  await page.locator('button[type=submit]').click()
  await expect(page.locator('text=Đã lưu')).toBeVisible({ timeout: 10_000 })
}

test.describe('Quản trị hệ thống', () => {
  test('sửa tài khoản không làm mất địa chỉ', async ({ page, request }) => {
    // Chính sự cố ngày 16/08 — quy tắc #1 sinh ra từ đây.
    await vaoHeThong(page, request, 'tai-khoan')
    await page.goto('/quan-tri/tai-khoan')

    const hang = page.locator('tbody tr', { hasText: 'admin' }).first()
    await hang.locator('button[title="Sửa"]').click()

    await page.fill('#diaChi', '123 Đường Test, Đà Nẵng')
    await page.fill('#email', 'test@example.com')
    await page.locator('dialog[open] button[type=submit]').click()
    await expect(page.locator('dialog[open]')).toHaveCount(0)

    // Sửa lại CHỈ email — địa chỉ phải còn.
    await hang.locator('button[title="Sửa"]').click()
    await expect(page.locator('#diaChi')).toHaveValue('123 Đường Test, Đà Nẵng')
    await page.fill('#email', 'doi@example.com')
    await page.locator('dialog[open] button[type=submit]').click()
    await expect(page.locator('dialog[open]')).toHaveCount(0)

    await page.reload()
    await page.locator('tbody tr', { hasText: 'admin' }).first()
      .locator('button[title="Sửa"]').click()
    await expect(page.locator('#diaChi')).toHaveValue('123 Đường Test, Đà Nẵng')
    await expect(page.locator('#email')).toHaveValue('doi@example.com')
  })

  /**
   * Cùng quy tắc #1 nhưng ở màn Thiết lập: đổi MỘT trường không được xoá trường khác.
   *
   * Đáng có test riêng vì Thiết lập dùng quy ước khác màn Tài khoản: backend hiểu `null` là
   * "client không gửi, giữ nguyên" còn `''` là "chủ động xoá". Gửi sai một trong hai thì trường
   * người dùng không chạm tới sẽ biến mất — âm thầm, đúng kiểu sự cố 16/08.
   */
  test('đổi tên trung tâm không làm mất thông tin chuyển khoản', async ({ page, request }) => {
    await vaoHeThong(page, request, 'thiet-lap')
    await page.goto('/quan-tri/thiet-lap')

    await page.fill('#soTaiKhoan', '0123456789')
    await page.fill('#tenNganHang', 'Vietcombank')
    await page.fill('#diaChi', '99 Lê Duẩn, Đà Nẵng')
    await luuThietLap(page)

    await page.reload()
    await expect(page.locator('#soTaiKhoan')).toHaveValue('0123456789')

    // Đổi CHỈ tên trung tâm — ba trường kia phải còn nguyên.
    const tenCu = await page.locator('#tenTrungTam').inputValue()
    await page.fill('#tenTrungTam', `${tenCu} Đã Đổi`)
    await luuThietLap(page)

    await page.reload()
    await expect(page.locator('#tenTrungTam')).toHaveValue(`${tenCu} Đã Đổi`)
    await expect(page.locator('#soTaiKhoan')).toHaveValue('0123456789')
    await expect(page.locator('#tenNganHang')).toHaveValue('Vietcombank')
    await expect(page.locator('#diaChi')).toHaveValue('99 Lê Duẩn, Đà Nẵng')
  })
})
