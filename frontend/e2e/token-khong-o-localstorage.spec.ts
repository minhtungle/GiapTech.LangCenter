import { expect, test } from '@playwright/test'
import { vaoHeThong } from './tro-giup'

/**
 * **Token không còn nằm trong `localStorage`** — ADR-0007 (22/09/2026), mục 7 của đợt rà soát.
 *
 * Trước đây cả access lẫn refresh token đều ở `localStorage`, nên một lỗ XSS — hoặc một gói npm
 * bị chiếm, thực tế hơn nhiều — lấy được refresh token và mạo danh **30 ngày**.
 *
 * Test này đứng ở đúng vị trí của kẻ tấn công: chạy JavaScript trong trang rồi tìm token.
 */
test('XSS không lấy được token: localStorage trống, cookie phiên là httpOnly', async ({
  page, request,
}) => {
  await vaoHeThong(page, request, 'token-ram')

  // ---------- Đứng ở vị trí XSS: đọc mọi thứ JS đọc được ----------
  const nhinThay = await page.evaluate(() => ({
    localStorage: Object.entries(localStorage),
    sessionStorage: Object.entries(sessionStorage),
    cookieDocDuoc: document.cookie,
  }))

  const chuoiTatCa = JSON.stringify(nhinThay)

  // Không có gì trông giống JWT (ba đoạn base64 ngăn bằng dấu chấm, bắt đầu bằng `eyJ`).
  expect(chuoiTatCa, 'JS không được thấy access token').not.toMatch(/eyJ[\w-]+\.eyJ[\w-]+\./)

  // Không có khoá cũ nào sót lại.
  const khoaLocal = nhinThay.localStorage.map(([k]) => k)
  expect(khoaLocal).not.toContain('lms_access_token')
  expect(khoaLocal).not.toContain('lms_refresh_token')

  /*
    Cookie phiên KHÔNG được nằm trong `document.cookie` — đó chính là ý nghĩa của `httpOnly`.

    Cookie CSRF thì ngược lại, PHẢI đọc được (frontend cần gửi lại trong header). Kiểm cả hai
    chiều để không ai "sửa cho nhất quán" theo hướng sai.
  */
  expect(nhinThay.cookieDocDuoc, 'refresh token phải httpOnly').not.toContain('lms_rt=')
  expect(nhinThay.cookieDocDuoc, 'cookie CSRF CỐ Ý đọc được').toContain('lms_csrf=')
})

/**
 * **F5 không đá người dùng ra** — cái giá của việc đưa access token vào RAM.
 *
 * Access token nằm trong biến JS nên tải lại trang là mất. App phải đổi cookie `httpOnly` lấy
 * token mới lúc khởi động. Quên bước đó — hoặc route guard không chờ nó xong — thì **mỗi lần
 * F5 là văng về màn đăng nhập**, một hồi quy rất dễ mắc và rất dễ bị người dùng phát hiện.
 */
test('Tải lại trang (F5) vẫn giữ nguyên phiên, không văng về màn đăng nhập', async ({
  page, request,
}) => {
  await vaoHeThong(page, request, 'f5-giu-phien')

  await page.goto('/lms/lop-hoc')
  await expect(page).toHaveURL(/lop-hoc/)

  await page.reload()

  // Vẫn ở đúng trang cũ, không bị đẩy sang /dang-nhap.
  await expect(page).toHaveURL(/lop-hoc/, { timeout: 15_000 })
  await expect(page.locator('nav a').first()).toBeVisible({ timeout: 15_000 })
})

/**
 * Mở **tab mới** cũng vào thẳng, không phải đăng nhập lại.
 *
 * Khác F5 ở chỗ tab mới có RAM hoàn toàn trống ngay từ đầu — nếu app chỉ khôi phục được nhờ
 * thứ gì đó còn sót trong bộ nhớ tab cũ thì ca này sẽ lộ ra.
 */
test('Tab mới vào thẳng nhờ cookie, không phải đăng nhập lại', async ({ page, request }) => {
  await vaoHeThong(page, request, 'tab-moi-cookie')

  const tabMoi = await page.context().newPage()
  await tabMoi.goto('/lms/lop-hoc')

  await expect(tabMoi).toHaveURL(/lop-hoc/, { timeout: 15_000 })
  await expect(tabMoi.locator('nav a').first()).toBeVisible({ timeout: 15_000 })
})
