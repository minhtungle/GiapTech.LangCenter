import { expect, test } from '@playwright/test'
import { vaoHeThong } from './tro-giup'

/**
 * Sidebar phải cao đúng MỘT viewport và dính tại chỗ khi trang cuộn.
 *
 * Lỗi thật (21/08): `<aside>` là flex item thường trong container `min-h-screen`, nên nó giãn theo
 * chiều cao của cả trang. Đo trên màn Thống kê: viewport 700px, trang 1413px, **sidebar 1412px**
 * — cuộn xuống đáy thì logo và menu trôi khỏi màn hình (`top = -713`) và người dùng phải cuộn
 * ngược lên mới đổi được trang.
 *
 * Không test nào bắt được vì nó không làm gì đỏ: mọi phần tử vẫn đúng chỗ trong DOM.
 */
test.describe('Sidebar', () => {
  test('KHÔNG giãn theo chiều cao trang, và dính khi cuộn', async ({ page, request }) => {
    // Viewport thấp để chắc chắn trang dài hơn màn hình.
    await page.setViewportSize({ width: 1440, height: 700 })
    await vaoHeThong(page, request, 'sidebar-cao')

    // Cần trang DÀI hơn viewport, và **không phụ thuộc dữ liệu**.
    //
    // Chọn màn KHÔNG phụ thuộc dữ liệu: `/quan-tri/thiet-lap` là form nhiều trường, luôn dài
    // hơn viewport 700px kể cả với trung tâm vừa tạo, và nó không có bảng nên không bị ảnh
    // hưởng nếu sau này cho bảng cuộn. Chốt an toàn ở dưới bắt được nếu giả định này hết đúng.
    await page.goto('/quan-tri/thiet-lap')
    await page.waitForTimeout(1500)

    const soDo = await page.evaluate(() => {
      const a = document.querySelector('aside')!.getBoundingClientRect()
      return {
        vh: window.innerHeight,
        trang: document.documentElement.scrollHeight,
        sidebar: Math.round(a.height),
      }
    })

    // Điều kiện tiên quyết: trang PHẢI dài hơn viewport, nếu không test này vô nghĩa.
    expect(
      soDo.trang,
      'trang không dài hơn viewport — test không kiểm được gì, cần màn có nhiều dữ liệu hơn',
    ).toBeGreaterThan(soDo.vh + 100)

    // Sidebar cao đúng một viewport, KHÔNG theo chiều cao trang.
    expect(soDo.sidebar).toBeLessThanOrEqual(soDo.vh + 1)

    // Cuộn xuống đáy: sidebar vẫn ở đỉnh màn hình.
    await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight))
    await page.waitForTimeout(400)

    const sauCuon = await page.evaluate(() => {
      const a = document.querySelector('aside')!.getBoundingClientRect()
      return { top: Math.round(a.top), bottom: Math.round(a.bottom) }
    })
    // `toBeCloseTo` chứ không `toBe(0)`: `Math.round(-0.2)` cho `-0`, mà `toBe` phân biệt `-0`
    // với `0` nên test đỏ dù sidebar đúng vị trí.
    expect(sauCuon.top, 'sidebar trôi khỏi màn hình sau khi cuộn').toBeCloseTo(0, 0)

    // Và menu bấm được ngay ở đáy trang — đây là hậu quả thật của lỗi.
    await page.click('a[href="/quan-tri/tai-khoan"]')
    await expect(page).toHaveURL(/quan-tri\/tai-khoan/)
  })

  test('màn hình thấp: menu cuộn TRONG sidebar, nút Đăng xuất vẫn thấy', async ({
    page,
    request,
  }) => {
    // Ở viewport rất thấp, nút Đăng xuất (nằm dưới cùng sidebar) không được bị đẩy ra ngoài
    // màn hình — nếu bị thì không ai đăng xuất được.
    //
    // Bản base chỉ có 4 mục menu nên nav CHƯA cần cuộn ở 420px; test vì thế chỉ khẳng định
    // "Đăng xuất thấy được", không khẳng định "nav cuộn được". Khi thêm module và menu dài
    // hơn viewport, thêm lại khẳng định `nav.scrollHeight > nav.clientHeight`.
    await vaoHeThong(page, request, 'sidebar-thap')
    await page.setViewportSize({ width: 1280, height: 420 })
    await page.goto('/quan-tri/thiet-lap')
    await page.waitForTimeout(1200)

    const d = await page.evaluate(() => {
      const nav = document.querySelector('aside nav')!
      const dangXuat = [...document.querySelectorAll('aside button')]
        .find((e) => e.textContent?.includes('Đăng xuất'))!
        .getBoundingClientRect()
      return {
        dangXuatTrongTamNhin: dangXuat.top >= 0 && dangXuat.bottom <= window.innerHeight,
        navCaoHopLe: nav.clientHeight > 0,
      }
    })

    expect(d.navCaoHopLe).toBeTruthy()
    expect(d.dangXuatTrongTamNhin, 'nút Đăng xuất bị đẩy ra ngoài màn hình').toBeTruthy()
  })
})
