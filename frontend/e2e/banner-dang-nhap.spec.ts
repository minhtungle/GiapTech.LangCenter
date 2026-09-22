import { expect, test } from '@playwright/test'
import { taoTrungTam } from './tro-giup'

/**
 * Bố cục màn đăng nhập: banner trái + form phải + footer bản quyền (22/09/2026).
 *
 * Yêu cầu: *"màn hình đăng nhập đang hơi trống — đưa khung đăng nhập sang phải, bên trái để
 * hiển thị 1 khung banner được setting trong thiết lập như logo, nếu chưa có hãy để mặc định,
 * nhớ làm responsive... footer có 1 dòng bản quyền phần mềm của GiapTex"*.
 *
 * Bốn điều canh, mỗi điều ứng một cách hỏng thật:
 *
 * 1. **Chưa gõ mã vẫn có nội dung** — để trống là quay lại đúng cái *"hơi trống"* cần chữa.
 * 2. **Gõ đúng mã thì banner mang thông tin trung tâm**.
 * 3. **Footer luôn có dòng GiapTex**, kể cả khi chưa biết trung tâm nào.
 * 4. **Điện thoại ẩn banner và KHÔNG cuộn ngang** — nhồi banner vào màn hẹp thì form bị đẩy
 *    xuống dưới nếp gấp, tệ hơn hẳn so với không có banner.
 */
test('Banner trái + form phải + footer, và responsive trên màn hẹp', async ({
  browser, request,
}) => {
  const tt = await taoTrungTam(request, 'banner')

  // ---------- Desktop ----------
  const may = await browser.newContext({ viewport: { width: 1440, height: 900 } })
  const page = await may.newPage()

  await page.goto('/dang-nhap')

  // 1: chưa gõ mã — banner vẫn có tiêu đề mặc định, không để nửa màn trống.
  const banner = page.locator('h2').first()
  await expect(banner).toBeVisible()
  await expect(banner).toContainText('Hệ thống quản lý trung tâm ngoại ngữ')

  // 3: footer có dòng bản quyền GiapTex ngay từ đầu.
  const footer = page.locator('footer')
  await expect(footer).toContainText('GiapTex')

  // 2: gõ đúng mã — banner đổi sang thông tin trung tâm.
  await page.fill('#maTrungTam', tt.maTrungTam)
  await expect(banner).toContainText(tt.tenTrungTam, { timeout: 15_000 })
  // Footer cũng mang tên trung tâm, nhưng vẫn giữ dòng GiapTex.
  await expect(footer).toContainText('GiapTex')

  // Form nằm bên PHẢI banner — so toạ độ, không so class (class đổi là test vỡ oan).
  const hopBanner = await page.locator('h2').first().boundingBox()
  const hopForm = await page.getByRole('button', { name: /^Đăng nhập$/ }).boundingBox()
  expect(hopBanner!.x, 'banner phải nằm bên trái form').toBeLessThan(hopForm!.x)

  await may.close()

  // ---------- Điện thoại ----------
  const dt = await browser.newContext({ viewport: { width: 390, height: 844 } })
  const trangDt = await dt.newPage()
  await trangDt.goto('/dang-nhap')
  await trangDt.fill('#maTrungTam', tt.maTrungTam)
  await trangDt.waitForTimeout(2000)

  /*
    4a: banner KHÔNG hiển thị.

    Kiểm `toBeHidden`, KHÔNG kiểm `toHaveCount(0)`: `hidden lg:flex` của Tailwind ẩn bằng CSS
    nên phần tử vẫn nằm trong DOM. Kiểm số lượng thì test đỏ oan dù giao diện đúng — đã gặp
    đúng vậy khi viết test này.
  */
  await expect(trangDt.locator('h2').first()).toBeHidden()

  // 4b: KHÔNG cuộn ngang. Đây là lỗi kinh điển của bố cục hai cột trên màn hẹp.
  const cuonNgang = await trangDt.evaluate(
    () => document.documentElement.scrollWidth > window.innerWidth + 1)
  expect(cuonNgang, 'màn điện thoại không được cuộn ngang').toBe(false)

  // 4c: nút đăng nhập phải thấy được, không bị đẩy khỏi màn.
  await expect(trangDt.getByRole('button', { name: /^Đăng nhập$/ })).toBeVisible()

  // Footer vẫn còn trên điện thoại.
  await expect(trangDt.locator('footer')).toContainText('GiapTex')

  await dt.close()
})
