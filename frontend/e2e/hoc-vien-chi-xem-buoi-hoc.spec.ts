import { expect, test } from '@playwright/test'
import { MAT_KHAU_MOI, taoTrungTam, dangNhap } from './tro-giup'

/**
 * **Học viên chỉ XEM** buổi học — không sinh lịch, không điểm danh (17/09/2026).
 *
 * Chủ sản phẩm báo: *"tôi thấy học viên vẫn có thể sinh lịch học và điểm danh trong buổi học.
 * học viên chỉ có quyền xem thôi"*.
 *
 * ## Không phải lỗ hổng bảo mật, nhưng vẫn là lỗi thật
 *
 * Backend chặn đủ (403 — đã thử từng endpoint bằng tài khoản học viên), nên không ai ghi được gì.
 * Vấn đề là **frontend không gác nút**: `LichVaDiemDanh.tsx` và `BangDiemDanh.tsx` không gọi
 * `coQuyen` một lần nào, nên học viên vào lớp mình học vẫn thấy "Sinh lịch", "Sinh lại lịch",
 * "Chốt buổi", "Lưu điểm danh" và **ô chọn trạng thái của cả lớp**. Bấm vào chỉ nhận lỗi đỏ —
 * người dùng không hiểu vì sao, và tưởng mình vừa làm hỏng dữ liệu của lớp.
 *
 * Tab "Điểm danh" của view chi tiết buổi gác bằng `DiemDanh` với thao tác mặc định `Xem` — quyền
 * mà học viên CÓ — nên đó là lối vào chính của lỗi này.
 *
 * ## Vì sao test cả hai vai trò trong một file
 *
 * Sửa kiểu "ẩn hết cho chắc" cũng làm test học viên xanh, mà giáo viên thì mất nút. Đã suýt mắc:
 * bản sửa đầu gom "Thêm buổi" chung cờ với "Sinh lịch", trong khi hai nút gọi hai endpoint gác
 * hai quyền khác nhau — giáo viên có `BuoiHoc.Them` nhưng KHÔNG có `LopHoc.SinhLich`.
 */
test('Học viên chỉ xem; giáo viên vẫn thao tác được', async ({ page, request }) => {
  const tt = await taoTrungTam(request, 'hv-chi-xem')
  await dangNhap(page, tt)

  const tok = async () => await page.evaluate(() => localStorage.getItem('lms_access_token'))
  const api = async (duong: string, than: unknown) => {
    const res = await page.request.post(`http://localhost:5229/api/v1${duong}`, {
      headers: { Authorization: `Bearer ${await tok()}` },
      data: than,
    })
    const txt = await res.text()
    expect(res.ok(), `${duong} → ${res.status()} ${txt}`).toBeTruthy()
    return txt ? JSON.parse(txt) : null
  }

  const quyens = await (await page.request.get('http://localhost:5229/api/v1/quyen', {
    headers: { Authorization: `Bearer ${await tok()}` },
  })).json()
  const idQuyen = (ten: string) =>
    (quyens as { id: string; tenQuyen: string }[]).find((q) => q.tenQuyen === ten)!.id

  // Giáo viên + học viên, mỗi người một tài khoản với NHÓM QUYỀN MẶC ĐỊNH của vai trò đó.
  const gv = await api('/nguoi-dung', {
    hoTen: 'GV quyền', loaiNguoiDung: 'GiaoVien',
    taiKhoan: {
      username: 'gv-q', matKhau: MAT_KHAU_MOI,
      quyenIds: [idQuyen('Giáo viên')], phaiDoiMatKhau: false,
    },
  })
  const hv = await api('/nguoi-dung', {
    hoTen: 'HV quyền', loaiNguoiDung: 'HocVien',
    taiKhoan: {
      username: 'hv-q', matKhau: MAT_KHAU_MOI,
      quyenIds: [idQuyen('Học viên')], phaiDoiMatKhau: false,
    },
  })

  const lop = await api('/lop-hoc', {
    ten: 'Lớp quyền', giaoVienChinhId: gv, hinhThuc: 'Offline',
    hocPhi: 1000, troGiangIds: [],
  })
  await api(`/lop-hoc/${lop}/hoc-vien`, { hocVienIds: [hv] })
  const buois = await api(`/lop-hoc/${lop}/sinh-lich`, {
    ngayKhaiGiang: '2026-10-06',
    thuTrongTuan: ['Tuesday'],
    gioBatDau: '18:00:00', gioKetThuc: '20:00:00', soBuoi: 2,
  })
  // Lớp `Nhap` chỉ người tạo mới thấy — quên là học viên vào nhận 404.
  await api(`/lop-hoc/${lop}/hoan-tat`, {})
  const buoi = (buois as { id: string }[])[0].id

  const vao = async (username: string) => {
    await page.goto('/dang-nhap')
    await page.fill('#maTrungTam', tt.maTrungTam)
    await page.fill('#username', username)
    await page.fill('#matKhau', MAT_KHAU_MOI)
    await page.click('button[type=submit]')
    /*
      Chờ ĐIỀU HƯỚNG THẬT, không `waitForTimeout` cố định: dưới tải của cả bộ E2E, 2,5s có lúc
      không đủ cho đăng nhập xong — test đỏ ở chỗ không liên quan tới quyền (đã gặp khi chạy
      cả bộ, mà chạy riêng thì xanh).
    */
    await page.waitForURL((u) => !u.pathname.includes('dang-nhap'), { timeout: 30_000 })
    // Quyền tải xong mới render đúng nút; `useQuyen` có trạng thái `dangTai`.
    await page.waitForTimeout(800)
  }

  const nutHienThi = async () =>
    (await page.locator('button:visible').allInnerTexts()).filter(Boolean).join(' | ')

  // ---------- HỌC VIÊN: chỉ xem ----------
  await vao('hv-q')

  await page.goto(`/lms/lop-hoc/${lop}?tab=lich`)
  await expect(page.locator('tbody tr').first()).toBeVisible({ timeout: 15000 })

  const nutHv = await nutHienThi()
  for (const cam of ['Sinh lịch', 'Sinh lại lịch', 'Thêm buổi'])
    expect(nutHv, `học viên KHÔNG được thấy nút "${cam}"`).not.toContain(cam)

  // Menu thao tác chỉ còn "Xem chi tiết".
  await page.locator('tbody tr button').first().click()
  await page.waitForTimeout(600)
  const menuHv = (await page.locator('div.absolute button').allInnerTexts()).filter(Boolean)
  expect(menuHv.join(' | '), 'học viên không được thấy Điểm danh / Huỷ / Xoá')
    .not.toMatch(/Điểm danh|Huỷ buổi|Xóa/)
  await page.keyboard.press('Escape')

  // Tab điểm danh: bảng CHỈ ĐỌC.
  await page.goto(`/lms/buoi-hoc/${buoi}?tab=diem-danh`)
  await expect(page.getByText(/chỉ có quyền xem/i)).toBeVisible({ timeout: 15000 })

  // Ghi chú "chỉ có quyền xem" vẽ ra từ QUYỀN, còn các dòng điểm danh đến từ MỘT QUERY KHÁC —
  // thấy ghi chú KHÔNG có nghĩa là bảng đã có dòng. Phải chờ riêng, nếu không thì đếm `select`
  // lúc bảng còn rỗng và `toBeGreaterThan(0)` đỏ oan.
  //
  // Đã đỏ thật 17/09/2026 khi chạy cả bộ: bản sửa `qc.clear()` lúc đổi phiên làm mọi query đều
  // phải tải lại từ đầu, nên khoảng chờ này rộng hơn trước và lỗi lộ ra.
  await expect(page.locator('tbody tr').first()).toBeVisible({ timeout: 15000 })

  const nutDd = await nutHienThi()
  expect(nutDd, 'học viên KHÔNG được thấy nút Chốt buổi').not.toContain('Chốt buổi')
  expect(nutDd, 'học viên KHÔNG được thấy nút Lưu điểm danh').not.toContain('Lưu điểm danh')

  // Mọi ô chọn trạng thái của BẢNG phải bị khoá (ô chọn buổi ở đầu trang là điều hướng, không tính).
  const oMo = await page.locator('tbody select:not([disabled])').count()
  expect(oMo, 'học viên vẫn sửa được điểm danh của lớp').toBe(0)
  expect(await page.locator('tbody select[disabled]').count(),
    'bảng điểm danh phải có ô, chỉ là bị khoá').toBeGreaterThan(0)

  // ---------- GIÁO VIÊN: vẫn thao tác được (chiều NGƯỢC) ----------
  await vao('gv-q')

  await page.goto(`/lms/lop-hoc/${lop}?tab=lich`)
  await expect(page.locator('tbody tr').first()).toBeVisible({ timeout: 15000 })
  // Giáo viên có `BuoiHoc.Them` → thấy "Thêm buổi"; KHÔNG có `LopHoc.SinhLich` → không thấy
  // "Sinh lại lịch". Đây chính là chỗ bản sửa đầu làm sai.
  expect(await nutHienThi(), 'giáo viên phải giữ được nút Thêm buổi').toContain('Thêm buổi')

  await page.goto(`/lms/buoi-hoc/${buoi}?tab=diem-danh`)
  await expect(page.locator('tbody select').first()).toBeVisible({ timeout: 15000 })

  const nutGv = await nutHienThi()
  expect(nutGv, 'giáo viên phải chốt buổi được').toContain('Chốt buổi')
  expect(nutGv, 'giáo viên phải lưu điểm danh được').toContain('Lưu điểm danh')
  expect(await page.locator('tbody select:not([disabled])').count(),
    'giáo viên phải sửa được điểm danh').toBeGreaterThan(0)
})
