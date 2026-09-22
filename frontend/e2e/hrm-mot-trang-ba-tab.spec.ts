import { expect, test } from '@playwright/test'
import { vaoHeThong, layTokenQuaApi } from './tro-giup'

/**
 * HRM gộp một trang ba tab + bấm sĩ số ra danh sách người (16/09/2026).
 *
 * Người dùng báo hai việc:
 * - *"cơ cấu tổ chức đang chưa xem được chi tiết danh sách nhân sự"*
 * - *"hồ sơ nhân viên, cơ cấu, chức vụ đang bị tách biệt"*
 *
 * ## Vì sao phải là E2E, không phải test đơn vị
 *
 * Việc cần kiểm là **một chuỗi bắc qua ba lớp**: link trên cây mang query string → React Router
 * → state khởi tạo của màn Nhân sự → tham số gửi lên API → số dòng trong bảng. Đứt ở bất kỳ mắt
 * nào cũng cho ra "bảng có dữ liệu", nên chỉ đếm dòng thật mới biết đúng hay sai.
 *
 * Đã đứt thật ở hai mắt trong lúc làm: link không mang `trangThaiNhanSu`, và màn Nhân sự không
 * đọc tham số đó từ URL. Cả hai đều không làm đỏ test nào của backend.
 */

/** Đếm dòng dữ liệu thật trong bảng (bỏ hàng tiêu đề và dòng "không có dữ liệu"). */
async function soDong(page: import('@playwright/test').Page) {
  await page.waitForTimeout(600)
  return page.locator('tbody tr').filter({ hasNot: page.locator('td[colspan]') }).count()
}

test('Một trang ba tab, bấm sĩ số ra ĐÚNG những người đang làm việc', async ({ page, request }) => {
  const ttE2E = await vaoHeThong(page, request, 'hrm-tab')

  const token = await layTokenQuaApi(page)
  const api = async (duong: string, than: unknown, method: 'post' | 'put' = 'post') => {
    const res = await page.request[method](`http://localhost:5229/api/v1${duong}`, {
      headers: { Authorization: `Bearer ${token}` },
      data: than,
    })
    expect(res.ok(), `${duong} → ${res.status()} ${await res.text()}`).toBeTruthy()
    return res.status() === 204 ? null : res.json()
  }

  /**
   * Nút TRÊN THANH TAB.
   *
   * Không dùng `getByRole('button', {name: 'Chức vụ'})` trần: màn Nhân sự cũng có ô lọc
   * "Chức vụ" (`#loc-chuc-vu`), nên tên đó khớp hai phần tử và Playwright báo strict mode
   * violation. Khoanh theo container thanh tab.
   */
  const tab = (ten: RegExp) =>
    page.locator('div.rounded-lg.border').first().getByRole('button', { name: ten })

  const loiJs: string[] = []
  page.on('pageerror', (e) => loiJs.push(e.message))

  // Cây hai cấp: cha có 2 người (1 đã nghỉ), con có 1 người.
  //  → sĩ số hiện "1 / 2": riêng = 1 (chỉ người đang làm), cả nhánh = 2.
  const cha = await api('/phong-ban', { ten: 'Khối kinh doanh', thuTu: 0 })
  const con = await api('/phong-ban', { ten: 'Tổ telesale', phongBanChaId: cha, thuTu: 0 })

  await api('/nhan-su', { hoTen: 'Anh Đang Làm', loaiNguoiDung: 'NhanVien', phongBanId: cha })
  const nghi = await api('/nhan-su',
    { hoTen: 'Chị Đã Nghỉ', loaiNguoiDung: 'NhanVien', phongBanId: cha })
  await api('/nhan-su', { hoTen: 'Em Tổ Dưới', loaiNguoiDung: 'NhanVien', phongBanId: con })

  await api(`/nhan-su/${nghi}`, {
    id: nghi, hoTen: 'Chị Đã Nghỉ', loaiNguoiDung: 'NhanVien', trangThaiNhanSu: 'DaNghi',
  }, 'put')

  // --- Một mục sidebar, ba tab ---
  await page.goto('/hrm')
  await expect(tab(/Cơ cấu tổ chức/)).toBeVisible({ timeout: 15000 })
  await expect(tab(/Hồ sơ nhân sự/)).toBeVisible()
  await expect(tab(/Chức vụ/)).toBeVisible()

  // --- Sĩ số hiện "riêng / cả nhánh" ---
  await expect(page.getByText('Khối kinh doanh')).toBeVisible({ timeout: 15000 })
  await expect(page.getByText('1 / 2', { exact: true })).toBeVisible()

  // --- CA CHÍNH: bấm sĩ số → sang tab Nhân sự, lọc sẵn, ĐÚNG SỐ NGƯỜI ---
  await page.getByText('1 / 2', { exact: true }).click()
  await page.waitForURL(/tab=nhan-su/, { timeout: 15000 })
  expect(page.url()).toContain('phongBanId=')

  // Cây nói 1 thì danh sách phải ra 1 — không phải 2 (lẫn người đã nghỉ).
  expect(await soDong(page), 'bấm sĩ số "1" phải ra đúng 1 người').toBe(1)
  await expect(page.getByText('Anh Đang Làm')).toBeVisible()
  await expect(page.getByText('Chị Đã Nghỉ')).toHaveCount(0)
  await expect(page.getByText('Em Tổ Dưới')).toHaveCount(0)   // phòng con, chưa gom

  // --- Tích "gồm cấp dưới": ra thêm người của tổ dưới ---
  await page.getByLabel(/Gồm cả phòng cấp dưới/i).check()
  expect(await soDong(page), 'gồm cấp dưới phải tăng số dòng').toBe(2)
  await expect(page.getByText('Em Tổ Dưới')).toBeVisible()

  // --- Chuyển tab vẫn giữ bộ lọc phòng ban (không bắt chọn lại) ---
  await tab(/Chức vụ/).click()
  await expect(page).toHaveURL(/tab=chuc-vu/)
  expect(page.url(), 'đổi tab làm mất phongBanId').toContain('phongBanId=')

  // --- Đường cũ vẫn vào được (link đã lưu, bookmark) ---
  for (const [cu, moi] of [
    ['/hrm/co-cau', 'tab=so-do'],
    ['/hrm/nhan-su', 'tab=nhan-su'],
    ['/hrm/chuc-vu', 'tab=chuc-vu'],
  ]) {
    await page.goto(cu)
    await expect(page, `${cu} phải chuyển hướng sang ${moi}`).toHaveURL(new RegExp(moi), {
      timeout: 15000,
    })
  }

  expect(loiJs, 'có lỗi JS chưa xử lý').toEqual([])
})
