import { expect, test } from '@playwright/test'
import { vaoHeThong } from './tro-giup'

/**
 * Cầu thủ nghỉ thi đấu (21/08).
 *
 * Điểm cốt lõi: đây **không phải xoá**. Lịch sử giữ nguyên, chỉ ẩn khỏi các chỗ chọn người cho
 * việc sắp tới. Bộ này canh cả hai nửa — ẩn đúng chỗ, và không mất gì.
 */

async function taoCauThu(page: import('@playwright/test').Page, hoTen: string) {
  await page.goto('/quan-tri/cau-thu')
  await page.click('button:has-text("Thêm cầu thủ")')
  await page.fill('#hoTen', hoTen)
  await page.locator('dialog[open] button[type=submit]').click()
  await expect(page.locator('dialog[open]')).toHaveCount(0)
}

test.describe('Nghỉ thi đấu', () => {
  test('cho nghỉ thì ẩn khỏi danh sách mặc định, lọc lại vẫn thấy', async ({ page, request }) => {
    await vaoHeThong(page, request, 'ntd-an')
    await taoCauThu(page, 'Người Đang Đá')
    await taoCauThu(page, 'Người Sẽ Nghỉ')

    await page.goto('/quan-tri/cau-thu')
    await expect(page.locator('tbody tr')).toHaveCount(2)

    await page
      .locator('tbody tr', { hasText: 'Người Sẽ Nghỉ' })
      .locator('button[title="Cho nghỉ thi đấu"]')
      .click()

    // Hộp xác nhận phải nói rõ dữ liệu KHÔNG mất — không có câu đó thì người dùng tưởng là xoá.
    await expect(page.locator('dialog[open]')).toContainText('GIỮ NGUYÊN')
    await page.click('dialog[open] button:has-text("Cho nghỉ thi đấu")')

    // Mặc định = chỉ người đang đá.
    await expect(page.locator('tbody tr')).toHaveCount(1)
    await expect(page.locator('tbody')).toContainText('Người Đang Đá')
    await expect(page.locator('tbody')).not.toContainText('Người Sẽ Nghỉ')

    // Lọc "Đã nghỉ" — không có đường này thì không ai cho họ đá lại được.
    await page.click('button:has-text("Đã nghỉ")')
    await expect(page.locator('tbody tr')).toHaveCount(1)
    await expect(page.locator('tbody')).toContainText('Người Sẽ Nghỉ')
    // Nhãn để phân biệt khi xem ở "Tất cả".
    await expect(page.locator('tbody')).toContainText('Đã nghỉ')

    await page.click('button:has-text("Tất cả")')
    await expect(page.locator('tbody tr')).toHaveCount(2)
  })

  test('cho đá lại thì hiện lại ở danh sách mặc định', async ({ page, request }) => {
    await vaoHeThong(page, request, 'ntd-dalai')
    await taoCauThu(page, 'Người Quay Lại')

    await page.goto('/quan-tri/cau-thu')
    await page.locator('button[title="Cho nghỉ thi đấu"]').first().click()
    await page.click('dialog[open] button:has-text("Cho nghỉ thi đấu")')
    await expect(page.locator('tbody tr')).toHaveCount(0)

    await page.click('button:has-text("Đã nghỉ")')
    await page.locator('button[title="Cho đá lại"]').first().click()

    await page.click('button:has-text("Đang đá")')
    await expect(page.locator('tbody')).toContainText('Người Quay Lại')
  })

  test('người đã nghỉ KHÔNG vào ô chọn mời đăng ký', async ({ page, request }) => {
    // Đây là lý do tính năng tồn tại: người nghỉ không được nằm trong việc sắp tới.
    await vaoHeThong(page, request, 'ntd-moi')
    await taoCauThu(page, 'Vẫn Đá Tiếp')
    await taoCauThu(page, 'Nghỉ Hẳn Rồi')

    await page.goto('/quan-tri/cau-thu')
    await page
      .locator('tbody tr', { hasText: 'Nghỉ Hẳn Rồi' })
      .locator('button[title="Cho nghỉ thi đấu"]')
      .click()
    await page.click('dialog[open] button:has-text("Cho nghỉ thi đấu")')

    // Tạo trận rồi mở tab Đăng ký.
    await page.goto('/lich-thi-dau')
    await page.click('button:has-text("Thêm trận đấu")')
    await page.click('#doiThuId')
    await page.fill('input[placeholder*="Gõ tên"]', 'FC Kiểm Nghỉ')
    await page.click('ul[role=listbox] button:has-text("Tạo đội")')
    await page.fill('#thoiGian', '2028-12-12T15:00')
    await page.locator('dialog[open] button[type=submit]').click()
    await expect(page.locator('dialog[open]')).toHaveCount(0)

    const nut = page
      .locator('tbody tr', { hasText: 'FC Kiểm Nghỉ' })
      .first()
      .locator('button[title]')
      .first()
    await nut.scrollIntoViewIfNeeded()
    await nut.click()
    await page.click('button:has-text("Đăng ký")')

    // Ô chọn người chỉ còn 1/1 — người đã nghỉ không nằm trong đó.
    // `poll` vì danh sách cầu thủ tải bằng query riêng: đọc ngay sẽ thấy "(0/0)".
    await expect
      .poll(
        async () =>
          (await page.locator('button:has-text("Ai được mời")').first().innerText())
            .replace(/\s+/g, ' '),
        { timeout: 10_000 },
      )
      .toContain('(1/1)')
  })

  test('KHÔNG hiện ô khoá tài khoản khi đó là tài khoản của chính mình', async ({
    page,
    request,
  }) => {
    // Tự khoá là mất đường vào hệ thống; nếu là admin duy nhất thì CLB mất luôn.
    // `vaoHeThong` đăng nhập bằng `admin`, nên gắn admin với một cầu thủ là dựng đúng tình huống.
    const clb = await vaoHeThong(page, request, 'ntd-tukhoa')
    await taoCauThu(page, 'Chính Là Admin')

    await page.goto('/quan-tri/tai-khoan')
    await page
      .locator('tbody tr', { hasText: clb.username })
      .locator('button[title]')
      .first()
      .click()
    // Gán hồ sơ cầu thủ cho tài khoản admin.
    await page.click('#cauThuId')
    await page.click('ul[role=listbox] li:has-text("Chính Là Admin")')
    await page.locator('dialog[open] button[type=submit]').click()
    await expect(page.locator('dialog[open]')).toHaveCount(0)

    await page.goto('/quan-tri/cau-thu')
    await page.locator('button[title="Cho nghỉ thi đấu"]').first().click()

    // Ô tích phải KHÔNG có, và phải giải thích vì sao.
    await expect(page.locator('dialog[open] input[type=checkbox]')).toHaveCount(0)
    await expect(page.locator('dialog[open]')).toContainText('chính tài khoản bạn đang dùng')
  })
})
