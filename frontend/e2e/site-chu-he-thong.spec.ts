import { expect, test } from '@playwright/test'

/**
 * Site chủ hệ thống (ADR-0009) — luồng thật trên trình duyệt.
 *
 * Cần `CHU_HE_THONG_MAT_KHAU` ở phía API để seeder tạo tài khoản chủ. Không có thì bỏ qua cả
 * file: thà **skip có lý do** còn hơn đỏ vì thiếu cấu hình môi trường — đỏ kiểu đó làm người
 * ta quen với việc "bộ test luôn có vài cái đỏ".
 */
const MAT_KHAU_CHU = process.env.CHU_HE_THONG_MAT_KHAU
const USERNAME_CHU = process.env.CHU_HE_THONG_USERNAME ?? 'chu'

test.describe('Site chủ hệ thống', () => {
  test.skip(
    !MAT_KHAU_CHU,
    'Chưa đặt CHU_HE_THONG_MAT_KHAU — không có tài khoản chủ để đăng nhập.',
  )

  test('đăng nhập, xem danh sách, tạo trung tâm, gắn domain', async ({ page }) => {
    await page.goto('/chu')

    await page.fill('#username', USERNAME_CHU)
    await page.fill('#matKhau', MAT_KHAU_CHU!)
    await page.getByRole('button', { name: 'Đăng nhập' }).click()

    // Vào được màn danh sách
    await expect(page).toHaveURL(/\/chu\/trung-tam$/)
    await expect(page.getByRole('heading', { name: 'Quản trị hệ thống' })).toBeVisible()

    // --- Tạo trung tâm ---
    const ten = `E2E site-chu ${Date.now()}`
    await page.getByRole('button', { name: 'Thêm trung tâm' }).click()
    await page.fill('#ten', ten)
    await page.getByRole('button', { name: 'Tạo', exact: true }).click()

    // Mật khẩu hiện ĐÚNG MỘT LẦN — đây là điểm dễ làm hỏng nhất khi sửa sau này.
    const hop = page.getByText('Chép mật khẩu ngay')
    await expect(hop).toBeVisible({ timeout: 10_000 })

    await page.getByRole('button', { name: 'Đã chép' }).click()
    await expect(page.getByRole('cell', { name: ten })).toBeVisible()

    // --- Gắn domain rồi gỡ ---
    const dong = page.getByRole('row').filter({ hasText: ten })
    await dong.getByRole('button', { name: 'Domain' }).click()

    const domain = `e2e-${Date.now()}.example.com`
    await page.fill('#dqt', domain)
    await page.getByRole('button', { name: 'Lưu' }).click()
    await expect(page.getByRole('cell', { name: domain })).toBeVisible({ timeout: 10_000 })

    // Gỡ domain: trung tâm KHÔNG mất đường vào — vẫn đăng nhập bằng mã (ADR-0008).
    await dong.getByRole('button', { name: 'Domain' }).click()
    await page.fill('#dqt', '')
    await page.getByRole('button', { name: 'Lưu' }).click()
    await expect(page.getByRole('cell', { name: domain })).toBeHidden({ timeout: 10_000 })
  })

  test('chưa đăng nhập thì không vào được màn danh sách', async ({ page }) => {
    await page.goto('/chu/trung-tam')

    // Token chủ chỉ nằm trong RAM, nên vào thẳng URL là bị đưa về màn đăng nhập.
    await expect(page).toHaveURL(/\/chu$/)
  })
})
