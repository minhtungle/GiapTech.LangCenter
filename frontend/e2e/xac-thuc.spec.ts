import { expect, test } from '@playwright/test'
import { MAT_KHAU_MOI, dangNhap, taoTrungTam } from './tro-giup'

/**
 * FR-01 — đăng nhập bằng bộ ba {mã đội, tên đăng nhập, mật khẩu} và buộc đổi mật khẩu lần đầu.
 */
test.describe('Xác thực', () => {
  test('đăng nhập rồi buộc đổi mật khẩu lần đầu', async ({ page, request }) => {
    const trungTam = await taoTrungTam(request, 'xac-thuc')

    await page.goto('/dang-nhap')
    await page.fill('#maTrungTam', trungTam.maTrungTam)
    await page.fill('#username', trungTam.username)
    await page.fill('#matKhau', trungTam.matKhau)
    await page.click('button[type=submit]')

    // Middleware chặn ở tầng API, không phó mặc frontend.
    await expect(page).toHaveURL(/doi-mat-khau/)

    await page.fill('#matKhauCu', trungTam.matKhau)
    await page.fill('#matKhauMoi', MAT_KHAU_MOI)
    await page.fill('#xacNhan', MAT_KHAU_MOI)
    await page.locator('button[type=submit]').click()

    await expect(page).toHaveURL((u) => u.pathname === '/')
  })

  test('sidebar hiện TÊN đội, không phải mã đội', async ({ page, request }) => {
    // Lỗi thật ngày 16/08: claim ten_doi chỉ có trong token MỚI, người đang mở phiên cầm
    // token cũ nên sidebar chỉ hiện mã đội.
    const trungTam = await taoTrungTam(request, 'ten-doi')
    await dangNhap(page, trungTam)

    const sidebar = page.locator('aside, nav').first()
    await expect(sidebar).toContainText(trungTam.tenTrungTam)
    await expect(sidebar).toContainText(trungTam.maTrungTam)
  })

  test('mật khẩu sai bị từ chối kèm thông báo', async ({ page, request }) => {
    const trungTam = await taoTrungTam(request, 'sai-mk')

    await page.goto('/dang-nhap')
    await page.fill('#maTrungTam', trungTam.maTrungTam)
    await page.fill('#username', trungTam.username)
    await page.fill('#matKhau', 'sai-hoan-toan')
    await page.click('button[type=submit]')

    // Vẫn ở trang đăng nhập và có thông báo — không phải trắng trang hay mã lỗi thô.
    await expect(page).toHaveURL(/dang-nhap/)
    await expect(page.locator('main, form')).toContainText(/không đúng|thất bại/i)
  })

  test('chưa đăng nhập thì bị đẩy về trang đăng nhập', async ({ page }) => {
    await page.goto('/quan-tri/tai-khoan')
    await expect(page).toHaveURL(/dang-nhap/)
  })
})
