import { expect, test } from '@playwright/test'
import { vaoHeThong } from './tro-giup'

/**
 * Ghi đơn ở **màn Doanh thu** và **tab Lịch sử đơn hàng** phải đồng nhất (17/09/2026).
 *
 * Chủ sản phẩm báo: *"phần ghi mua hàng tại lịch sử mua hàng và doanh thu đang chưa đồng nhất
 * về cả tên và thao tác"*.
 *
 * Hai khác biệt tìm được, mỗi cái một kiểu:
 *
 * 1. **Tên**: cùng một việc mà gọi hai kiểu — *"Ghi mua hàng"* (chi tiết khách) vs *"Thêm đăng
 *    ký"* (Doanh thu). Nay cả hai là **"Ghi đơn"**.
 *
 * 2. **Thao tác**: màn Doanh thu chỉ gửi được `khoaHocId`, nên **không ghi được đơn sản phẩm**
 *    và **sửa đơn sản phẩm thì 400** — trên dữ liệu thật có 10/85 đơn như vậy. Phép tính ở
 *    backend đã có test riêng (`CrmTests`); test này canh phần UI: ô chọn loại mặt hàng và ô số
 *    lượng phải có mặt ở CẢ HAI màn.
 */
test('Hai màn ghi đơn dùng cùng tên và cùng bộ ô nhập', async ({ page, request }) => {
  await vaoHeThong(page, request, 'ghi-don')
  const token = await page.evaluate(() => localStorage.getItem('lms_access_token'))

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

  const khach = await api('/khach-hang', { hoTen: 'Khách ghi đơn', soDienThoai: '0900777888' })
  await api('/san-pham', {
    ten: 'Sách thử', ghiChu: '', giaTien: 120000, donViTien: 'VND', donViTinh: 'quyển',
  })

  /** Ô nhập của form ghi đơn — dùng chung cho cả hai màn. */
  const kiemFormGhiDon = async (nhan: string) => {
    /*
      Khoanh trong `<dialog>`: chữ "Khoá học" / "Sản phẩm" còn xuất hiện ở ô LỌC và ô chọn mặt
      hàng của chính form — `getByRole('button', …)` trần khớp 3 phần tử và Playwright báo
      strict mode violation, dù tính năng đúng.
    */
    const hop = page.getByRole('dialog')

    // KHÔNG `exact`: nhãn render kèm dấu sao bắt buộc — "Khách mua gì *".
    await expect(hop.getByText(/Khách mua gì/).first(),
      `${nhan}: thiếu ô chọn loại mặt hàng`).toBeVisible()
    await expect(hop.getByRole('button', { name: 'Khoá học', exact: true }),
      `${nhan}: thiếu lựa chọn Khoá học`).toBeVisible()
    await expect(hop.getByRole('button', { name: 'Sản phẩm', exact: true }),
      `${nhan}: thiếu lựa chọn Sản phẩm`).toBeVisible()

    // Số lượng chỉ hiện với SẢN PHẨM — khoá học không ai mua 2 suất trong một đơn.
    await expect(hop.locator('#soLuong'), `${nhan}: số lượng không được hiện với khoá học`)
      .toHaveCount(0)
    await hop.getByRole('button', { name: 'Sản phẩm', exact: true }).click()
    await expect(hop.locator('#soLuong'), `${nhan}: chọn sản phẩm phải hiện ô số lượng`)
      .toBeVisible()
  }

  // ---------- Màn DOANH THU ----------
  await page.goto('/crm/doanh-thu')
  await expect(page.getByRole('button', { name: 'Ghi đơn' }).first())
    .toBeVisible({ timeout: 15000 })
  // Tên cũ phải biến mất — hai màn gọi một kiểu.
  await expect(page.getByRole('button', { name: 'Thêm đăng ký' })).toHaveCount(0)

  await page.getByRole('button', { name: 'Ghi đơn' }).first().click()
  await kiemFormGhiDon('Doanh thu')
  await page.keyboard.press('Escape')

  // ---------- Tab LỊCH SỬ ĐƠN HÀNG ở chi tiết khách ----------
  await page.goto(`/crm/khach-hang/${khach}?tab=mua-hang`)
  await expect(page.getByRole('button', { name: 'Ghi đơn' }).first())
    .toBeVisible({ timeout: 15000 })
  // Tab đổi tên theo cùng từ vựng.
  await expect(page.getByText('Lịch sử đơn hàng').first()).toBeVisible()
  await expect(page.getByText('Lịch sử mua hàng')).toHaveCount(0)

  await page.getByRole('button', { name: 'Ghi đơn' }).first().click()
  await kiemFormGhiDon('Chi tiết khách')

  expect(loiJs, 'có lỗi JS chưa xử lý').toEqual([])
})
