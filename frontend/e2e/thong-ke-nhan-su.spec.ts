import { expect, test } from '@playwright/test'
import { vaoHeThong, layTokenQuaApi } from './tro-giup'

/**
 * FR-29 — thống kê nhân sự + module tiêu chí đánh giá (16/09/2026).
 *
 * Backend đã canh phép tính (`ThongKeNhanSuTests`, 18 test). Việc E2E canh là **chuỗi UI**:
 * tạo tiêu chí (tab Tiêu chí) → chấm điểm (tab Thống kê) → điểm hiện trên bảng xếp hạng.
 *
 * Đứt ở giữa thì không có lỗi nào hiện ra — đúng như bug đã gặp khi làm: `GuiNhanXetBody` của
 * controller thiếu trường `DiemTieuChis` nên điểm học viên chấm **rơi âm thầm**, command nhận
 * `null` và handler chạy đúng theo `null`. Chỉ test đầu-cuối bắt được.
 */
test('Tạo tiêu chí, chấm điểm, điểm lên bảng xếp hạng', async ({ page, request }) => {
  const ttE2E = await vaoHeThong(page, request, 'tk-nhan-su')
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

  await api('/nhan-su', { hoTen: 'Sale UI', loaiNguoiDung: 'NhanVienKinhDoanh' })
  await api('/nhan-su', { hoTen: 'GV UI', loaiNguoiDung: 'GiaoVien' })

  // --- Tab Tiêu chí: tạo một tiêu chí nhóm Kinh doanh qua UI ---
  await page.goto('/hrm/tieu-chi-danh-gia')
  await expect(page.getByRole('button', { name: 'Thêm tiêu chí' })).toBeVisible({ timeout: 15000 })
  // Module RIÊNG (tách 16/09/2026): có mục sidebar của mình, không còn là tab của `/hrm`.
  await expect(page.getByRole('link', { name: 'Tiêu chí đánh giá' })).toBeVisible()

  // Hai nhóm luôn hiện, kèm chú thích ai chấm ở đâu — đó là thứ dễ nhầm nhất của module này.
  // `exact` vì cùng câu này còn nằm trong đoạn giới thiệu đầu màn.
  await expect(page.getByText('quản lý chấm theo kỳ tháng', { exact: true })).toBeVisible()
  await expect(
    page.getByText('học viên chấm trong từng buổi học', { exact: true })).toBeVisible()

  await page.getByRole('button', { name: 'Thêm tiêu chí' }).click()
  await page.fill('#ten', 'Thái độ phục vụ')
  await page.getByRole('button', { name: 'Lưu' }).click()
  await page.getByRole('button', { name: 'Đồng ý' }).click()
  await expect(page.getByText('Thái độ phục vụ')).toBeVisible({ timeout: 10000 })

  // --- Tab Thống kê: ba bảng theo vai trò ---
  await page.goto('/hrm/thong-ke')
  await expect(page.getByRole('button', { name: 'Kinh doanh' })).toBeVisible({ timeout: 15000 })

  const dong = (ten: string) => page.locator('tbody tr').filter({ hasText: ten })

  // Sale có mặt ở bảng Kinh doanh, giáo viên KHÔNG (ba bảng loại trừ nhau theo vai trò).
  await expect(dong('Sale UI')).toHaveCount(1)
  await expect(dong('GV UI')).toHaveCount(0)

  // Chưa chấm thì cột chất lượng là "—", KHÔNG phải 0: "chưa ai chấm" khác "bị 0 điểm".
  await expect(dong('Sale UI')).toContainText('—')

  // --- Chấm điểm qua UI ---
  await dong('Sale UI').getByRole('button', { name: 'Chấm điểm' }).click()
  await expect(page.getByText('Chấm điểm nhân viên kinh doanh')).toBeVisible({ timeout: 10000 })

  // Nút Lưu bị khoá khi chưa chấm gì — gửi phiếu rỗng là tạo một kỳ không có điểm nào.
  await expect(page.getByRole('button', { name: 'Lưu' })).toBeDisabled()

  await page.getByLabel('Thái độ phục vụ 4').click()
  await page.getByRole('button', { name: 'Lưu' }).click()
  await page.getByRole('button', { name: 'Đồng ý' }).click()

  // Điểm lên bảng ngay — đây là mắt cuối của chuỗi.
  await expect(dong('Sale UI')).toContainText('4.0', { timeout: 10000 })

  // --- Tab Giáo viên: đổi bộ chỉ số, không phải cùng bảng ---
  await page.getByRole('button', { name: 'Giáo viên' }).click()
  // Theo vai trò cột (không phải getByText): cùng chữ này còn ở tiêu đề biểu đồ dưới.
  await expect(
    page.getByRole('columnheader', { name: 'Buổi dạy đủ' })).toBeVisible()
  await expect(dong('GV UI')).toHaveCount(1)
  await expect(dong('Sale UI')).toHaveCount(0)

  // --- Link `?tab=` CŨ vẫn mở được: rơi về tab đầu thì người dùng tưởng tính năng bị xoá ---
  for (const [cu, moi] of [
    ['/hrm?tab=thong-ke', '/hrm/thong-ke'],
    ['/hrm?tab=tieu-chi', '/hrm/tieu-chi-danh-gia'],
  ]) {
    await page.goto(cu)
    await expect(page, `${cu} phải chuyển hướng sang ${moi}`)
      .toHaveURL(new RegExp(`${moi.replace(/\//g, '\\/')}$`), { timeout: 15000 })
  }

  // Ba tab còn lại của `/hrm` KHÔNG mất đi — chúng nói về cùng một tập người nên vẫn gộp.
  await page.goto('/hrm')
  for (const nhan of ['Cơ cấu tổ chức', 'Hồ sơ nhân sự', 'Chức vụ'])
    await expect(page.getByRole('button', { name: nhan })).toBeVisible({ timeout: 15000 })
  await expect(page.getByRole('button', { name: 'Thống kê nhân sự' })).toHaveCount(0)

  expect(loiJs, 'có lỗi JS chưa xử lý').toEqual([])
})
