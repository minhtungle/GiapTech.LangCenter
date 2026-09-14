import { expect, test } from '@playwright/test'
import { vaoHeThong } from './tro-giup'

/**
 * FR-05 — trang cấu hình ma trận quyền (thiết kế lại 14/09/2026: modal → trang riêng).
 *
 * Vì sao cần E2E dù đã có 28 test vitest cho phép tính: `kieu.test.ts` kiểm hàm thuần, nhưng
 * thứ hay hỏng ở màn này là **đường nối** giữa hàm thuần và API — form gửi đúng ma trận
 * không, tick một ô có làm mất ô khác không. Đúng loại lỗi ngày 16/08 (form thiếu ô địa chỉ
 * nên âm thầm xoá địa chỉ), và backend không bắt được vì nó nhận đúng thứ frontend gửi.
 */

test('sửa một ô quyền không làm mất ô khác, và mẫu vai trò áp đúng', async ({
  page,
  request,
}) => {
  await vaoHeThong(page, request, 'ma-tran-quyen')

  await page.goto('/quan-tri/phan-quyen')
  await expect(page.getByRole('link', { name: 'Giáo viên' })).toBeVisible()

  // --- Vào trang chi tiết bằng cách bấm thẳng tên nhóm (thao tác chính của dòng) ---
  await page.getByRole('link', { name: 'Giáo viên' }).click()
  await expect(page).toHaveURL(/\/quan-tri\/phan-quyen\/[0-9a-f-]{36}/)

  const duongDan = page.url()

  // Ma trận dựng bằng THẺ theo chức năng, không phải bảng. Trang mở ở tab HRM nên sang LMS
  // trước — "Lớp học" thuộc hệ thống con đó.
  await expect(page.locator('section[aria-label="Nhân sự"]')).toBeVisible()
  await page.getByRole('button', { name: /LMS/ }).click()
  await expect(page.locator('section[aria-label="Lớp học"]')).toBeVisible()

  const soDaChon = async () => {
    const chu = await page.getByText(/Đã chọn \d+ ô/).innerText()
    return Number(/\d+/.exec(chu)![0])
  }

  // --- Tick đúng MỘT ô rồi lưu ---
  const truoc = await soDaChon()
  // `Hủy` của Lớp học: thao tác đặc thù, và là ô "cần cân nhắc" nên có mô tả hệ quả kèm theo.
  const oHuy = page.locator('section[aria-label="Lớp học"] label', { hasText: 'Hủy' })
  await expect(oHuy).toContainText('Dữ liệu còn nhưng lớp ngừng hoạt động')
  await oHuy.click()
  expect(await soDaChon()).toBe(truoc + 1)

  await page.getByRole('button', { name: 'Lưu', exact: true }).click()
  await page.getByRole('button', { name: 'Đồng ý' }).click()
  await expect(page).toHaveURL('/quan-tri/phan-quyen')

  // --- Mở lại: đúng +1 ô, không mất ô nào ---
  await page.goto(duongDan)
  await expect(page.getByText(/Đã chọn \d+ ô/)).toBeVisible()
  expect(await soDaChon()).toBe(truoc + 1)
  await page.getByRole('button', { name: /LMS/ }).click()
  await expect(
    page.locator('section[aria-label="Lớp học"] label', { hasText: 'Hủy' }).locator('input'),
  ).toBeChecked()

  // --- Mẫu vai trò THAY toàn bộ, không cộng dồn ---
  await page.getByRole('button', { name: 'Học viên', exact: true }).click()
  await page.getByRole('button', { name: 'Đồng ý' }).click()

  const sauMau = await soDaChon()
  expect(sauMau).toBeLessThan(truoc + 1) // học viên ít quyền hơn giáo viên
  // Ô vừa tick thủ công phải BIẾN MẤT: mẫu thay chứ không gộp.
  await expect(
    page.locator('section[aria-label="Lớp học"] label', { hasText: 'Hủy' }).locator('input'),
  ).not.toBeChecked()
})

/**
 * Tab chỉ LỌC hiển thị — chuyển tab không được làm mất ô đã tick ở tab khác.
 *
 * Đây là lỗi rất dễ mắc nếu mỗi tab giữ state riêng, và hậu quả là mất quyền âm thầm ở hệ
 * thống con mà người dùng không hề mở ra xem (quy tắc #1).
 */
test('chuyển tab không mất ô đã tick ở tab khác', async ({ page, request }) => {
  await vaoHeThong(page, request, 'ma-tran-tab')

  await page.goto('/quan-tri/phan-quyen')
  await page.getByRole('link', { name: 'Trợ giảng' }).click()
  await expect(page.getByText(/Đã chọn \d+ ô/)).toBeVisible()

  const soDaChon = async () =>
    Number(/\d+/.exec(await page.getByText(/Đã chọn \d+ ô/).innerText())![0])

  const truoc = await soDaChon()

  // Tick một ô ở tab HRM (mặc định)...
  await page.locator('section[aria-label="Phòng ban"] label', { hasText: 'Xem' }).click()
  expect(await soDaChon()).toBe(truoc + 1)

  // ...sang CRM rồi LMS rồi quay lại: vẫn còn.
  await page.getByRole('button', { name: /CRM/ }).click()
  await page.getByRole('button', { name: /LMS/ }).click()
  await page.getByRole('button', { name: /HRM/ }).click()

  expect(await soDaChon()).toBe(truoc + 1)
  await expect(
    page.locator('section[aria-label="Phòng ban"] label', { hasText: 'Xem' }).locator('input'),
  ).toBeChecked()
})
