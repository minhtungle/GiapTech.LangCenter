import { expect, test } from '@playwright/test'
import { MAT_KHAU_MOI, taoTrungTam, dangNhap } from './tro-giup'

/**
 * Lưu điểm danh khi còn học viên vắng CHƯA có lý do (18/09/2026).
 *
 * Người dùng báo: *"lưu điểm danh đang lỗi không lưu được"*. Tái hiện đúng vậy — và điều tệ
 * nhất không phải việc bị chặn, mà là **không nói vì sao**:
 *
 * - Bảng mặc định cho mọi học viên là `Vắng`, ô "Lý do vắng" trống.
 * - Backend bắt buộc vắng phải có lý do (`THIEU_LY_DO_VANG`) — chủ ý, *"báo cáo vắng không lý
 *   do là báo cáo vô dụng"*.
 * - Nhưng lỗi trả theo TỪNG DÒNG trong `duLieu.truong`, còn `layMaLoi` chỉ đọc `errorCode` ở
 *   tầng ngoài ⇒ người dùng thấy đúng một câu **"Dữ liệu nhập vào chưa hợp lệ"**, không biết
 *   thiếu ở đâu trong 6 dòng.
 *
 * Test canh **cả hai chiều**: chặn kèm chỉ dẫn rõ ràng khi thiếu, và **lưu được** khi đã đủ.
 * Thiếu chiều thứ hai thì một bản sửa "khoá nút vĩnh viễn" cũng xanh.
 */
test('Thiếu lý do vắng thì nói rõ ai thiếu; nhập đủ thì lưu được', async ({ page, request }) => {
  const tt = await taoTrungTam(request, 'luu-diem-danh')
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

  const gv = await api('/nguoi-dung', {
    hoTen: 'GV Điểm Danh', loaiNguoiDung: 'GiaoVien',
    taiKhoan: {
      username: 'gv-dd', matKhau: MAT_KHAU_MOI,
      quyenIds: [idQuyen('Giáo viên')], phaiDoiMatKhau: false,
    },
  })

  // BA học viên: đủ để phân biệt "dòng nào thiếu" với "cả bảng thiếu".
  const hvIds: string[] = []
  for (const ten of ['An', 'Bình', 'Cường']) {
    hvIds.push(await api('/nguoi-dung', { hoTen: `HV ${ten}`, loaiNguoiDung: 'HocVien' }))
  }

  const lop = await api('/lop-hoc', {
    ten: 'Lớp điểm danh', giaoVienChinhId: gv, hinhThuc: 'Offline',
    hocPhi: 1000, troGiangIds: [],
  })
  await api(`/lop-hoc/${lop}/hoc-vien`, { hocVienIds: hvIds })
  const buois = await api(`/lop-hoc/${lop}/sinh-lich`, {
    ngayKhaiGiang: '2026-10-06',
    thuTrongTuan: ['Tuesday'],
    gioBatDau: '18:00:00', gioKetThuc: '20:00:00', soBuoi: 1,
  })
  await api(`/lop-hoc/${lop}/hoan-tat`, {})
  const buoi = buois[0].id

  // ---------- Vào bằng nick GIÁO VIÊN: đúng người làm việc này ----------
  await page.getByRole('button', { name: /Đăng xuất/ }).click()
  await page.waitForURL(/dang-nhap/, { timeout: 15_000 })
  await page.fill('#maTrungTam', tt.maTrungTam)
  await page.fill('#username', 'gv-dd')
  await page.fill('#matKhau', MAT_KHAU_MOI)
  await page.click('button[type=submit]')
  await page.waitForURL((u) => !u.pathname.includes('dang-nhap'), { timeout: 30_000 })

  await page.goto(`/lms/buoi-hoc/${buoi}?tab=diem-danh`)
  await expect(page.locator('tbody tr').first()).toBeVisible({ timeout: 15_000 })

  const nutLuu = page.getByRole('button', { name: /^Lưu điểm danh$/ })

  // ---------- CHIỀU 1: thiếu lý do → chặn, và NÓI RÕ ai thiếu ----------
  await expect(nutLuu).toBeDisabled()

  const canhBao = page.locator('[role=alert]')
  await expect(canhBao).toContainText('HV An')
  await expect(canhBao).toContainText('HV Bình')
  await expect(canhBao).toContainText('HV Cường')
  // Phải gợi ý đường thoát, không chỉ báo lỗi.
  await expect(canhBao).toContainText(/Chốt buổi/)

  // Ô nào thiếu thì ô đó được đánh dấu — dòng cảnh báo nói "ai", viền nói "gõ vào đâu".
  await expect(page.locator('tbody tr input[aria-invalid="true"]')).toHaveCount(3)

  // Đổi MỘT người sang Có mặt: người đó hết cần lý do, hai người kia vẫn cần.
  await page.locator('tbody select').first().selectOption('CoMat')
  await expect(page.locator('tbody tr input[aria-invalid="true"]')).toHaveCount(2)
  await expect(canhBao).not.toContainText('HV An')
  await expect(nutLuu).toBeDisabled()

  // ---------- CHIỀU 2: nhập đủ lý do → LƯU ĐƯỢC ----------
  const oThieu = page.locator('tbody tr input[aria-invalid="true"]')
  // Nhắm theo `aria-label` (mang tên học viên): locator theo chỉ số co lại ngay khi điền xong
  // vì `aria-invalid` mất, nên `nth(i)` biến mất giữa vòng lặp.
  const nhan = await oThieu.evaluateAll(
    (els) => els.map((e) => e.getAttribute('aria-label') ?? ''))
  for (const l of nhan) await page.getByLabel(l, { exact: true }).fill('Nghỉ có báo trước')

  await expect(canhBao).toHaveCount(0)
  await expect(nutLuu).toBeEnabled()

  await nutLuu.click()
  await page.getByRole('button', { name: /^Đồng ý$/ }).click()
  await page.waitForTimeout(2500)

  // Chốt bằng DỮ LIỆU đã lưu, không bằng thông báo trên màn: thông báo tự ẩn sau 2,5s nên
  // kiểm nó là kiểm một cuộc đua.
  const sau = await get(`/buoi-hoc/${buoi}/diem-danh`)
  const theoTen: Record<string, { trangThaiChinhThuc: string; lyDoVang: string | null }> =
    Object.fromEntries((sau as { hoTen: string }[]).map((d) => [d.hoTen, d as never]))

  expect(theoTen['HV An'].trangThaiChinhThuc).toBe('CoMat')
  expect(theoTen['HV Bình'].trangThaiChinhThuc).toBe('Vang')
  expect(theoTen['HV Bình'].lyDoVang).toBe('Nghỉ có báo trước')
  expect(theoTen['HV Cường'].lyDoVang).toBe('Nghỉ có báo trước')
})
