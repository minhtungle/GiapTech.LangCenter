import { expect, test } from '@playwright/test'
import { anhHienThiDuoc, vaoHeThong } from './tro-giup'

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
 * Reload ngay sau click sẽ huỷ request PUT đang bay — test đỏ với "nhận 0 màu" trong khi ứng
 * dụng hoàn toàn đúng. Chờ chỉ dấu "Đã lưu" là cách chắc chắn nhất.
 */
async function luuThietLap(page: import('@playwright/test').Page) {
  await page.locator('button[type=submit]').click()
  await expect(page.locator('text=Đã lưu')).toBeVisible({ timeout: 10_000 })
}

test.describe('Quản trị hệ thống', () => {
  test('sửa hồ sơ cầu thủ không làm mất trường khác', async ({ page, request }) => {
    await vaoHeThong(page, request, 'cau-thu')
    await page.goto('/quan-tri/cau-thu')

    // Tạo hồ sơ có ĐỦ mọi trường.
    await page.click('button:has-text("Thêm cầu thủ")')
    await page.fill('#hoTen', 'Nguyễn Văn Đủ Trường')
    await page.fill('#soAo', '10')
    await page.fill('#viTriSoTruong', 'st')
    await page.fill('#ngaySinh', '1995-05-20')
    await page.fill('#ghiChu', 'ghi chú ban đầu')
    await page.locator('dialog[open] button[type=submit]').click()
    await expect(page.locator('dialog[open]')).toHaveCount(0)

    const hang = page.locator('tbody tr', { hasText: 'Nguyễn Văn Đủ Trường' })
    await expect(hang).toHaveCount(1)

    // Sửa CHỈ một trường — mọi trường khác phải còn nguyên.
    await hang.locator('button[title="Sửa"]').click()
    await page.fill('#hoTen', 'Nguyễn Văn Đã Đổi')
    await page.locator('dialog[open] button[type=submit]').click()
    await expect(page.locator('dialog[open]')).toHaveCount(0)

    await page.reload()
    const hangMoi = page.locator('tbody tr', { hasText: 'Nguyễn Văn Đã Đổi' })
    await hangMoi.locator('button[title="Sửa"]').click()

    await expect(page.locator('#soAo')).toHaveValue('10')
    await expect(page.locator('#viTriSoTruong')).toHaveValue('ST')
    await expect(page.locator('#ngaySinh')).toHaveValue('1995-05-20')
    await expect(page.locator('#ghiChu')).toHaveValue('ghi chú ban đầu')
  })

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

  test('tải ảnh đại diện cầu thủ và ảnh hiển thị được', async ({ page, request }) => {
    await vaoHeThong(page, request, 'anh')
    await page.goto('/quan-tri/cau-thu')

    await page.click('button:has-text("Thêm cầu thủ")')
    await page.fill('#hoTen', 'Cầu Thủ Có Ảnh')
    await page.locator('dialog[open] button[type=submit]').click()
    await expect(page.locator('dialog[open]')).toHaveCount(0)

    const hang = page.locator('tbody tr', { hasText: 'Cầu Thủ Có Ảnh' })
    await hang.locator('button[title="Sửa"]').click()

    // PNG 1×1 tạo tại chỗ, không cần tệp trong repo.
    await page.locator('dialog[open] input[type=file]').setInputFiles({
      name: 'avatar.png',
      mimeType: 'image/png',
      buffer: Buffer.from(
        'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==',
        'base64',
      ),
    })

    // Ảnh phải THẬT SỰ tải được, không chỉ có thẻ img: lỗi 401 vì thẻ img không gửi JWT
    // khiến naturalWidth = 0 trong khi thẻ vẫn "visible".
    await anhHienThiDuoc(page, 'dialog[open] img')

    await page.locator('dialog[open] button[type=submit]').click()
    await expect(page.locator('dialog[open]')).toHaveCount(0)
    await page.reload()
    await anhHienThiDuoc(page, 'tbody img')
  })

  test('SVG bị từ chối kèm thông báo', async ({ page, request }) => {
    // SVG là XML, chứa được <script> → chặn để không mở đường XSS lưu trữ.
    await vaoHeThong(page, request, 'svg')
    await page.goto('/quan-tri/cau-thu')

    await page.click('button:has-text("Thêm cầu thủ")')
    await page.fill('#hoTen', 'Cầu Thủ SVG')
    await page.locator('dialog[open] button[type=submit]').click()

    const hang = page.locator('tbody tr', { hasText: 'Cầu Thủ SVG' })
    await hang.locator('button[title="Sửa"]').click()

    await page.locator('dialog[open] input[type=file]').setInputFiles({
      name: 'x.svg',
      mimeType: 'image/svg+xml',
      buffer: Buffer.from('<svg xmlns="http://www.w3.org/2000/svg"><script>alert(1)</script></svg>'),
    })

    await expect(page.locator('dialog[open]')).toContainText(/JPG, PNG/i)
  })

  test('khai bộ áo đấu rồi đổi tên đội, bộ áo còn nguyên', async ({ page, request }) => {
    await vaoHeThong(page, request, 'mau-ao')
    await page.goto('/quan-tri/thiet-lap')

    await page.click('button[aria-label="Xanh dương"]')
    await page.click('button[aria-label="Vàng"]')
    await luuThietLap(page)

    await page.reload()
    await expect(page.locator('button[aria-pressed="true"]')).toHaveCount(2)

    // Đổi tên đội — bộ áo không được mất (quy tắc #1).
    const tenCu = await page.locator('#tenDoi').inputValue()
    await page.fill('#tenDoi', `${tenCu} Đã Đổi`)
    await luuThietLap(page)

    await page.reload()
    await expect(page.locator('button[aria-pressed="true"]')).toHaveCount(2)
  })
})
