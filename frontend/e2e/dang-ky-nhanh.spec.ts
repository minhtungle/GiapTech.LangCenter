import { expect, test } from '@playwright/test'
import { vaoHeThong } from './tro-giup'

/**
 * FR-19 — đăng ký đá trận qua link/QR, không cần đăng nhập.
 *
 * Ba lỗi trong bộ này chỉ thấy khi XEM ẢNH CHỤP, 369 test backend đều xanh khi chúng còn:
 * 1. `traLoi` là CHUỖI ("ThamGia") nhưng frontend so với SỐ → bảng hiện "Chưa trả lời" cho người
 *    đã trả lời và dòng thống kê đếm 0. Không lỗi console. Lệnh ghi nhận cả số lẫn chuỗi nên
 *    trả lời vẫn thành công — điều đó che mất lỗi ở phần đọc.
 * 2. Danh sách tick mặc định 16/16 trong khi lời mời chỉ 14 người → bấm "Lưu danh sách" ngay sau
 *    khi mở tab sẽ âm thầm mời lại người vừa bỏ.
 * 3. Hiện `Tenant.SanNha` làm địa điểm trận → người đọc đến sai sân.
 */

/** Tạo một trận rồi trả về URL chi tiết của nó. */
async function taoTran(page: import('@playwright/test').Page, tenDoiThu: string) {
  await page.goto('/lich-thi-dau')
  await page.click('button:has-text("Thêm trận đấu")')
  await page.click('#doiThuId')
  await page.fill('input[placeholder*="Gõ tên"]', tenDoiThu)
  await page.click('ul[role=listbox] button:has-text("Tạo đội")')
  await page.fill('#thoiGian', '2028-11-11T15:00')
  await page.locator('dialog[open] button[type=submit]').click()
  await expect(page.locator('dialog[open]')).toHaveCount(0)

  // Bấm NÚT "Mở chi tiết" trong dòng, không phải cả dòng — dòng không có onClick.
  await page
    .locator('tbody tr', { hasText: tenDoiThu })
    .first()
    // Title lấy từ i18n `tranDau.moChiTiet` — neo vào icon thì bền hơn khi câu đó đổi.
    .locator('button[title]')
    .first()
    .click()
  await expect(page).toHaveURL(/lich-thi-dau\/[0-9a-f-]{36}/)
  return page.url()
}

test.describe('Đăng ký nhanh qua link', () => {
  test('tab Đăng ký nằm TRONG chi tiết trận, không phải Hòm thư', async ({ page, request }) => {
    // Yêu cầu của chủ sản phẩm 21/08. Trước đó nút tạo lời mời ở Hòm thư nên trưởng nhóm phải
    // rời trận đang xem để đi tìm.
    await vaoHeThong(page, request, 'dkn-cho-dat')
    await taoTran(page, 'FC Chỗ Đặt')

    await expect(page.locator('button:has-text("Đăng ký")')).toBeVisible()
  })

  test('mặc định tick HẾT, bỏ tick được, và chỉ mời người đã tick', async ({ page, request }) => {
    await vaoHeThong(page, request, 'dkn-chon')

    // Cần vài cầu thủ để có gì mà tick.
    for (const ten of ['Người A', 'Người B', 'Người C']) {
      await page.goto('/quan-tri/cau-thu')
      await page.click('button:has-text("Thêm cầu thủ")')
      await page.fill('#hoTen', ten)
      await page.locator('dialog[open] button[type=submit]').click()
      await expect(page.locator('dialog[open]')).toHaveCount(0)
    }

    await taoTran(page, 'FC Chọn Người')
    await page.click('button:has-text("Đăng ký")')

    // Mặc định tick hết — giữ hành vi cũ "mời tất cả".
    await expect(page.locator('text=/Ai được mời \\(3\\/3\\)/')).toBeVisible()

    // Bỏ một người rồi gửi: lời mời chỉ có 2 người.
    await page.locator('input[type=checkbox]').first().uncheck()
    await expect(page.locator('text=/Ai được mời \\(2\\/3\\)/')).toBeVisible()
    await page.click('button:has-text("Gửi lời mời đăng ký")')

    await expect(page.locator('tbody tr')).toHaveCount(2)

    // Mở lại tab: danh sách tick phải là 2/3, KHÔNG quay về 3/3. Quay về 3/3 nghĩa là bấm
    // "Lưu danh sách" sẽ âm thầm mời lại người vừa bỏ.
    await page.reload()
    await page.click('button:has-text("Đăng ký")')
    await expect(page.locator('text=/Ai được mời \\(2\\/3\\)/')).toBeVisible({ timeout: 10_000 })
  })

  test('người ẨN DANH mở link, chọn tên, trả lời — trưởng nhóm thấy ngay', async ({
    page,
    request,
    browser,
  }) => {
    await vaoHeThong(page, request, 'dkn-an-danh')

    await page.goto('/quan-tri/cau-thu')
    await page.click('button:has-text("Thêm cầu thủ")')
    await page.fill('#hoTen', 'Người Đăng Ký')
    await page.locator('dialog[open] button[type=submit]').click()
    await expect(page.locator('dialog[open]')).toHaveCount(0)

    const urlTran = await taoTran(page, 'FC Ẩn Danh')
    await page.click('button:has-text("Đăng ký")')
    await page.click('button:has-text("Gửi lời mời đăng ký")')
    await expect(page.locator('tbody tr')).toHaveCount(1)

    await page.click('button:has-text("Tạo link + QR")')
    const link = await page.locator('#dkLink').inputValue()
    expect(link).toContain('/dang-ky-nhanh?token=')

    // Context RIÊNG, không cookie/token — đúng trải nghiệm người được mời.
    const ctx = await browser.newContext()
    const anDanh = await ctx.newPage()
    await anDanh.goto(link)

    await expect(anDanh.locator('#dknTen')).toBeVisible()
    await anDanh.selectOption('#dknTen', { index: 1 })
    await anDanh.fill('#dknGhiChu', 'Đến muộn 10 phút')
    await anDanh.click('button:has-text("Tham gia")')
    await expect(anDanh.locator('text=Đã ghi nhận')).toBeVisible({ timeout: 10_000 })

    // KHÔNG hiện sân nhà CLB làm địa điểm trận — người đọc sẽ đến sai sân.
    await expect(anDanh.locator('text=Sân Chi Lăng')).toHaveCount(0)
    await ctx.close()

    // Trưởng nhóm xem lại: `traLoi` là CHUỖI ở API, nếu frontend so với SỐ thì ô này hiện
    // "Chưa trả lời" và dòng thống kê đếm 0 — sai im lặng, không lỗi console.
    await page.goto(urlTran)
    await page.click('button:has-text("Đăng ký")')
    const dong = page.locator('tbody tr', { hasText: 'Người Đăng Ký' })
    await expect(dong).toContainText('Tham gia')
    await expect(dong).toContainText('Qua link')
    await expect(dong).toContainText('Đến muộn 10 phút')
    await expect(page.locator('text=/Tham gia: 1/')).toBeVisible()
  })

  test('link bị thu hồi thì nói rõ, không phải trang 404', async ({ page, request, browser }) => {
    await vaoHeThong(page, request, 'dkn-thu-hoi')

    await page.goto('/quan-tri/cau-thu')
    await page.click('button:has-text("Thêm cầu thủ")')
    await page.fill('#hoTen', 'Người Thu Hồi')
    await page.locator('dialog[open] button[type=submit]').click()
    await expect(page.locator('dialog[open]')).toHaveCount(0)

    await taoTran(page, 'FC Thu Hồi')
    await page.click('button:has-text("Đăng ký")')
    await page.click('button:has-text("Gửi lời mời đăng ký")')
    await page.click('button:has-text("Tạo link + QR")')
    const link = await page.locator('#dkLink').inputValue()

    await page.click('button:has-text("Thu hồi link")')
    await expect(page.locator('button:has-text("Thu hồi link")')).toHaveCount(0)

    const ctx = await browser.newContext()
    const anDanh = await ctx.newPage()
    await anDanh.goto(link)

    // 404 làm người dùng tưởng link sai rồi bỏ luôn, thay vì liên hệ trưởng nhóm.
    await expect(anDanh.locator('text=Link không dùng được')).toBeVisible()
    await expect(anDanh.locator('text=Liên hệ trưởng nhóm')).toBeVisible()
    await expect(anDanh.locator('#dknTen')).toHaveCount(0)
    await ctx.close()
  })
})
