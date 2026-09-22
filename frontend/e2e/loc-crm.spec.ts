import { expect, test } from '@playwright/test'
import { vaoHeThong } from './tro-giup'

/**
 * Bộ lọc CRM theo đội nhóm · nhân viên · nguồn · hình thức · sản phẩm · khoảng ngày
 * (thêm 15/09/2026).
 *
 * `LocCrmTests` đã canh phần backend (tổng hợp khớp danh sách, mốc quy doanh số). E2E ở đây
 * canh thứ backend không thấy: **ô số tổng trên màn có đổi theo bộ lọc hay không**.
 *
 * Đó là chỗ hỏng im lặng nguy hiểm nhất của màn Doanh thu: ba ô KPI to đùng ở đầu trang gọi
 * endpoint RIÊNG với danh sách. Quên truyền một bộ lọc xuống là người dùng thấy "85 đơn /
 * 671 triệu" trong khi bảng dưới có 4 dòng — và họ tin ba ô số to, không tin bảng.
 */

/** Đọc ba ô KPI của màn Doanh thu. */
async function oSo(page: import('@playwright/test').Page) {
  return page.evaluate(() => {
    const c = [...document.querySelectorAll('p.text-2xl')].map((e) => e.textContent?.trim() ?? '')
    return { tongTien: c[0], soDon: c[1], soKhach: c[2] }
  })
}

test('Doanh thu: ô số tổng đổi theo bộ lọc, không chỉ bảng bên dưới', async ({ page, request }) => {
  await vaoHeThong(page, request, 'loc-crm-dt')

  /*
    DỰNG DỮ LIỆU PHÂN BIỆT ĐƯỢC — hai đơn khác hình thức thanh toán.

    Bản đầu của test này chạy trên tenant rỗng và **xanh cả khi bỏ hẳn bộ lọc**: không có đơn
    nào thì lọc hay không cũng ra 0, mọi phép so đều đúng một cách vô nghĩa. Đột biến "quên
    truyền phuongThuc" lọt qua.

    Có 1 đơn tiền mặt + 1 đơn chuyển khoản thì lọc sai là số sai ngay.
  */
  /*
    Dựng dữ liệu qua API, không qua form.

    Form đăng ký dùng `SelectTimKiem` mở khung `position: fixed` NGOÀI thẻ `<dialog>`, và mục
    chọn hiển thị tên kèm số điện thoại — nên `getByText(..., exact)` không khớp. Đi qua form ở
    đây cũng không kiểm thêm điều gì: form tạo đơn đã có test riêng, còn test NÀY canh bộ lọc.
  */
  const token = await page.evaluate(() => localStorage.getItem('lms_access_token'))
  const api = async (duong: string, than: unknown) => {
    const res = await page.request.post(`http://localhost:5229/api/v1${duong}`, {
      headers: { Authorization: `Bearer ${token}` },
      data: than,
    })
    expect(res.ok(), `${duong} → ${res.status()} ${await res.text()}`).toBeTruthy()
    return res.json()
  }

  const khoaId = await api('/khoa-hoc', {
    ten: 'Khoá lọc', giaTien: 1_000_000, donViTien: 'VND', soBuoi: 10, dangBan: true,
  })
  const khachId = await api('/khach-hang', { hoTen: 'Khách lọc', soDienThoai: '0913000222' })

  for (const [phuongThuc, soTien] of [['TienMat', 500_000], ['ChuyenKhoan', 700_000]] as const) {
    await api('/doanh-thu', {
      khachHangId: khachId, khoaHocId: khoaId, soTien, donViTien: 'VND', tyGiaVeVnd: 1,
      ngayDangKy: new Date().toISOString(), phuongThuc,
    })
  }

  await page.goto('/crm/doanh-thu')

  // --- Không lọc: 2 đơn / 1.2 triệu ---
  await expect(page.getByRole('button', { name: /Lọc thêm/ })).toBeVisible({ timeout: 10000 })
  const truoc = await oSo(page)
  expect(truoc.soDon).toBe('2')

  await page.getByRole('button', { name: /Lọc thêm/ }).click()

  // Chú thích mốc quy đơn phải hiện: người đọc cần biết "đội nhóm" nghĩa là đội của người
  // MANG KHÁCH VỀ, không phải người nhập đơn — nếu không họ đối chiếu sai với sổ tay.
  await expect(page.getByText(/NGƯỜI MANG KHÁCH VỀ/)).toBeVisible()

  // --- Lọc tiền mặt: ô KPI phải xuống 1 đơn, KHÔNG giữ số 2 ---
  await page.locator('#loc-pt').click()
  await page.getByText('Tiền mặt', { exact: true }).first().click()

  await expect
    .poll(async () => (await oSo(page)).soDon, { timeout: 10000 })
    .toBe('1')

  const loc = await oSo(page)
  expect(loc.soDon).not.toBe(truoc.soDon)   // chốt: số ĐÃ đổi, không phải giữ nguyên
  expect(loc.tongTien).not.toBe(truoc.tongTien)

  // Badge số bộ lọc đang bật — để đóng panel lại mà vẫn còn lọc thì người dùng biết.
  await expect(page.getByRole('button', { name: /Lọc thêm/ })).toContainText('1')

  // --- Xóa lọc: trả về đúng trạng thái ban đầu ---
  await page.getByRole('button', { name: /Xóa lọc/ }).click()
  await expect(page.getByRole('button', { name: /Xóa lọc/ })).toHaveCount(0)
  await expect.poll(async () => (await oSo(page)).soDon, { timeout: 10000 }).toBe('2')
})

test('Khách hàng: lọc đội nhóm thu hẹp danh sách và hiện số bộ lọc', async ({ page, request }) => {
  await vaoHeThong(page, request, 'loc-crm-kh')

  /*
    Hai khách, HAI ĐỘI khác nhau — dữ liệu phải phân biệt được, nếu không test vô nghĩa.

    Bản đầu chỉ tạo một khách rồi lọc nguồn `TuDangKy` (mong rỗng). Nó xanh cả khi **bỏ hẳn bộ
    lọc**: tenant mới chỉ có một khách nguồn `NhanVienTao`, nên `TuDangKy` trả rỗng dù có lọc
    hay không. Và `LuuKhachHangCommand` không nhận `Nguon` (nguồn do hệ thống đặt), nên E2E
    không tạo nổi khách `TuDangKy` để so.

    Đội nhóm thì tạo được: hai nhân viên ở hai phòng, mỗi người tạo khách của mình.
  */
  const token = await page.evaluate(() => localStorage.getItem('lms_access_token'))
  const api = async (duong: string, than: unknown, tok = token) => {
    const res = await page.request.post(`http://localhost:5229/api/v1${duong}`, {
      headers: { Authorization: `Bearer ${tok}` },
      data: than,
    })
    expect(res.ok(), `${duong} → ${res.status()} ${await res.text()}`).toBeTruthy()
    return res.json()
  }

  // `tagVaiTro: 'KinhDoanh'` là BẮT BUỘC từ 16/09/2026: bộ lọc đội nhóm ở CRM chỉ liệt kê
  // phòng mang tag Kinh doanh. Phòng không tag chỉ tồn tại trong cây cơ cấu — tạo phòng rồi
  // mong nó hiện trong ô lọc là kỳ vọng của hành vi CŨ.
  const doiA = await api('/phong-ban', { ten: 'Đội Alpha', tagVaiTro: 'KinhDoanh' })
  const doiB = await api('/phong-ban', { ten: 'Đội Beta', tagVaiTro: 'KinhDoanh' })

  const quyens = await (await page.request.get('http://localhost:5229/api/v1/quyen', {
    headers: { Authorization: `Bearer ${token}` },
  })).json()
  const quyenId = quyens.find((q: { tenQuyen: string }) => q.tenQuyen === 'Quản trị viên').id

  /** Tạo nhân viên kèm tài khoản, trả token của chính họ để họ tự tạo khách. */
  const tokenCuaNhanVien = async (ten: string, username: string, phongBanId: string) => {
    await api('/nguoi-dung', {
      hoTen: ten,
      loaiNguoiDung: 'NhanVien',
      phongBanId,
      taiKhoan: { username, matKhau: 'matkhau123456', quyenIds: [quyenId], phaiDoiMatKhau: false },
    })
    const maTrungTam = await page.evaluate(() => {
      const t = localStorage.getItem('lms_access_token')!
      return JSON.parse(atob(t.split('.')[1])).ma_trung_tam
        ?? JSON.parse(atob(t.split('.')[1])).maTrungTam
    })
    const res = await page.request.post('http://localhost:5229/api/v1/auth/dang-nhap', {
      data: { maTrungTam, username, matKhau: 'matkhau123456' },
    })
    expect(res.ok(), `đăng nhập ${username} → ${await res.text()}`).toBeTruthy()
    return (await res.json()).accessToken as string
  }

  const tokA = await tokenCuaNhanVien('NV Alpha', 'nvalpha', doiA)
  const tokB = await tokenCuaNhanVien('NV Beta', 'nvbeta', doiB)

  await api('/khach-hang', { hoTen: 'Khách của Alpha', soDienThoai: '0914000111' }, tokA)
  await api('/khach-hang', { hoTen: 'Khách của Beta', soDienThoai: '0914000222' }, tokB)

  await page.goto('/crm/khach-hang')
  await expect(page.getByText('Khách của Alpha')).toBeVisible({ timeout: 10000 })
  await expect(page.getByText('Khách của Beta')).toBeVisible()

  await page.getByRole('button', { name: /Lọc thêm/ }).click()

  // --- Lọc đội Alpha: PHẢI giữ khách của Alpha và LOẠI khách của Beta ---
  await page.locator('#loc-doi').click()
  await page.getByText('Đội Alpha', { exact: true }).first().click()

  await expect(page.getByText('Khách của Alpha')).toBeVisible()
  await expect(page.getByText('Khách của Beta')).toHaveCount(0)   // chiều LOẠI — chốt của test
  await expect(page.getByRole('button', { name: /Lọc thêm/ })).toContainText('1')

  // --- Xóa lọc: thấy lại cả hai ---
  await page.getByRole('button', { name: /Xóa lọc/ }).click()
  await expect(page.getByText('Khách của Beta')).toBeVisible()
})

/**
 * Tag vai trò phòng ban (FR-22, 16/09/2026) — **phòng không tag không hiện ở bộ lọc**.
 *
 * `TagVaiTroPhongBanTests` đã canh backend. E2E ở đây canh thứ backend không thấy: ô chọn trên
 * màn có thật sự bỏ phòng không tag, và khi CHƯA phòng nào có tag thì màn nói rõ phải làm gì
 * thay vì để ô rỗng im lặng (người dùng sẽ tưởng hệ thống hỏng).
 */
test('Bộ lọc đội nhóm chỉ hiện phòng tag Kinh doanh', async ({ page, request }) => {
  await vaoHeThong(page, request, 'loc-tag')

  const token = await page.evaluate(() => localStorage.getItem('lms_access_token'))
  const api = async (duong: string, than: unknown) => {
    const res = await page.request.post(`http://localhost:5229/api/v1${duong}`, {
      headers: { Authorization: `Bearer ${token}` },
      data: than,
    })
    expect(res.ok(), `${duong} → ${res.status()} ${await res.text()}`).toBeTruthy()
    return res.json()
  }

  // --- Chưa phòng nào có tag: ô lọc rỗng, nhưng màn phải CHỈ DẪN ---
  await api('/phong-ban', { ten: 'Phòng chỉ mô tả' })

  await page.goto('/crm/khach-hang')
  await page.getByRole('button', { name: /Lọc thêm/ }).click()

  await expect(page.getByText(/Chưa phòng ban nào được đánh tag/)).toBeVisible()
  await expect(page.getByRole('link', { name: /Đánh tag ở Cơ cấu tổ chức/ })).toBeVisible()

  // --- Đánh tag một phòng: nó hiện, phòng không tag thì KHÔNG ---
  await api('/phong-ban', { ten: 'Đội bán hàng', tagVaiTro: 'KinhDoanh' })
  await api('/phong-ban', { ten: 'Đội giáo viên', tagVaiTro: 'GiaoVien' })

  await page.reload()
  await page.getByRole('button', { name: /Lọc thêm/ }).click()
  await expect(page.getByText(/Chưa phòng ban nào được đánh tag/)).toHaveCount(0)

  await page.locator('#loc-doi').click()
  const muc = await page.evaluate(() =>
    [...document.querySelectorAll('[role=option]')].map((e) => e.textContent?.trim()))

  expect(muc).toContain('Đội bán hàng')
  // Hai chiều LOẠI — đây là chốt của test: chỉ kiểm chiều "có" thì bỏ hẳn bộ lọc vẫn xanh.
  expect(muc).not.toContain('Phòng chỉ mô tả')   // không tag
  expect(muc).not.toContain('Đội giáo viên')     // tag khác, không phải Kinh doanh
})
