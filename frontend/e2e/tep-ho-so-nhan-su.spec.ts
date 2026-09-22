import { expect, test } from '@playwright/test'
import { vaoHeThong, layTokenQuaApi } from './tro-giup'

/**
 * FR-23 — đặt tên tệp hồ sơ nhân sự + hạn mức 10 tệp (yêu cầu chủ sản phẩm 16/09/2026).
 *
 * ## Vì sao cần E2E, ngoài test backend
 *
 * Backend đã canh luật (`HoSoNhanSuTests`, 15 test). Việc E2E canh là **chuỗi UI**: chọn tệp →
 * hộp thoại đặt tên → gửi kèm `tenHienThi` → tên hiện đúng trên danh sách. Đứt ở bất kỳ mắt nào
 * thì tệp vẫn tải lên được, chỉ là mang tên máy quét — không có lỗi nào hiện ra.
 *
 * Và ca **đầy 10 tệp** chỉ nhìn được ở đây: nút phải khoá TRƯỚC, không để người dùng chọn xong
 * tệp rồi mới nhận lỗi.
 */
test('Đặt tên khi tải lên, đổi tên sau, và khoá nút khi đủ 10 tệp', async ({ page, request }) => {
  const ttE2E = await vaoHeThong(page, request, 'tep-ho-so')
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

  const id = await api('/nhan-su', { hoTen: 'NV Tệp', loaiNguoiDung: 'NhanVien' })

  await page.goto(`/hrm/nhan-su/${id}?tab=tep`)
  await expect(page.getByText(/Chưa có tệp/)).toBeVisible({ timeout: 15000 })
  await expect(page.getByText(/đã dùng 0\/10 tệp/)).toBeVisible()

  // --- Chọn tệp thì hiện hộp thoại ĐẶT TÊN, chưa tải lên ngay ---
  await page.setInputFiles('input[type=file]', {
    name: 'SCAN_0012.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-1.4 test'),
  })
  await expect(page.getByText('Đặt tên tệp')).toBeVisible({ timeout: 10000 })

  // Gợi ý sẵn tên tệp ĐÃ BỎ ĐUÔI — người dùng không phải gõ lại từ đầu, và ô nhập không
  // nói sai rằng cần gõ ".pdf".
  await expect(page.locator('#ten-tep')).toHaveValue('SCAN_0012')

  await page.fill('#ten-tep', 'Hợp đồng lao động 2026')
  await page.getByRole('button', { name: 'Lưu' }).click()

  // Tên người dùng đặt + ĐUÔI THẬT của tệp.
  await expect(page.getByText('Hợp đồng lao động 2026.pdf')).toBeVisible({ timeout: 10000 })
  await expect(page.getByText(/đã dùng 1\/10 tệp/)).toBeVisible()

  // --- Đổi tên tệp ĐÃ CÓ ---
  await page.getByTitle('Đổi tên tệp').click()
  await expect(page.locator('#ten-tep')).toHaveValue('Hợp đồng lao động 2026')
  await page.fill('#ten-tep', 'Phụ lục HĐ')
  await page.getByRole('button', { name: 'Lưu' }).click()

  await expect(page.getByText('Phụ lục HĐ.pdf')).toBeVisible({ timeout: 10000 })
  await expect(page.getByText('Hợp đồng lao động 2026.pdf')).toHaveCount(0)

  // --- Đổ cho đủ 10 tệp rồi kiểm nút bị khoá ---
  // Lấy LẠI token: `page.goto` ở trên đã làm app xoay vòng token, bản cũ nay 401 (xem
  // `layTokenQuaApi`).
  const token2 = await layTokenQuaApi(page)

  for (let i = 2; i <= 10; i++) {
    const r = await page.request.post(`http://localhost:5229/api/v1/nhan-su/${id}/tep`, {
      headers: { Authorization: `Bearer ${token2}` },
      multipart: {
        tep: {
          name: `t${i}.pdf`, mimeType: 'application/pdf', buffer: Buffer.from(`pdf ${i}`),
        },
      },
    })
    expect(r.ok(), `tệp thứ ${i} phải vào được`).toBeTruthy()
  }

  await page.reload()
  await expect(page.getByText(/đã dùng 10\/10 tệp/)).toBeVisible({ timeout: 15000 })

  // Ô chọn tệp bị GỠ HẲN, không chỉ làm mờ: `<label>` bọc input không có `disabled`, để nguyên
  // thì vẫn bấm chọn được tệp rồi mới nhận lỗi từ server.
  await expect(page.locator('input[type=file]')).toHaveCount(0)
  await expect(page.getByText('Tải tệp lên')).toHaveClass(/cursor-not-allowed/)

  expect(loiJs, 'có lỗi JS chưa xử lý').toEqual([])
})
