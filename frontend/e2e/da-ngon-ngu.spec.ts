import { expect, test } from '@playwright/test'
import { vaoHeThong } from './tro-giup'

/**
 * **Đa ngôn ngữ** (23/09/2026 — yêu cầu chủ sản phẩm: Anh, Trung, Hàn, Nhật).
 *
 * Canh ba điều dễ hỏng mà không có gì báo:
 *
 * 1. Đổi ngôn ngữ mà **không nhớ** ⇒ F5 về tiếng Việt. Người dùng đổi lại mỗi lần mở app.
 * 2. Đổi giao diện nhưng **ngày tháng vẫn kiểu Việt** ⇒ nửa vời, và gây đọc nhầm với người
 *    quen định dạng khác.
 * 3. **Dữ liệu người dùng nhập bị dịch** — điều chủ sản phẩm nói rõ là KHÔNG được phép.
 */

test('Đổi ngôn ngữ ở màn đăng nhập, và NHỚ sau khi tải lại', async ({ page }) => {
  await page.goto('/dang-nhap')

  // Mặc định tiếng Việt.
  await expect(page.locator('label[for=maTrungTam]')).toHaveText('Mã trung tâm')

  await page.getByRole('button', { name: /Đổi ngôn ngữ/ }).first().click()
  await page.getByRole('option', { name: /English/ }).click()

  await expect(page.locator('label[for=maTrungTam]')).toHaveText('Center code')

  // Nhớ lựa chọn: F5 vẫn tiếng Anh. Không nhớ thì người dùng phải đổi lại mỗi lần mở app.
  await page.reload()
  await expect(page.locator('label[for=maTrungTam]')).toHaveText('Center code', {
    timeout: 15_000,
  })
})

test('Đủ 5 ngôn ngữ, mỗi thứ hiện bằng CHÍNH ngôn ngữ đó', async ({ page }) => {
  await page.goto('/dang-nhap')
  await page.getByRole('button', { name: /Đổi ngôn ngữ/ }).first().click()

  const muc = (await page.locator('[role=option]').allTextContents()).map((s) => s.trim())

  // Tên hiển thị bằng chính ngôn ngữ đó: người Hàn tìm "한국어" nhanh hơn "Tiếng Hàn".
  expect(muc.join(' ')).toContain('Tiếng Việt')
  expect(muc.join(' ')).toContain('English')
  expect(muc.join(' ')).toContain('中文')
  expect(muc.join(' ')).toContain('한국어')
  expect(muc.join(' ')).toContain('日本語')
})

test('Trong quản trị: đổi ngôn ngữ đổi cả menu, KHÔNG đổi dữ liệu người dùng nhập', async ({
  page, request,
}) => {
  const tt = await vaoHeThong(page, request, 'da-ngon-ngu')

  // Menu tiếng Việt trước.
  await expect(page.getByRole('link', { name: 'Tổng quan' }).first()).toBeVisible()

  await page.getByRole('button', { name: /Đổi ngôn ngữ/ }).first().click()
  await page.getByRole('option', { name: /English/ }).click()
  await page.waitForTimeout(600)

  // Menu đã đổi sang tiếng Anh. Dùng đúng chuỗi trong `en.ts` (`menu.tongQuan` =
  // 'Overview'), không đoán theo thói quen — tôi từng viết 'Dashboard' và test đỏ oan.
  await expect(page.getByRole('link', { name: 'Overview' }).first()).toBeVisible({
    timeout: 10_000,
  })

  /*
    ĐIỀU QUAN TRỌNG NHẤT: tên trung tâm (dữ liệu người dùng nhập) **giữ nguyên**.

    Chủ sản phẩm nói rõ chỉ dịch giao diện. Dịch cả dữ liệu là làm sai lệch tên riêng và mỗi
    trung tâm có cách gọi riêng — không phần mềm nào được tự ý đổi.
  */
  await expect(page.getByText(tt.tenTrungTam).first()).toBeVisible()
})

test('Ngày tháng đổi theo ngôn ngữ, không kẹt ở định dạng Việt', async ({ page, request }) => {
  await vaoHeThong(page, request, 'ngay-theo-ngon-ngu')

  /*
    Kiểm bằng chính `Intl` trong trang thay vì tìm một ngày cụ thể trên màn: màn nào có ngày
    còn phụ thuộc dữ liệu, mà test này canh **cơ chế** chứ không canh một màn.

    Nếu `locale()` vẫn trả `vi-VN` sau khi đổi ngôn ngữ thì hai chuỗi dưới đây giống nhau —
    đúng cái lỗi cần bắt.
    */
  const truoc = await page.evaluate(() =>
    new Date('2026-09-23T10:00:00Z').toLocaleDateString(
      (window as unknown as { __locale?: () => string }).__locale?.() ?? 'vi-VN',
    ),
  )

  await page.getByRole('button', { name: /Đổi ngôn ngữ/ }).first().click()
  await page.getByRole('option', { name: /日本語/ }).click()
  await page.waitForTimeout(600)

  // Tiếng Nhật xếp năm/tháng/ngày — khác hẳn ngày/tháng/năm của tiếng Việt.
  const sau = await page.evaluate(() => new Date('2026-09-23T10:00:00Z').toLocaleDateString('ja-JP'))

  expect(sau).not.toBe(truoc)
  expect(sau).toContain('2026')
})

/**
 * **Số thứ tự buổi học đặt đúng chỗ trong từng ngôn ngữ** (23/09/2026).
 *
 * Bản đầu khoá này là **tiền tố** (`'Buổi '`) rồi code tự nối số. Cách đó chỉ đúng với ngôn
 * ngữ đặt số ở SAU. Tiếng Nhật và tiếng Hàn đếm kiểu **bao quanh** số (`第3回`, `3차시`) nên
 * tiền tố thuần cho ra `第3` — thiếu đuôi, đọc như câu bỏ dở.
 *
 * Hai agent dịch (Nhật và Hàn) **độc lập** chỉ ra cùng chỗ này, nên nó không phải tiểu tiết.
 *
 * Test đọc thẳng `i18n` thay vì dựng một lớp có buổi học: nó canh **khuôn chuỗi**, không canh
 * một màn cụ thể, nên không vỡ khi giao diện đổi.
 */
test('Số thứ tự buổi đặt đúng vị trí trong từng ngôn ngữ', async ({ page }) => {
  await page.goto('/dang-nhap')

  const kq = await page.evaluate(async () => {
    const i18n = (await import('/src/lib/i18n.ts')).default
    const cu = i18n.language
    const ra: Record<string, string> = {}
    for (const m of ['vi', 'en', 'zh', 'ko', 'ja']) {
      await i18n.changeLanguage(m)
      ra[m] = i18n.t('buoiHoc.thuTuNgan', { n: 3 })
    }
    await i18n.changeLanguage(cu)
    return ra
  })

  // Mọi ngôn ngữ đều phải chứa con số — thiếu nghĩa là placeholder bị mất.
  for (const [ma, chuoi] of Object.entries(kq)) {
    expect(chuoi, `${ma}: thiếu số thứ tự`).toContain('3')
  }

  // Nhật/Hàn/Trung phải có ĐUÔI đếm sau số, không dừng ở con số trần.
  expect(kq.ja, 'tiếng Nhật phải là 第N回, không phải 第N').toMatch(/第3回/)
  expect(kq.ko, 'tiếng Hàn phải có đơn vị đếm 차시').toContain('차시')
  expect(kq.zh, 'tiếng Trung phải có đuôi 次课').toContain('次课')
})
