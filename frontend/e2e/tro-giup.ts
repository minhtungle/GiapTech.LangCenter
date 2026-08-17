import { expect, type APIRequestContext, type Page } from '@playwright/test'

/**
 * Trợ giúp dùng chung cho test E2E.
 *
 * **Mỗi test tự tạo CLB riêng** qua `/dang-ky-clb` thay vì dùng chung một CLB có sẵn: dùng
 * chung thì test này sửa dữ liệu của test kia và kết quả phụ thuộc thứ tự chạy — đúng lỗi đã
 * gặp ở integration test khi nhiều test cùng đổi mật khẩu `admin`.
 *
 * Endpoint `/dang-ky-clb` chỉ chạy ở Development (trả 404 ở Production), nên test E2E cũng chỉ
 * chạy được với API ở Development. Đó là đánh đổi có ý thức: cách còn lại là mở đường tạo CLB
 * ở production chỉ để phục vụ test.
 */

export interface Clb {
  maDoi: string
  tenDoi: string
  username: string
  matKhau: string
}

/** Mật khẩu sau khi đổi lần đầu — mọi test dùng chung giá trị này. */
export const MAT_KHAU_MOI = 'e2e-matkhau-123'

/**
 * Tạo CLB mới. Tên có timestamp để không trùng khi chạy lại nhiều lần.
 *
 * Dùng `request` của Playwright chứ không qua UI: đăng ký không phải thứ đang được kiểm ở đây,
 * mà đi qua UI cho mọi test sẽ làm mỗi test dài thêm vài giây.
 */
export async function taoClb(request: APIRequestContext, nhan: string): Promise<Clb> {
  const res = await request.post('/api/v1/dang-ky-clb', {
    data: { tenDoi: `E2E ${nhan} ${Date.now()}` },
  })

  expect(
    res.ok(),
    'Không tạo được CLB. Kiểm API đang chạy ở Development và `docker compose up -d` đã lên.',
  ).toBeTruthy()

  const body = await res.json()
  return {
    maDoi: body.maDoi,
    tenDoi: body.tenDoi,
    username: body.username,
    matKhau: body.matKhau,
  }
}

/**
 * Đăng nhập qua UI và đổi mật khẩu lần đầu.
 *
 * CLB mới luôn bị buộc đổi mật khẩu (middleware chặn ở tầng API, không phó mặc frontend), nên
 * mọi test phải đi qua bước này. Gộp vào helper để không lặp ở từng test.
 */
export async function dangNhap(page: Page, clb: Clb) {
  await page.goto('/dang-nhap')
  await page.fill('#maDoi', clb.maDoi)
  await page.fill('#username', clb.username)
  await page.fill('#matKhau', clb.matKhau)
  await page.click('button[type=submit]')

  // Mật khẩu mặc định ai cũng biết → hệ thống buộc đổi trước khi vào.
  await page.waitForURL(/doi-mat-khau/, { timeout: 15_000 })
  await page.fill('#matKhauCu', clb.matKhau)
  await page.fill('#matKhauMoi', MAT_KHAU_MOI)

  const oXacNhan = page.locator('#xacNhan')
  if (await oXacNhan.count()) await oXacNhan.fill(MAT_KHAU_MOI)

  await page.locator('button[type=submit]').click()
  await page.waitForURL((u) => u.pathname === '/', { timeout: 15_000 })
}

/** Tạo CLB + đăng nhập — bước mở đầu của gần như mọi test. */
export async function vaoHeThong(page: Page, request: APIRequestContext, nhan: string) {
  const clb = await taoClb(request, nhan)
  await dangNhap(page, clb)
  return clb
}

/**
 * Đóng dropdown của SelectTimKiem bằng cách bấm lại chính nút select (nó là toggle).
 *
 * Ba cách khác đều sai:
 * - `Escape` đóng cả `<dialog>` bao ngoài chứ không chỉ dropdown.
 * - Bấm phần tử khác trên trang: dropdown đang mở che nó, Playwright chờ mãi cho tới khi
 *   phần tử "stable" rồi timeout.
 * - Bấm toạ độ (5,5): đó là vùng backdrop của modal nên cũng đóng luôn modal.
 */
export async function dongDropdown(page: Page, selectorSelect: string) {
  await page.locator(selectorSelect).click()
  await expect(page.locator('ul[role=listbox]')).toHaveCount(0)
}

/** Chờ ảnh tải xong và kiểm nó thật sự hiển thị được (naturalWidth > 0). */
export async function anhHienThiDuoc(page: Page, selector: string) {
  const img = page.locator(selector).first()
  await expect(img).toBeVisible()

  // `toBeVisible` không đủ: thẻ img lỗi vẫn "visible" nhưng naturalWidth = 0. Đúng lỗi đã gặp
  // khi ảnh trả 401 vì thẻ img không gửi JWT.
  await expect
    .poll(async () => img.evaluate((n: HTMLImageElement) => n.naturalWidth), { timeout: 10_000 })
    .toBeGreaterThan(0)
}
