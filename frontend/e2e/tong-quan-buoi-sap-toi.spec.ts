import { expect, test } from '@playwright/test'
import { vaoHeThong, layTokenQuaApi } from './tro-giup'

/**
 * Tổng quan hiện **buổi sắp tới** đủ thông tin và bấm được (17/09/2026).
 *
 * Yêu cầu chủ sản phẩm: *"ở phần tổng quan của học viên và giáo viên, đối với buổi sắp tới hãy
 * hiện đủ tên lớp, số buổi, thời gian và khi ấn thì chuyển thẳng tới xem chi tiết buổi học đó"*.
 *
 * Trước đó Tổng quan chỉ có con số *"buổi học hôm nay"*: không nói được lớp nào, mấy giờ, và
 * không bấm được — người dùng biết có việc mà vẫn phải đi tìm.
 *
 * ## Hai thứ test này canh
 *
 * 1. **Đủ ba mẩu thông tin** trên một dòng: tên lớp · số buổi · thời gian. Thiếu một mẩu thì màn
 *    vẫn "có vẻ đúng" — chỉ người dùng mới phát hiện là không đủ để quyết định.
 * 2. **Bấm vào tới THẲNG chi tiết buổi**, không phải màn lớp. Link sai chỗ vẫn điều hướng được
 *    nên không có lỗi nào hiện ra.
 */
test('Buổi sắp tới: đủ tên lớp, số buổi, thời gian và bấm tới chi tiết', async ({ page, request }) => {
  const ttE2E = await vaoHeThong(page, request, 'tq-sap-toi')
  const token = await layTokenQuaApi(page)

  const api = async (duong: string, than: unknown) => {
    const res = await page.request.post(`http://localhost:5229/api/v1${duong}`, {
      headers: { Authorization: `Bearer ${token}` },
      data: than,
    })
    const txt = await res.text()
    expect(res.ok(), `${duong} → ${res.status()} ${txt}`).toBeTruthy()
    return txt ? JSON.parse(txt) : null
  }

  const loiJs: string[] = []
  page.on('pageerror', (e) => loiJs.push(e.message))

  const gv = await api('/nhan-su', { hoTen: 'GV Tổng quan', loaiNguoiDung: 'GiaoVien' })
  const lop = await api('/lop-hoc', {
    ten: 'Lớp Tổng quan', giaoVienChinhId: gv, hinhThuc: 'Offline',
    hocPhi: 1000, troGiangIds: [],
  })

  /*
    Lịch bắt đầu từ HÔM NAY để chắc chắn có buổi "sắp tới".

    Mốc tính theo ngày chạy, không cắm cứng: cắm cứng thì test đúng hôm nay và sai vào năm sau,
    lúc đó không ai hiểu vì sao đỏ.
  */
  const nay = new Date()
  const iso = (d: Date) =>
    `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`

  await api(`/lop-hoc/${lop}/sinh-lich`, {
    ngayKhaiGiang: iso(nay),
    thuTrongTuan: ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'],
    gioBatDau: '23:30:00',   // muộn trong ngày để buổi hôm nay chưa kết thúc
    gioKetThuc: '23:59:00',
    soBuoi: 5,
  })
  // Lớp `Nhap` chỉ người tạo mới thấy (`IPhamViLopHoc`) — quên bước này là danh sách rỗng.
  await api(`/lop-hoc/${lop}/hoan-tat`, {})

  await page.goto('/')

  const khoi = page.locator('li').filter({ hasText: 'Lớp Tổng quan' })
  await expect(khoi.first()).toBeVisible({ timeout: 15000 })

  // --- ĐỦ BA MẨU THÔNG TIN trên cùng một dòng ---
  const dongDau = khoi.first()
  await expect(dongDau, 'thiếu tên lớp').toContainText('Lớp Tổng quan')
  await expect(dongDau, 'thiếu số buổi').toContainText(/Buổi\s*1/)
  // Thời gian dạng "23:30 Thứ X, dd/mm" — chỉ cần chắc có giờ và ngày.
  await expect(dongDau, 'thiếu thời gian').toContainText(/\d{2}:\d{2}.*\d{2}\/\d{2}/)

  // Tối đa 3 buổi: Tổng quan là chỗ liếc nhanh, danh sách dài thuộc về màn lịch.
  await expect(khoi).toHaveCount(3)

  // --- BẤM VÀO TỚI THẲNG CHI TIẾT BUỔI, không phải màn lớp ---
  await dongDau.click()
  await page.waitForURL(/\/lms\/buoi-hoc\/[0-9a-f-]{36}/, { timeout: 15000 })
  expect(page.url(), 'phải tới chi tiết BUỔI, không phải màn lớp').not.toContain('/lop-hoc/')

  // Và đúng buổi đó — trang chi tiết hiện tên lớp của nó.
  await expect(page.getByText('Lớp Tổng quan').first()).toBeVisible({ timeout: 15000 })

  expect(loiJs, 'có lỗi JS chưa xử lý').toEqual([])
})
