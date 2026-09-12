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
/**
 * Mở modal Sửa của một dòng bảng.
 *
 * Nút Sửa nằm trong `MenuThaoTac` từ 07/09/2026, không còn là `button[title="Sửa"]` bày sẵn
 * trên dòng — gom nhiều nút vào menu để cột thao tác không thành dải icon khó phân biệt.
 */
async function moSua(hang: import('@playwright/test').Locator) {
  await hang.getByRole('button', { name: 'Thao tác' }).click()
  await hang.page().getByText('Sửa', { exact: true }).click()
}

/**
 * Bấm Lưu trong modal rồi ĐỒNG Ý ở hộp xác nhận.
 *
 * Từ 07/09/2026 mọi thao tác ghi đều hỏi xác nhận — bấm submit thôi thì modal không đóng.
 */
async function luuVaXacNhan(page: import('@playwright/test').Page) {
  await page.locator('dialog[open] button[type=submit]').click()
  await page.getByRole('button', { name: 'Đồng ý' }).click()
  await expect(page.locator('dialog[open]')).toHaveCount(0)
}

async function luuThietLap(page: import('@playwright/test').Page) {
  await page.locator('button[type=submit]').click()
  // Xác nhận trước khi ghi — thêm 07/09/2026 cho MỌI thao tác ghi.
  await page.getByRole('button', { name: 'Đồng ý' }).click()
  await expect(page.locator('text=Đã lưu')).toBeVisible({ timeout: 10_000 })
}

test.describe('Quản trị hệ thống', () => {
  /**
   * Canh quy tắc #1 trên màn **Người dùng**, không phải Tài khoản.
   *
   * Địa chỉ và email chuyển sang màn **Hồ sơ nhân sự** (`/hrm/nhan-su`) khi tách người ≠ tài
   * khoản (07/09/2026) rồi chia theo hệ thống con (08/09) — modal Tài khoản nay chỉ còn
   * username, nhóm quyền, trạng thái. Test cũ trỏ màn Tài khoản nên đỏ liên tục từ trước 10/09
   * (nợ N16): **app đúng, test lạc hậu**.
   *
   * Tầng integration đã canh chặt hơn (`CapNhatKhongMatDuLieuTests`); giữ bản E2E này vì nó
   * kiểm cả FORM — lỗi 16/08 nằm ở form thiếu ô, không ở handler.
   */
  test('sửa người dùng không làm mất địa chỉ', async ({ page, request }) => {
    // Chính sự cố ngày 16/08 — quy tắc #1 sinh ra từ đây.
    await vaoHeThong(page, request, 'tai-khoan')
    await page.goto('/hrm/nhan-su')

    const hang = page.locator('tbody tr', { hasText: 'Quản trị viên' }).first()
    await moSua(hang)

    await page.fill('#diaChi', '123 Đường Test, Đà Nẵng')
    await page.fill('#email', 'test@example.com')
    await luuVaXacNhan(page)

    // Sửa lại CHỈ email — địa chỉ phải còn.
    await moSua(hang)
    await expect(page.locator('#diaChi')).toHaveValue('123 Đường Test, Đà Nẵng')
    await page.fill('#email', 'doi@example.com')
    await luuVaXacNhan(page)

    await page.reload()
    await moSua(page.locator('tbody tr', { hasText: 'Quản trị viên' }).first())
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
