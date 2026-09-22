import { expect, type APIRequestContext, type Page } from '@playwright/test'

/**
 * Trợ giúp dùng chung cho test E2E.
 *
 * **Mỗi test tự tạo trung tâm riêng** qua `/dang-ky-trung-tam` thay vì dùng chung một trung
 * tâm có sẵn: dùng chung thì test này sửa dữ liệu của test kia và kết quả phụ thuộc thứ tự
 * chạy — đúng lỗi đã gặp ở integration test khi nhiều test cùng đổi mật khẩu `admin`.
 */

export interface TrungTam {
  maTrungTam: string
  tenTrungTam: string
  username: string
  matKhau: string
}

/**
 * Mật khẩu sau khi đổi lần đầu — mọi test dùng chung giá trị này.
 *
 * **Phải ≥ 12 ký tự** (`ChinhSachMatKhau.DoDaiToiThieu`, nâng từ 6 lên 12 ngày 22/09/2026).
 * Mật khẩu ngắn hơn sẽ bị validator từ chối và test đỏ ở chỗ trông như lỗi giao diện — đã xảy
 * ra thật với hai test dùng `'matkhau123'` (10 ký tự).
 */
export const MAT_KHAU_MOI = 'e2e-matkhau-123'

/**
 * Tạo trung tâm mới. Tên có timestamp để không trùng khi chạy lại nhiều lần.
 *
 * Dùng `request` của Playwright chứ không qua UI: đăng ký không phải thứ đang được kiểm ở đây,
 * mà đi qua UI cho mọi test sẽ làm mỗi test dài thêm vài giây.
 */
export async function taoTrungTam(request: APIRequestContext, nhan: string): Promise<TrungTam> {
  const res = await request.post('/api/v1/dang-ky-trung-tam', {
    data: { tenTrungTam: `E2E ${nhan} ${Date.now()}` },
  })

  expect(
    res.ok(),
    'Không tạo được trung tâm. Kiểm API đang chạy và `docker compose up -d` đã lên.',
  ).toBeTruthy()

  const body = await res.json()
  return {
    maTrungTam: body.maTrungTam,
    tenTrungTam: body.tenTrungTam,
    username: body.username,
    matKhau: body.matKhau,
  }
}

/**
 * Đăng nhập qua UI và đổi mật khẩu lần đầu.
 *
 * Trung tâm mới luôn bị buộc đổi mật khẩu (middleware chặn ở tầng API, không phó mặc
 * frontend), nên mọi test phải đi qua bước này. Gộp vào helper để không lặp ở từng test.
 */
export async function dangNhap(page: Page, trungTam: TrungTam) {
  await page.goto('/dang-nhap')
  await page.fill('#maTrungTam', trungTam.maTrungTam)
  await page.fill('#username', trungTam.username)
  await page.fill('#matKhau', trungTam.matKhau)
  await page.click('button[type=submit]')

  // Mật khẩu mặc định ai cũng biết → hệ thống buộc đổi trước khi vào.
  await page.waitForURL(/doi-mat-khau/, { timeout: 15_000 })
  await page.fill('#matKhauCu', trungTam.matKhau)
  await page.fill('#matKhauMoi', MAT_KHAU_MOI)

  const oXacNhan = page.locator('#xacNhan')
  if (await oXacNhan.count()) await oXacNhan.fill(MAT_KHAU_MOI)

  await page.locator('button[type=submit]').click()
  await page.waitForURL((u) => u.pathname === '/', { timeout: 15_000 })
}

/**
 * Lấy access token mà TRANG đang giữ trong RAM (ADR-0007, 22/09/2026).
 *
 * ⚠️ **GỌI LẠI sau mỗi `page.goto`/`page.reload`.** Tải trang làm app đổi cookie lấy access
 * token mới, mà refresh token **xoay vòng** nên `PhienHienTai` đổi theo ⇒ token cũ bị
 * `PhienDuyNhatMiddleware` trả **401**. Giữ token trong một biến rồi dùng lại sau khi điều
 * hướng là lỗi im lặng: request đầu còn chạy, request sau 401, và test đỏ ở chỗ trông như lỗi
 * nghiệp vụ.
 *
 * Trước đây test đọc `localStorage.getItem('lms_access_token')`. Từ ADR-0007 token nằm trong
 * biến JS, không còn ở `localStorage`, nên dòng đó trả `null` và 21 tệp test đỏ.
 *
 * **Đã thử cách khác và SAI**: cho helper tự gọi `POST /auth/dang-nhap` để lấy token riêng.
 * Đăng nhập lần hai **đẩy phiên của trình duyệt ra** (một phiên mỗi tài khoản, 20/09/2026), nên
 * chính trang đang test bị đá về màn đăng nhập — 17 test đỏ theo kiểu rất khó đoán, vì lỗi hiện
 * ra ở chỗ "không thấy tab/nút" chứ không ở chỗ xác thực.
 *
 * Nên phải lấy **đúng token của phiên trình duyệt**, không tạo phiên mới.
 */
export async function layTokenQuaApi(page: Page): Promise<string> {
  const token = await page.evaluate(
    () => (window as unknown as { __layTokenTest?: () => string | null }).__layTokenTest?.() ?? null,
  )

  expect(
    token,
    'Không lấy được access token từ trang. Kiểm `__layTokenTest` trong `lib/api.ts` '
    + '(chỉ gắn khi `import.meta.env.DEV`) và trang đã đăng nhập xong chưa.',
  ).toBeTruthy()

  return token!
}


/** Tạo trung tâm + đăng nhập — bước mở đầu của gần như mọi test. */
export async function vaoHeThong(page: Page, request: APIRequestContext, nhan: string) {
  const trungTam = await taoTrungTam(request, nhan)
  await dangNhap(page, trungTam)
  return trungTam
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
