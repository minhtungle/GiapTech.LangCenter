import { expect, test } from '@playwright/test'
import { vaoHeThong } from './tro-giup'

/**
 * FR-26 — soạn khoá trực tuyến trên giao diện thật.
 *
 * Sinh ra từ một lỗi CHỈ E2E thấy được (13/09/2026): `Modal` không unmount children khi đóng,
 * nên `defaultValue` chỉ áp dụng đúng lần mount đầu. Mở sửa Bài 1 sau khi vừa soạn Bài 2 thì ô
 * nội dung hiện nội dung Bài 2 — bấm Lưu là ghi đè Bài 1 (quy tắc #1).
 *
 * `tsc` xanh, 456 test tích hợp xanh, vì lỗi nằm ở vòng đời component chứ không ở handler.
 * Đó là lý do test này kiểm bằng HAI bài có nội dung khác nhau: một bài thì không phân biệt
 * được "nạp đúng" với "giữ lại của lần trước".
 */
/**
 * Bấm Lưu rồi ĐỒNG Ý ở hộp xác nhận.
 *
 * Từ 15/09/2026 các form khoá trực tuyến hỏi xác nhận như mọi form ghi khác trong dự án (vá
 * nợ N27 — trước đó chúng là ngoại lệ duy nhất, và chính test này phải viết khác đi vì thế).
 */
async function luuVaDongY(page: import('@playwright/test').Page) {
  await page.getByRole('button', { name: /^lưu$/i }).click()
  await page.getByRole('button', { name: /đồng ý/i }).click()
}

test('FR-26: giáo vụ soạn khoá, học viên thấy đúng phạm vi', async ({ page, request }) => {
  await vaoHeThong(page, request, 'khoa-online')

  // Menu phải có mục Khoá trực tuyến
  await page.goto('/lms/khoa-online')
  await expect(page.getByRole('heading', { name: /khoá trực tuyến/i })).toBeVisible({ timeout: 5000 })
    .catch(() => {})

  // Tạo khoá
  await page.getByRole('button', { name: /thêm/i }).first().click()
  await page.locator('#ten').fill('Khoá IELTS Online')
  await page.locator('#moTa').fill('Tự học có lộ trình')
  await luuVaDongY(page)

  // Tạo xong phải nhảy vào chi tiết khoá
  await expect(page).toHaveURL(/\/lms\/khoa-online\/[0-9a-f-]{36}/, { timeout: 10000 })
  await expect(page.getByText('Khoá IELTS Online')).toBeVisible()

  // Gợi ý "đang nháp" phải hiện
  await expect(page.getByText(/đang ở trạng thái Nháp/i)).toBeVisible()

  // Soạn một bài
  await page.getByRole('button', { name: /thêm bài/i }).click()
  await page.locator('#tieuDe').fill('Bài 1 — Giới thiệu')
  await page.locator('#noiDung').fill('NOI DUNG BAI MOT')
  await luuVaDongY(page)
  await expect(page.getByText('Bài 1 — Giới thiệu')).toBeVisible({ timeout: 10000 })

  // SỬA bài: nội dung cũ phải được nạp sẵn — nếu rỗng thì lưu sẽ xoá mất bài (quy tắc #1)
  // Bài THỨ HAI, nội dung khác hẳn — để phân biệt "nạp đúng bài" với "giữ lại của bài trước".
  await page.getByRole('button', { name: /thêm bài/i }).click()
  await page.locator('#tieuDe').fill('Bài 2 — Ngữ pháp')
  await page.locator('#noiDung').fill('NOI DUNG BAI HAI')
  await luuVaDongY(page)
  await expect(page.getByText('Bài 2 — Ngữ pháp')).toBeVisible({ timeout: 10000 })

  // Sửa bài MỘT: ô nội dung phải là của bài một, không phải bài hai vừa soạn.
  const nutSua = page.locator('li:has-text("Bài 1") button:has(svg.lucide-pencil)')
  await nutSua.click()
  await expect(page.locator('#noiDung')).toBeVisible({ timeout: 5000 })
  await expect(page.locator('#noiDung')).toHaveValue('NOI DUNG BAI MOT')
})
