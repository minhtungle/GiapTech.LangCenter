import { expect, test } from '@playwright/test'
import { vaoHeThong } from './tro-giup'

/**
 * Khi một modal mở tiếp hộp xác nhận, CHỈ lớp trên cùng được nhận tương tác.
 *
 * Lỗi thật đã gặp 11/09/2026: hộp xác nhận "Duyệt vào lớp" đè lên modal duyệt, mà **cả hai
 * nút cùng nhãn "Duyệt vào lớp"** (i18n `xepLop.duyet`), cùng màu xanh, cách nhau ~77px.
 * `::backdrop` của dialog trên không che dialog khác trong top layer → bấm nhầm nút của modal
 * DƯỚI thì chỉ mở lại hộp xác nhận: không ghi gì, không lỗi gì, người dùng tưởng đã duyệt
 * xong. F5 mới thấy dòng vẫn nằm trong hàng chờ.
 */
test('modal dưới bị inert khi hộp xác nhận mở; duyệt vẫn ghi đúng', async ({ page, request }) => {
  await vaoHeThong(page, request, 'modal-long')
  const tok = await page.evaluate(() => localStorage.getItem('lms_access_token'))
  const H = { Authorization: `Bearer ${tok}`, 'Content-Type': 'application/json' }
  const J = async (r: { json: () => Promise<unknown> }) => await r.json()

  const gv = await J(await request.post('/api/v1/nhan-su', { headers: H, data: {
    hoTen: 'GV M', email: 'gvm@t.vn', soDienThoai: '0900000041',
    diaChi: null, ngaySinh: null, loaiNguoiDung: 'GiaoVien' } }))
  const kh = await J(await request.post('/api/v1/khoa-hoc', { headers: H, data: {
    ten: 'Khoa M', ghiChu: null, giaTien: 4000000, donViTien: 'VND', soBuoi: 10, dangBan: true } }))
  const khach = await J(await request.post('/api/v1/khach-hang', { headers: H, data: {
    hoTen: 'Khach M', email: 'km@t.vn', soDienThoai: null, linkFacebook: null, ghiChu: null } }))
  const don = await J(await request.post('/api/v1/doanh-thu', { headers: H, data: {
    khachHangId: khach, khoaHocId: kh, soTien: 4000000, donViTien: 'VND',
    tyGiaVeVnd: 1, ngayDangKy: new Date().toISOString() } }))
  await request.post(`/api/v1/doanh-thu/${don}/yeu-cau-xep-lop`, { headers: H, data: {} })
  await J(await request.post('/api/v1/lop-hoc', { headers: H, data: {
    ten: 'Lop M', giaoVienChinhId: gv, hinhThuc: 'Offline', phongHoc: 'P', linkHoc: null,
    hocPhi: 4000000, sucChuaToiDa: 20, ghiChu: null, troGiangIds: [] } }))

  await page.goto('/lms/lop-hoc?tab=cho-xep-lop')
  await page.waitForTimeout(1500)

  await page.getByRole('button', { name: /Duyệt vào lớp/ }).first().click()
  await page.waitForTimeout(600)
  await page.getByRole('button', { name: /Chọn lớp/ }).click()
  await page.waitForTimeout(400)
  await page.locator('ul[role=listbox] li').filter({ hasText: 'Lop M' }).first().click()
  await page.waitForTimeout(300)

  // Phân biệt hai dialog bằng thứ CHỈ có ở mỗi cái: modal duyệt có nút "Chọn lớp";
  // hộp xác nhận có câu "sổ học phí". Lọc theo chữ "Duyệt vào lớp" sẽ khớp CẢ HAI.
  const modalDuyet = page.locator('dialog[open]')
    .filter({ has: page.getByRole('button', { name: /Chọn lớp|Lop M/ }) })
  await modalDuyet.getByRole('button', { name: 'Duyệt vào lớp' }).click()
  await page.waitForTimeout(700)
  await page.screenshot({ path: '/tmp/m1-hai-lop.png', fullPage: true })

  // Modal dưới phải bị inert; hộp xác nhận (lớp trên) thì không.
  expect(await modalDuyet.evaluate((d: HTMLDialogElement) => d.inert),
    'modal dưới phải inert khi hộp xác nhận mở').toBe(true)
  const hopXn = page.locator('dialog[open]').filter({ hasText: 'sổ học phí' })
  expect(await hopXn.evaluate((d: HTMLDialogElement) => d.inert),
    'hộp xác nhận (lớp trên) không được inert').toBe(false)

  // Bấm nút của modal DƯỚI: inert phải làm cú bấm này vô hiệu, không mở thêm lớp nào.
  await modalDuyet.getByRole('button', { name: 'Duyệt vào lớp' })
    .click({ force: true, timeout: 3000 }).catch(() => {})
  await page.waitForTimeout(500)
  expect(await page.locator('dialog[open]').count(), 'không được mở thêm lớp modal').toBe(2)

  // Bấm đúng nút của hộp xác nhận → ghi thật.
  await hopXn.getByRole('button', { name: 'Duyệt vào lớp' }).click()
  await page.waitForTimeout(2000)
  await page.screenshot({ path: '/tmp/m2-sau-duyet.png', fullPage: true })

  // Mọi modal đã đóng và inert được dọn sạch (không khoá modal mở sau đó).
  expect(await page.locator('dialog[open]').count(), 'modal phải đóng hết sau khi duyệt').toBe(0)

  await page.reload()
  await page.waitForTimeout(1800)
  expect(await page.getByText('Khach M').isVisible().catch(() => false),
    'đã duyệt thì phải rời hàng chờ, kể cả sau F5').toBe(false)
  // Endpoint trả `KetQuaTrang` từ 12/09/2026 (trước đó là mảng trần) — đọc `tongSoDong`.
  const api = await (await request.get('/api/v1/lop-hoc/cho-xep-lop', { headers: H })).json()
  expect(api.tongSoDong, 'API hàng chờ phải rỗng').toBe(0)
})
