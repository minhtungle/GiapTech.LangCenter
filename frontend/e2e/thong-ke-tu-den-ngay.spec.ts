import { expect, test } from '@playwright/test'
import { vaoHeThong, layTokenQuaApi } from './tro-giup'

/**
 * Thống kê CRM lọc **từ ngày → đến ngày** (16/09/2026) — yêu cầu chủ sản phẩm:
 * *"phần khoảng thời gian lọc hãy đổi thành từ ngày tới ngày giống khách hàng và doanh thu"*.
 *
 * Thay cho ô chọn sẵn "12 tháng / 6 tháng / 3 tháng / tháng này".
 *
 * ## Hai thứ test này canh, đều là lỗi im lặng
 *
 * 1. **Lệch một ngày ở `denNgay`.** Handler thống kê so `NgayDangKy < den`, khác màn Doanh thu
 *    (`<= denNgay`, nên bên đó gắn `T23:59:59Z`). Gửi thẳng ngày người dùng chọn thì **mất trọn
 *    ngày cuối kỳ**: chọn "đến 30/09" mà đơn ngày 30/09 không được tính. Không có gì báo lỗi —
 *    chỉ là con số nhỏ hơn thực tế.
 *
 * 2. **Bộ lọc phải SỐNG khi khoảng sai.** Màn này từng `return` sớm khi không có dữ liệu (hợp lý
 *    hồi bộ lọc là ô chọn sẵn, luôn hợp lệ). Với ô nhập tay, một khoảng đảo đầu làm cả trang —
 *    **kể cả bộ lọc** — thành "Không tìm thấy dữ liệu", người dùng không còn ô nào để sửa và phải
 *    F5. Gặp thật lúc kiểm chứng tính năng này.
 */
test('Lọc từ ngày → đến ngày, không lệch ngày cuối kỳ', async ({ page, request }) => {
  const ttE2E = await vaoHeThong(page, request, 'tk-tu-den')
  const token = await layTokenQuaApi(page)

  const api = async (duong: string, than: unknown) => {
    const res = await page.request.post(`http://localhost:5229/api/v1${duong}`, {
      headers: { Authorization: `Bearer ${token}` },
      data: than,
    })
    expect(res.ok(), `${duong} → ${res.status()} ${await res.text()}`).toBeTruthy()
    return res.json()
  }

  const loiJs: string[] = []
  page.on('pageerror', (e) => loiJs.push(e.message))

  // Một khách + một đơn ĐÚNG NGÀY sẽ chọn làm `denNgay`. Đây là mấu chốt của ca lệch ngày.
  const kh = await api('/khach-hang', { hoTen: 'Khách mốc cuối', soDienThoai: '0900000111' })
  const khoa = await api('/khoa-hoc', {
    ten: 'Khoá mốc', giaTien: 5_000_000, donViTien: 'VND', soBuoi: 10, dangBan: true,
  })
  // Giờ 09:00 theo +07: đủ xa hai đầu ngày để không phải ca biên múi giờ.
  await api('/doanh-thu', {
    khachHangId: kh, khoaHocId: khoa, soTien: 5_000_000, donViTien: 'VND', tyGiaVeVnd: 1,
    ngayDangKy: '2026-06-15T09:00:00+07:00', phuongThuc: 'ChuyenKhoan',
  })

  await page.goto('/crm/thong-ke')
  await expect(page.locator('#tk-tu-ngay')).toBeVisible({ timeout: 15000 })

  // --- Nhãn và kiểu ô GIỐNG màn Khách hàng / Doanh thu ---
  await expect(page.getByText('Từ ngày', { exact: true })).toBeVisible()
  await expect(page.getByText('Đến ngày', { exact: true })).toBeVisible()
  await expect(page.locator('#tk-tu-ngay')).toHaveAttribute('type', 'date')
  await expect(page.locator('#tk-den-ngay')).toHaveAttribute('type', 'date')
  // Ô chọn sẵn cũ phải biến mất, không để hai cách lọc cạnh nhau.
  await expect(page.getByText('12 tháng gần nhất')).toHaveCount(0)

  const tongDoanhThu = () =>
    page.locator('div').filter({ hasText: /^Tổng doanh thu/ }).first()

  /*
    CA CHÍNH: đặt `denNgay` = ĐÚNG ngày có đơn. Đơn đó PHẢI được tính.

    Nếu frontend gửi thẳng `denNgay=2026-06-15` thì handler so `< 2026-06-15` và đơn ngày 15/06
    bị loại ⇒ tổng doanh thu 0đ.
  */
  await page.fill('#tk-tu-ngay', '2026-06-15')
  await page.fill('#tk-den-ngay', '2026-06-15')
  await page.waitForTimeout(2000)

  await expect(tongDoanhThu(), 'đơn đúng ngày denNgay bị loại — lệch một ngày').toContainText('5 tr')

  /*
    Chiều ngược: kỳ kết thúc TRƯỚC ngày có đơn thì KHÔNG được tính.

    Kỳ rỗng thì cả khối số liệu không render, nên kiểm bằng "không còn chữ 5 tr trên trang" chứ
    không `not.toContainText` trên một locator đã biến mất — locator rỗng làm assert đó lỗi
    "element not found" thay vì xanh.
  */
  await page.fill('#tk-den-ngay', '2026-06-14')
  await page.waitForTimeout(2000)
  await expect(page.getByText('5 tr', { exact: false })).toHaveCount(0)

  /*
    Kỳ RỖNG (hợp lệ nhưng không có đơn nào) — bộ lọc phải còn.

    Đây là ca tái hiện được chắc chắn của lỗi "return sớm": `tk` về rỗng thì trang cũ thay cả
    bộ lọc bằng "Không tìm thấy dữ liệu". Ca khoảng-đảo-đầu ở dưới không bắt được nó vì
    TanStack Query giữ lại dữ liệu của lần gọi trước khi query bị tắt.
  */
  await page.fill('#tk-tu-ngay', '2020-01-01')
  await page.fill('#tk-den-ngay', '2020-01-02')
  await page.waitForTimeout(2000)
  await expect(page.locator('#tk-tu-ngay'), 'kỳ rỗng làm bộ lọc biến mất').toBeVisible()
  await expect(page.locator('#tk-den-ngay')).toBeVisible()

  // --- Khoảng đảo đầu: báo rõ, và BỘ LỌC VẪN CÒN để sửa ---
  await page.fill('#tk-tu-ngay', '2026-12-31')
  await page.fill('#tk-den-ngay', '2026-06-01')
  await page.waitForTimeout(1500)

  await expect(page.getByText(/phải trước hoặc bằng/)).toBeVisible()
  await expect(page.locator('#tk-tu-ngay'), 'bộ lọc biến mất — không còn gì để sửa')
    .toBeVisible()
  await expect(page.locator('#tk-den-ngay')).toBeVisible()

  // --- Nút "Kỳ mặc định" đưa về 12 tháng gần nhất ---
  await page.getByRole('button', { name: 'Kỳ mặc định' }).click()
  await page.waitForTimeout(1500)
  await expect(page.getByText(/phải trước hoặc bằng/)).toHaveCount(0)
  await expect(tongDoanhThu()).toBeVisible()

  expect(loiJs, 'có lỗi JS chưa xử lý').toEqual([])
})
