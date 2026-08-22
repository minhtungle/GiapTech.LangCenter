import { expect, test, type APIRequestContext } from '@playwright/test'
import { MAT_KHAU_MOI, taoClb, vaoHeThong } from './tro-giup'

/**
 * FR-18 — lời mời thách đấu qua link/QR.
 *
 * Điểm cần canh nhất ở tầng UI: trang xem link phải mở được **khi chưa đăng nhập**. Đây là thứ
 * duy nhất khiến ca "đối thủ chưa có tài khoản" chạy được, và nó dễ vỡ khi ai đó bọc route vào
 * `<CanThietDangNhap>` cho gọn.
 */

async function layToken(request: APIRequestContext, maDoi: string, matKhau: string) {
  const res = await request.post('/api/v1/auth/dang-nhap', {
    data: { maDoi, username: 'admin', matKhau },
  })
  return (await res.json()).accessToken as string
}

test.describe('Lời mời qua link', () => {
  test('xem được link khi CHƯA đăng nhập, và chấp nhận thì nâng cấp đối thủ', async ({
    page,
    request,
    browser,
  }) => {
    const benNhan = await taoClb(request, 'link-nhan')
    const benGui = await vaoHeThong(page, request, 'link-gui')

    // Tạo đối thủ tên gõ tay + link mời.
    await page.goto('/doi-thu')
    await page.click('button:has-text("Thêm đối thủ")')
    await page.fill('#tenDoi', 'Đội Chỉ Có Tên')
    await page.locator('dialog[open] button[type=submit]').click()
    await expect(page.locator('dialog[open]')).toHaveCount(0)

    const hang = page.locator('tbody tr', { hasText: 'Đội Chỉ Có Tên' })
    await hang.locator('button[title="Mời qua link/QR"]').click()

    await page.fill('#linkThoiGian', '2029-06-06T15:00')
    await page.fill('#linkDiaDiem', 'Sân E2E Link')
    await page.fill('#linkLoiNhan', 'Đá giao hữu nhé')
    await page.click('button:has-text("Tạo link")')

    // Link + QR hiện ra, kèm nhắc copy trước khi đóng (token chỉ trả một lần).
    const oLink = page.locator('#linkDaTao')
    await expect(oLink).toBeVisible()
    await expect(page.locator('dialog[open]')).toContainText('chỉ hiện một lần')
    await expect(page.locator('dialog[open] canvas')).toHaveCount(1)

    const link = await oLink.inputValue()
    expect(link).toContain('/loi-moi?token=')
    await page.locator('dialog[open] button:has-text("Đóng")').click()

    // --- Người nhận: ngữ cảnh SẠCH, không token nào ---
    const ctx = await browser.newContext()
    const trangNhan = await ctx.newPage()
    await trangNhan.goto(link)

    // Xem được KHÔNG cần đăng nhập — thứ duy nhất khiến ca "chưa có tài khoản" chạy được.
    await expect(trangNhan.locator('body')).toContainText(benGui.tenDoi)
    await expect(trangNhan.locator('body')).toContainText('Đội Chỉ Có Tên')
    await expect(trangNhan.locator('body')).toContainText('Sân E2E Link')
    await expect(trangNhan.locator('button:has-text("Tạo đội mới")')).toHaveCount(1)
    // Chưa đăng nhập thì KHÔNG có nút chấp nhận.
    await expect(trangNhan.locator('button:has-text("Đồng ý đá")')).toHaveCount(0)

    // Đăng nhập rồi mở lại link.
    await trangNhan.goto('/dang-nhap')
    await trangNhan.fill('#maDoi', benNhan.maDoi)
    await trangNhan.fill('#username', 'admin')
    await trangNhan.fill('#matKhau', benNhan.matKhau)
    await trangNhan.click('button[type=submit]')
    await trangNhan.waitForURL(/doi-mat-khau/, { timeout: 15_000 })
    await trangNhan.fill('#matKhauCu', benNhan.matKhau)
    await trangNhan.fill('#matKhauMoi', MAT_KHAU_MOI)
    const oXacNhan = trangNhan.locator('#xacNhan')
    if (await oXacNhan.count()) await oXacNhan.fill(MAT_KHAU_MOI)
    await trangNhan.locator('button[type=submit]').click()
    await trangNhan.waitForURL((u) => u.pathname === '/', { timeout: 15_000 })

    await trangNhan.goto(link)

    // Xác nhận danh tính: link chia sẻ được nên phải bắt họ đọc tên CLB mình trước khi bấm.
    await expect(trangNhan.locator('body')).toContainText(benNhan.tenDoi)
    await expect(trangNhan.locator('body')).toContainText('với tư cách')
    // Và nói trước việc sẽ tạo trận.
    await expect(trangNhan.locator('body')).toContainText('tạo một trận')

    await trangNhan.fill('textarea', 'OK, chốt luôn')
    await trangNhan.click('button:has-text("Đồng ý đá")')
    await expect(trangNhan.locator('body')).toContainText('Đã nhận lời mời')

    // Trận vào lịch bên NHẬN.
    await trangNhan.goto('/lich-thi-dau')
    await expect(trangNhan.locator('tbody')).toContainText(benGui.tenDoi)

    // Bên GỬI: tên giữ nguyên (ca 12), có mã đội, và nút mời-qua-link đã ẩn (ca 4).
    await page.goto('/doi-thu')
    const hangSau = page.locator('tbody tr', { hasText: 'Đội Chỉ Có Tên' })
    await expect(hangSau).toContainText(benNhan.maDoi)
    await expect(hangSau.locator('button[title="Mời qua link/QR"]')).toHaveCount(0)

    await ctx.close()
  })

  test('token sai báo rõ, không phải trang lỗi trắng', async ({ page }) => {
    await page.goto('/loi-moi?token=khong-ton-tai-gi-ca')
    await expect(page.locator('body')).toContainText('Không tìm thấy lời mời')
    // Có đường ra, không để người dùng mắc kẹt.
    await expect(page.locator('a:has-text("Về trang đăng nhập")')).toHaveCount(1)
  })

  test('trang đăng nhập hiện lại lối tạo câu lạc bộ', async ({ page }) => {
    // Đăng ký mở ở production từ 20/08 (nợ N4) — luồng "đối thủ chưa có tài khoản" cần nó.
    await page.goto('/dang-nhap')
    await expect(page.locator('body')).toContainText('Tạo câu lạc bộ')

    await page.click('text=Tạo câu lạc bộ')
    await expect(page).toHaveURL(/\/dang-ky/)
    // KHÔNG còn màn "Đăng ký chưa mở".
    await expect(page.locator('body')).not.toContainText('Đăng ký chưa mở')
  })
})
