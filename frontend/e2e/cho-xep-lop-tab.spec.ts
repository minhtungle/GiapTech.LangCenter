import { expect, test } from '@playwright/test'
import { vaoHeThong, MAT_KHAU_MOI } from './tro-giup'

/**
 * "Chờ xếp lớp" là một TAB của màn Lớp học, không phải module riêng (yêu cầu 10/09/2026).
 *
 * Điều đáng canh nhất **không** phải chuyện tab bấm được, mà là **ai thấy nó**: endpoint
 * `GET /lop-hoc/cho-xep-lop` đòi `LopHoc.Sua`, còn mục menu cũ chỉ kiểm `LopHoc.Xem` — quyền mà
 * giáo viên và học viên **cũng có**. Hậu quả trước 10/09: họ thấy menu "Chờ xếp lớp", bấm vào
 * và nhận **403** (đã kiểm bằng tay: giáo viên có đúng `['Xem']` trên `LopHoc`).
 */
test.describe('Chờ xếp lớp — tab của màn Lớp học', () => {
  test('admin thấy tab; đường cũ vẫn mở được', async ({ page, request }) => {
    await vaoHeThong(page, request, 'cxl-admin')

    await page.goto('/lms/lop-hoc')
    await page.waitForTimeout(1500)

    // Không còn mục menu riêng.
    expect(
      await page.locator('aside').getByRole('link', { name: 'Chờ xếp lớp' }).isVisible(),
    ).toBe(false)

    // Có tab, và bấm được.
    const tabCho = page.getByRole('button', { name: /Chờ xếp lớp/ })
    await expect(tabCho).toBeVisible()
    await expect(page.getByRole('button', { name: 'Danh sách lớp' })).toBeVisible()
    await page.screenshot({ path: '/tmp/c1-tab-danh-sach.png', fullPage: true })

    await tabCho.click()
    await page.waitForTimeout(1200)
    expect(new URL(page.url()).searchParams.get('tab')).toBe('cho-xep-lop')
    // Bảng lớp phải ẨN khi đang ở tab chờ — hai khối loại trừ nhau.
    expect(await page.getByLabel('Lọc theo trạng thái').isVisible()).toBe(false)
    await page.screenshot({ path: '/tmp/c2-tab-cho.png', fullPage: true })

    // Về tab danh sách: `?tab=` biến mất hẳn.
    await page.getByRole('button', { name: 'Danh sách lớp' }).click()
    await page.waitForTimeout(800)
    expect(new URL(page.url()).searchParams.get('tab')).toBe(null)

    // Đường CŨ `/lms/lop-hoc/cho-xep-lop` vẫn còn route riêng → không chết link đã gửi.
    await page.goto('/lms/lop-hoc/cho-xep-lop')
    await page.waitForTimeout(1200)
    await expect(page.getByRole('heading', { name: 'Chờ xếp lớp' })).toBeVisible()
  })

  /**
   * Giáo viên chỉ có `LopHoc.Xem` → **không** được thấy tab, và gõ thẳng `?tab=cho-xep-lop`
   * phải rơi về danh sách chứ không phải tab trắng/403.
   */
  test('giáo viên KHÔNG thấy tab, gõ thẳng ?tab= cũng không vào được', async ({ page, request }) => {
    const tt = await vaoHeThong(page, request, 'cxl-gv')

    // Tạo giáo viên có tài khoản, dùng nhóm quyền "Giáo viên" mặc định.
    const quyens = await (await request.get('/api/v1/quyen', {
      headers: { Authorization: `Bearer ${await page.evaluate(
        () => localStorage.getItem('lms_access_token'))}` },
    })).json()
    const quyenGv = quyens.find((q: { tenQuyen: string }) => q.tenQuyen === 'Giáo viên').id

    const res = await request.post('/api/v1/nguoi-dung', {
      headers: { Authorization: `Bearer ${await page.evaluate(
        () => localStorage.getItem('lms_access_token'))}` },
      data: {
        hoTen: 'GV không thấy tab', loaiNguoiDung: 'GiaoVien',
        taiKhoan: {
          username: 'gv-cxl', matKhau: MAT_KHAU_MOI,
          quyenIds: [quyenGv], phaiDoiMatKhau: false,
        },
      },
    })
    expect(res.ok(), 'không tạo được giáo viên').toBeTruthy()

    // Đăng nhập lại bằng giáo viên.
    await page.goto('/dang-nhap')
    await page.fill('#maTrungTam', tt.maTrungTam)
    await page.fill('#username', 'gv-cxl')
    await page.fill('#matKhau', MAT_KHAU_MOI)
    await page.click('button[type=submit]')
    await page.waitForURL((u) => u.pathname === '/', { timeout: 15_000 })

    await page.goto('/lms/lop-hoc')
    await page.waitForTimeout(2000)
    await page.screenshot({ path: '/tmp/c3-giao-vien.png', fullPage: true })

    // Không thấy tab chờ xếp lớp, cũng không còn menu riêng.
    expect(await page.getByRole('button', { name: /Chờ xếp lớp/ }).isVisible()).toBe(false)
    expect(
      await page.locator('aside').getByRole('link', { name: 'Chờ xếp lớp' }).isVisible(),
    ).toBe(false)

    // Gõ thẳng query cũng rơi về danh sách lớp (bảng lớp vẫn hiện).
    await page.goto('/lms/lop-hoc?tab=cho-xep-lop')
    await page.waitForTimeout(1500)
    await expect(page.getByLabel('Lọc theo trạng thái')).toBeVisible()
    expect(await page.getByRole('heading', { name: 'Chờ xếp lớp' }).isVisible()).toBe(false)
  })
})
