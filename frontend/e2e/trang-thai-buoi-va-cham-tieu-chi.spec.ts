import { expect, test } from '@playwright/test'
import { MAT_KHAU_MOI, taoTrungTam, dangNhap } from './tro-giup'

/**
 * Trạng thái buổi học + chấm tiêu chí riêng từng người đứng lớp (18/09/2026).
 *
 * Ba yêu cầu của chủ sản phẩm, kiểm đầu-cuối qua UI:
 *
 * 1. *"buổi đã qua vẫn hiện đã lên lịch"* → buổi quá giờ phải hiện **Chưa chốt**.
 * 2. *"Quản lý lớp có thể chọn trạng thái cho buổi học"* → đổi được sang Đã xong / Chuyển lịch.
 * 3. *"đánh sao ... thay bằng tiêu chí đánh giá cho giáo viên và trợ giảng"* → phiếu nhận xét
 *    có khối riêng cho GV và trợ giảng, KHÔNG còn ô chấm sao chung.
 */
test('Trạng thái buổi suy theo giờ, đổi được, và học viên chấm riêng GV/trợ giảng', async ({
  page, request,
}) => {
  const tt = await taoTrungTam(request, 'trang-thai-cham')
  await dangNhap(page, tt)

  const tok = await page.evaluate(() => localStorage.getItem('lms_access_token'))
  const api = async (duong: string, than: unknown, method = 'post') => {
    const res = await page.request[method as 'post'](`/api/v1${duong}`, {
      headers: { Authorization: `Bearer ${tok}` },
      data: than,
    })
    const txt = await res.text()
    expect(res.ok(), `${duong} → ${res.status()} ${txt}`).toBeTruthy()
    return txt ? JSON.parse(txt) : null
  }
  const get = async (duong: string) =>
    (await page.request.get(`/api/v1${duong}`, {
      headers: { Authorization: `Bearer ${tok}` },
    })).json()

  const quyens = await get('/quyen')
  const idQuyen = (ten: string) =>
    (quyens as { id: string; tenQuyen: string }[]).find((q) => q.tenQuyen === ten)!.id

  const nguoi = async (hoTen: string, loai: string, username: string, quyen: string) =>
    api('/nguoi-dung', {
      hoTen, loaiNguoiDung: loai,
      taiKhoan: {
        username, matKhau: MAT_KHAU_MOI,
        quyenIds: [idQuyen(quyen)], phaiDoiMatKhau: false,
      },
    })

  const gv = await nguoi('GV Trạng Thái', 'GiaoVien', 'gv-tt', 'Giáo viên')
  const tg = await nguoi('TG Trạng Thái', 'TroGiang', 'tg-tt', 'Trợ giảng')
  const hv = await nguoi('HV Trạng Thái', 'HocVien', 'hv-tt', 'Học viên')

  // Tiêu chí nhóm GiangDay — phiếu chấm dựng từ đây.
  await api('/tieu-chi-danh-gia',
    { ten: 'Truyền đạt dễ hiểu', nhom: 'GiangDay', thuTu: 0, dangDung: true })
  await api('/tieu-chi-danh-gia',
    { ten: 'Nhiệt tình', nhom: 'GiangDay', thuTu: 1, dangDung: true })

  // Lớp có CẢ giáo viên và trợ giảng — không có trợ giảng thì không kiểm được việc tách người.
  const lop = await api('/lop-hoc', {
    ten: 'Lớp trạng thái', giaoVienChinhId: gv, hinhThuc: 'Offline',
    hocPhi: 1000, troGiangIds: [tg],
  })
  await api(`/lop-hoc/${lop}/hoc-vien`, { hocVienIds: [hv] })

  await api(`/lop-hoc/${lop}/sinh-lich`, {
    ngayKhaiGiang: '2026-10-06',
    thuTrongTuan: ['Tuesday', 'Thursday'],
    gioBatDau: '18:00:00', gioKetThuc: '20:00:00', soBuoi: 3,
  })
  await api(`/lop-hoc/${lop}/hoan-tat`, {})

  const buois = await get(`/lop-hoc/${lop}/buoi-hoc`)
  const buoi1 = buois[0].id

  // Đẩy buổi 1 về QUÁ KHỨ — đây là ca lỗi được báo.
  const batDau = new Date(Date.now() - 3 * 86400_000).toISOString()
  await api(`/buoi-hoc/${buoi1}`, {
    id: buoi1, batDau, ketThuc: new Date(Date.parse(batDau) + 2 * 3600_000).toISOString(),
  }, 'put')

  // ---------- 1) Buổi đã qua hiện "Chưa chốt", KHÔNG phải "Đã lên lịch" ----------
  await page.goto(`/lms/lop-hoc/${lop}?tab=lich`)
  await expect(page.locator('tbody tr').first()).toBeVisible({ timeout: 15_000 })

  const badge = page.locator('tbody tr span.rounded-full')
  await expect(badge.filter({ hasText: 'Chưa chốt' }).first()).toBeVisible()
  // Chiều LOẠI: không được còn nhãn thô "Đã lên lịch" trên bảng.
  await expect(badge.filter({ hasText: /^Đã lên lịch$/ })).toHaveCount(0)
  await expect(badge.filter({ hasText: 'Chưa bắt đầu' }).first()).toBeVisible()

  // ---------- 2) Đổi trạng thái qua UI ----------
  const dongBuoi1 = page.locator('tbody tr', { hasText: 'Chưa chốt' }).first()
  // Nhắm NÚT MỞ theo tên: `.locator('button').last()` bắt vào chính mục trong menu vừa mở
  // (menu render bên trong ô thao tác), nên lần bấm thứ hai không bao giờ tới.
  await dongBuoi1.getByRole('button', { name: 'Thao tác' }).click()
  await page.getByRole('menuitem', { name: 'Đổi trạng thái' }).click()
  await page.getByRole('button', { name: /^Đã hoàn thành$/ }).click()
  await page.waitForTimeout(1500)

  await expect(badge.filter({ hasText: 'Đã xong' }).first()).toBeVisible()

  // ---------- 3) Học viên chấm RIÊNG giáo viên và trợ giảng ----------
  await page.getByRole('button', { name: /Đăng xuất/ }).click()
  await page.waitForURL(/dang-nhap/, { timeout: 15_000 })
  await page.fill('#maTrungTam', tt.maTrungTam)
  await page.fill('#username', 'hv-tt')
  await page.fill('#matKhau', MAT_KHAU_MOI)
  await page.click('button[type=submit]')
  await page.waitForURL((u) => !u.pathname.includes('dang-nhap'), { timeout: 30_000 })

  await page.goto(`/lms/buoi-hoc/${buoi1}?tab=nhan-xet`)
  await page.waitForTimeout(2500)

  // Hai khối chấm, mỗi người một khối, kèm nhãn vai trò.
  await expect(page.getByText('GV Trạng Thái').first()).toBeVisible()
  await expect(page.getByText('TG Trạng Thái').first()).toBeVisible()
  await expect(page.getByText('Trợ giảng').first()).toBeVisible()

  // KHÔNG còn ô chấm sao "mức hài lòng" trên form — đây là cái được yêu cầu thay thế.
  await expect(page.getByLabel(/Mức hài lòng chung [1-5]/)).toHaveCount(0)

  // Chấm LỆCH hẳn: GV 5, trợ giảng 1 — rồi đọc lại phải thấy đúng từng người.
  await page.fill('textarea', 'GV dạy tốt, trợ giảng cần chủ động hơn')
  await page.getByRole('button', { name: 'GV Trạng Thái — Truyền đạt dễ hiểu: 5' }).click()
  await page.getByRole('button', { name: 'TG Trạng Thái — Truyền đạt dễ hiểu: 1' }).click()
  await page.getByRole('button', { name: /^Gửi nhận xét$/ }).click()
  // Hộp xác nhận dùng nhãn mặc định "Đồng ý" (xem `xacNhan.tsx`), không lặp lại tên thao tác.
  await page.getByRole('button', { name: /^Đồng ý$/ }).click()
  await page.waitForTimeout(2500)

  // Điểm gom theo NGƯỜI — đọc "GV 5/5" và "TG 1/5" mới có nghĩa.
  const dsNhanXet = page.locator('li', { hasText: 'trợ giảng cần chủ động hơn' })
  await expect(dsNhanXet.getByText(/Truyền đạt dễ hiểu 5\/5/)).toBeVisible()
  await expect(dsNhanXet.getByText(/Truyền đạt dễ hiểu 1\/5/)).toBeVisible()

  // Học viên vẫn ở LMS, KHÔNG bị quyền đọc tiêu chí (thuộc HRM) đẩy sang sidebar nhân sự.
  const menu = (await page.locator('nav a').allInnerTexts()).join(' | ')
  expect(menu, 'học viên phải vẫn thấy menu LMS').toContain('Lớp học')
  expect(menu, 'học viên KHÔNG được thấy menu nhân sự').not.toContain('Nhân sự')
})
