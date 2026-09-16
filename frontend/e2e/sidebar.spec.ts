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

  /**
   * Bộ chuyển hệ thống phải chuyển được từ MỌI trang, kể cả trang thuộc một hệ thống khác.
   *
   * Lỗi thật (09/09/2026): đứng ở `/crm/khach-hang` bấm HRM thì `doi('Hrm')` ghi localStorage,
   * rồi effect "URL thắng" đọc lại đường dẫn `/crm/...` và ghi đè về `Crm` sau 16ms — bộ chuyển
   * như chết trên mọi trang thuộc hệ thống, tức gần như toàn bộ app. Từ Tổng quan (`/`) thì
   * chuyển được nên rất dễ bỏ sót khi thử tay.
   *
   * Sửa bằng cách ĐIỀU HƯỚNG sang trang của hệ thống đích, để URL và lựa chọn nói cùng chuyện.
   */
  test('chuyển hệ thống được từ trang thuộc hệ thống khác', async ({ page, request }) => {
    await vaoHeThong(page, request, 'chuyen-he-thong')

    const nutChuyen = page.locator('button').filter({ hasText: /HRM|CRM|LMS/ }).first()
    const doi = async (ten: string) => {
      await nutChuyen.click()
      await page.locator('div.absolute button').filter({ hasText: ten }).first().click()
      await page.waitForTimeout(600)
    }

    // Vào một trang CRM — đây là điều kiện gây lỗi, không phải Tổng quan.
    await page.goto('/crm/khach-hang')
    await page.waitForTimeout(1200)
    await expect(nutChuyen).toContainText('CRM')

    // Chuyển sang HRM: cả nhãn, localStorage và URL đều phải theo.
    await doi('HRM')
    await expect(nutChuyen).toContainText('HRM')
    expect(await page.evaluate(() => localStorage.getItem('lms_he_thong'))).toBe('Hrm')
    // `/hrm` (không có đoạn sau) cũng hợp lệ: từ 16/09/2026 HRM có trang đích gộp ba tab, nên
    // bộ chuyển điều hướng thẳng tới đó. `Layout` suy hệ thống con bằng `startsWith('/hrm')`
    // nên cả hai dạng đều nhận đúng.
    expect(new URL(page.url()).pathname).toMatch(/^\/hrm(\/|$)/)

    // Và chuyển tiếp được — lỗi cũ kẹt luôn từ lần thứ hai.
    await doi('LMS')
    await expect(nutChuyen).toContainText('LMS')
    expect(await page.evaluate(() => localStorage.getItem('lms_he_thong'))).toBe('Lms')

    await doi('CRM')
    await expect(nutChuyen).toContainText('CRM')
    expect(await page.evaluate(() => localStorage.getItem('lms_he_thong'))).toBe('Crm')
  })

  /**
   * Chiều ngược: mở link trực tiếp thì URL vẫn THẮNG lựa chọn đã lưu.
   *
   * Đây là chức năng cái effect kia tồn tại để làm (thêm 08/09/2026) — sửa lỗi trên không được
   * làm hỏng nó, nếu không mở bookmark sẽ hiện sidebar của hệ thống khác.
   */
  test('URL thắng lựa chọn đã lưu khi mở link trực tiếp', async ({ page, request }) => {
    await vaoHeThong(page, request, 'url-thang')

    await page.evaluate(() => localStorage.setItem('lms_he_thong', 'Lms'))
    await page.goto('/crm/doanh-thu')
    await page.waitForTimeout(1200)

    expect(await page.evaluate(() => localStorage.getItem('lms_he_thong'))).toBe('Crm')
    await expect(
      page.locator('button').filter({ hasText: /HRM|CRM|LMS/ }).first(),
    ).toContainText('CRM')
  })
})
