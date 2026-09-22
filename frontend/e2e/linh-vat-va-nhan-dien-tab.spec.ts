import { expect, test } from '@playwright/test'
import { vaoHeThong } from './tro-giup'

/**
 * **Linh vật · thanh tiến trình · favicon + tiêu đề tab** (22/09/2026 — yêu cầu chủ sản phẩm).
 *
 * Ba thứ trang trí/tiện ích, nhưng mỗi thứ có một cách hỏng thật đã gặp khi làm:
 *
 * - Linh vật **đè lên footer** ở màn đăng nhập (biến CSS không tới được component vì nó mount
 *   ngoài cây DOM của trang).
 * - Chữ trong bong bóng thoại **bị viết ngược** ở nửa sau chu kỳ (`scaleX(-1)` để nhân vật
 *   quay đầu áp cho cả cây con).
 * - Favicon đặt thẳng `/api/v1/anh/{khoa}` thì **401** — thẻ `<link>` không gắn được token.
 *
 * Cả ba chỉ lộ ra khi nhìn màn hình thật, nên phần nào kiểm được bằng test thì kiểm ở đây.
 */

test('Tiêu đề tab và favicon đổi theo trung tâm ở màn đăng nhập', async ({ page, request }) => {
  const tt = await vaoHeThong(page, request, 'tab-dang-nhap')

  // Đăng xuất để về màn đăng nhập rồi gõ mã.
  await page.getByRole('button', { name: /Đăng xuất|Thoát/i }).first().click()
  await page.waitForURL(/dang-nhap/, { timeout: 15_000 })

  await page.fill('#maTrungTam', tt.maTrungTam)

  // Tiêu đề mang tên trung tâm — người mở nhiều tab nhận ra ngay tab nào của trung tâm nào.
  await expect
    .poll(() => page.title(), { timeout: 15_000 })
    .toContain(tt.tenTrungTam)
})

test('Trong quản trị: tiêu đề tab mang tên trung tâm', async ({ page, request }) => {
  const tt = await vaoHeThong(page, request, 'tab-quan-tri')

  await expect.poll(() => page.title(), { timeout: 15_000 }).toContain(tt.tenTrungTam)
})

/**
 * Linh vật **KHÔNG che footer** — lỗi đã gặp thật.
 *
 * Kiểm bằng toạ độ chứ không bằng class: class đổi là test vỡ oan, còn "có chồng lên nhau
 * không" mới là điều thật sự cần đúng.
 */
test('Linh vật đi phía TRÊN footer, không đè lên dòng bản quyền', async ({ page, request }) => {
  const tt = await vaoHeThong(page, request, 'linh-vat-footer')
  await page.getByRole('button', { name: /Đăng xuất|Thoát/i }).first().click()
  await page.waitForURL(/dang-nhap/, { timeout: 15_000 })
  await page.fill('#maTrungTam', tt.maTrungTam)
  await page.waitForTimeout(1500)

  const hopFooter = await page.locator('footer').boundingBox()
  const hopLinhVat = await page.locator('svg').last().boundingBox()

  expect(hopFooter, 'phải có footer ở màn đăng nhập').not.toBeNull()
  expect(hopLinhVat, 'phải thấy linh vật').not.toBeNull()

  // Đáy linh vật phải nằm TRÊN đỉnh footer.
  expect(
    hopLinhVat!.y + hopLinhVat!.height,
    'linh vật đè lên footer — kiểm biến `--linh-vat-day`',
  ).toBeLessThanOrEqual(hopFooter!.y + 2)
})

/**
 * **Tắt được, và nhớ lựa chọn.**
 *
 * Người dùng ngồi với hệ thống cả ngày; một nhân vật chuyển động trong tầm mắt ngoại vi là
 * thứ gây phân tâm kinh điển. Nút tắt không chỉ phải có, mà phải **nhớ** — bắt tắt lại mỗi
 * lần tải trang thì thà không có.
 */
test('Tắt được linh vật, và lựa chọn được nhớ sau khi tải lại', async ({ page, request }) => {
  await vaoHeThong(page, request, 'tat-linh-vat')
  await page.waitForTimeout(1200)

  const nut = page.getByRole('button', { name: /Ẩn nhân vật/ })
  await expect(nut).toHaveCount(1)

  // Nút chỉ hiện khi rê chuột (opacity-0 + group-hover), nên bấm thẳng bằng `force`.
  await nut.click({ force: true })
  await expect(page.getByRole('button', { name: /Ẩn nhân vật/ })).toHaveCount(0)

  await page.reload()
  await page.waitForTimeout(1500)
  await expect(
    page.getByRole('button', { name: /Ẩn nhân vật/ }),
    'đã tắt thì tải lại vẫn phải tắt',
  ).toHaveCount(0)
})

/**
 * Linh vật **không chặn thao tác** khi đi ngang qua nút bấm.
 *
 * Đây là lỗi kinh điển của mọi thứ nổi trên màn: quên `pointer-events-none` thì nhân vật
 * nuốt cú click của người dùng, và triệu chứng là "thỉnh thoảng bấm không ăn" — gần như không
 * thể lần ra từ báo lỗi của người dùng.
 */
test('Linh vật KHÔNG nuốt cú click của trang', async ({ page, request }) => {
  await vaoHeThong(page, request, 'linh-vat-click')
  await page.waitForTimeout(1200)

  const lop = page.locator('div.fixed.bottom-\\[var\\(--linh-vat-day\\,0px\\)\\]').first();
  if (await lop.count()) {
    await expect(lop).toHaveCSS('pointer-events', 'none')
  }

  // Và trang vẫn bấm được bình thường.
  await page.getByRole('link', { name: /Tổng quan/ }).first().click()
  await expect(page).toHaveURL(/\/$|tong-quan/)
})
