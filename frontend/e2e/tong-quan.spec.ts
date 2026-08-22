import { expect, test } from '@playwright/test'
import { vaoHeThong } from './tro-giup'

/**
 * Màn Tổng quan (nợ N6, 21/08).
 *
 * Trước đó chỉ có "Xin chào, admin" — 12 dòng JSX. Đây là màn ĐẦU TIÊN sau khi đăng nhập, nên nó
 * phải vào được với mọi tài khoản và không vỡ khi CLB chưa có dữ liệu.
 */
test.describe('Tổng quan', () => {
  test('CLB mới: không việc gì, KHÔNG trang trắng', async ({ page, request }) => {
    // CLB vừa tạo không có trận/quỹ/lời mời. Trạng thái rỗng phải nói rõ, không để trống —
    // người dùng khỏi tưởng lỗi.
    await vaoHeThong(page, request, 'tq-rong')

    // Neo vào `main` và dùng `.first()`: "Trận đấu" trùng một phần với menu "Lịch thi đấu" ở
    // sidebar nên `text=` khớp hai phần tử và Playwright báo strict mode violation.
    const noiDung = page.locator('main')
    await expect(noiDung.locator('text=Việc cần làm')).toBeVisible()
    await expect(noiDung.locator('text=Không có việc nào cần xử lý')).toBeVisible()
    await expect(noiDung.locator('text=Trận kế tiếp')).toBeVisible()
  })

  test('có trận chưa mời đăng ký thì hiện việc, bấm được để tới lịch', async ({
    page,
    request,
  }) => {
    // Mỗi dòng việc phải BẤM ĐƯỢC tới đúng chỗ xử lý. Một con số không kèm đường đi tiếp chỉ làm
    // người dùng biết có việc mà không biết làm ở đâu.
    await vaoHeThong(page, request, 'tq-viec')

    await page.goto('/lich-thi-dau')
    await page.click('button:has-text("Thêm trận đấu")')
    await page.click('#doiThuId')
    await page.fill('input[placeholder*="Gõ tên"]', 'FC Tổng Quan')
    await page.click('ul[role=listbox] button:has-text("Tạo đội")')
    await page.fill('#thoiGian', '2029-03-03T15:00')
    await page.locator('dialog[open] button[type=submit]').click()
    await expect(page.locator('dialog[open]')).toHaveCount(0)

    await page.goto('/')
    const viec = page.locator('a:has-text("chưa mời đăng ký")')
    await expect(viec).toBeVisible({ timeout: 10_000 })

    await viec.click()
    await expect(page).toHaveURL(/lich-thi-dau/)
  })

  test('trận kế tiếp bấm được để mở chi tiết trận', async ({ page, request }) => {
    await vaoHeThong(page, request, 'tq-tran')

    await page.goto('/lich-thi-dau')
    await page.click('button:has-text("Thêm trận đấu")')
    await page.click('#doiThuId')
    await page.fill('input[placeholder*="Gõ tên"]', 'FC Kế Tiếp')
    await page.click('ul[role=listbox] button:has-text("Tạo đội")')
    await page.fill('#thoiGian', '2029-04-04T15:00')
    await page.locator('dialog[open] button[type=submit]').click()
    await expect(page.locator('dialog[open]')).toHaveCount(0)

    await page.goto('/')
    await expect(page.locator('text=FC Kế Tiếp')).toBeVisible({ timeout: 10_000 })
    // Chưa gửi lời mời → nói rõ, không hiện "0/0" (hai thứ đó khác nhau).
    // `.first()` vì dòng việc "trận sắp tới chưa mời đăng ký" cũng chứa chuỗi này.
    await expect(page.locator('text=Chưa mời đăng ký').first()).toBeVisible()

    await page.click('a:has-text("FC Kế Tiếp")')
    await expect(page).toHaveURL(/lich-thi-dau\/[0-9a-f-]{36}/)
  })
})
