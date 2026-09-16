import { expect, test } from '@playwright/test'
import { vaoHeThong } from './tro-giup'

/**
 * Vai trò **Nhân viên kinh doanh** (16/09/2026) — yêu cầu chủ sản phẩm:
 * *"vai trò nhân viên => nhân viên kinh doanh. tránh nhầm lẫn"*.
 *
 * Làm thành vai trò **riêng** thay vì đổi tên `NhanVien`, vì `NhanVien` còn gồm hành chính,
 * nhân sự, IT — và là vai trò của **tài khoản quản trị** ở mọi trung tâm mới. Đổi nhãn sẽ gọi
 * người quản trị hệ thống là nhân viên kinh doanh: tạo ra nhầm lẫn mới thay vì bỏ nhầm lẫn cũ.
 *
 * Test này canh đúng điều đó ở màn hình: hai vai trò phải **đọc ra khác nhau** trên cùng một
 * bảng, và ô lọc phải tách được chúng.
 */
test('Hai vai trò nhân viên tách biệt trên màn Nhân sự', async ({ page, request }) => {
  await vaoHeThong(page, request, 'vai-tro-kd')
  const token = await page.evaluate(() => localStorage.getItem('lms_access_token'))

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

  await api('/nhan-su', { hoTen: 'Trần Sale', loaiNguoiDung: 'NhanVienKinhDoanh' })
  await api('/nhan-su', { hoTen: 'Lê Hành Chính', loaiNguoiDung: 'NhanVien' })

  await page.goto('/hrm?tab=nhan-su')
  await expect(page.getByText('Trần Sale')).toBeVisible({ timeout: 15000 })

  const dong = (ten: string) => page.locator('tbody tr').filter({ hasText: ten })

  // --- Nhãn phải PHÂN BIỆT ĐƯỢC, không cùng đọc là "Nhân viên" ---
  await expect(dong('Trần Sale')).toContainText('Nhân viên kinh doanh')
  await expect(dong('Lê Hành Chính')).toContainText('Nhân viên khác')

  /*
    Chốt QUAN TRỌNG NHẤT của cách làm này: tài khoản quản trị do seeder tạo mang vai trò
    `NhanVien`, nên nếu chỉ đổi nhãn `NhanVien` thành "Nhân viên kinh doanh" thì dòng này sẽ
    gọi người quản trị hệ thống là sale. Ở mọi trung tâm mới.
  */
  await expect(dong('Quản trị viên')).toContainText('Nhân viên khác')
  await expect(dong('Quản trị viên')).not.toContainText('Nhân viên kinh doanh')

  // --- Ô lọc có đủ bốn vai trò nhân sự, và tách được hai loại nhân viên ---
  await page.locator('#loc-vai-tro').click()
  for (const nhan of ['Nhân viên khác', 'Nhân viên kinh doanh', 'Giáo viên', 'Trợ giảng'])
    await expect(page.getByRole('option', { name: nhan })).toBeVisible()

  await page.getByRole('option', { name: 'Nhân viên kinh doanh' }).click()
  await page.waitForTimeout(1200)

  await expect(dong('Trần Sale')).toHaveCount(1)
  await expect(dong('Lê Hành Chính')).toHaveCount(0)   // chiều LOẠI
  await expect(dong('Quản trị viên')).toHaveCount(0)

  expect(loiJs, 'có lỗi JS chưa xử lý').toEqual([])
})
