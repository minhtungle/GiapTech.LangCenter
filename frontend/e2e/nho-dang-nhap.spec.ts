import { expect, test } from '@playwright/test'
import { MAT_KHAU_MOI, taoTrungTam, dangNhap } from './tro-giup'

/**
 * "Nhớ đăng nhập" ở màn đăng nhập (22/09/2026).
 *
 * Chủ sản phẩm yêu cầu *"thêm nút nhớ mật khẩu"*; khi được hỏi đã chốt phương án **nhớ mã
 * trung tâm + tên đăng nhập, KHÔNG nhớ mật khẩu** — mã 7 ký tự vô nghĩa (`W686AE9`) mới là
 * thứ phiền khi gõ lại, còn mật khẩu để trình duyệt lo (xem `lib/nhoDangNhap.ts`).
 *
 * Test này đi qua UI thật vì phần dễ hỏng không nằm trong module lưu trữ (đã có
 * `nhoDangNhap.test.ts` canh), mà nằm ở chỗ **nối dây**: quên gọi `luuDaNho` sau khi đăng nhập,
 * quên truyền `defaultValues` vào `useForm`, hoặc lỡ điền cả mật khẩu.
 */
test('Tích nhớ thì lần sau điền sẵn mã + tên đăng nhập, KHÔNG điền mật khẩu', async ({
  page, request,
}) => {
  const tt = await taoTrungTam(request, 'nho-dang-nhap')
  await dangNhap(page, tt)

  // Đăng xuất bằng NÚT THẬT, không `goto('/dang-nhap')`: `goto` là tải lại trang, nó tự chữa
  // đúng loại lỗi trạng thái mà test đang muốn bắt (bài học 17/09, xem memory).
  await page.getByRole('button', { name: /Đăng xuất|Thoát/i }).first().click()
  await page.waitForURL(/dang-nhap/, { timeout: 15_000 })

  const oNho = page.getByRole('checkbox')
  // Máy dùng chung ở quầy lễ tân: mặc định TẮT, người dùng phải tự tích.
  await expect(oNho, 'ô nhớ phải mặc định tắt').not.toBeChecked()

  await page.fill('#maTrungTam', tt.maTrungTam)
  await page.fill('#username', tt.username)
  await page.fill('#matKhau', MAT_KHAU_MOI)
  await oNho.check()
  await page.click('button[type=submit]')
  await page.waitForURL((u) => u.pathname === '/', { timeout: 15_000 })

  // Mở lại trang đăng nhập trong một TAB MỚI của cùng trình duyệt — mô phỏng "hôm sau quay
  // lại". Dùng tab mới thay vì `goto` để chắc chắn dữ liệu đến từ `localStorage` chứ không
  // phải từ state React còn sót lại trong trang cũ.
  const tabMoi = await page.context().newPage()
  await tabMoi.goto('/dang-nhap')

  await expect(tabMoi.locator('#maTrungTam')).toHaveValue(tt.maTrungTam)
  await expect(tabMoi.locator('#username')).toHaveValue(tt.username)
  // ĐIỀU QUAN TRỌNG NHẤT: mật khẩu vẫn TRỐNG.
  await expect(tabMoi.locator('#matKhau'), 'không bao giờ điền sẵn mật khẩu').toHaveValue('')
  await expect(tabMoi.getByRole('checkbox'), 'đã nhớ thì ô tích sẵn').toBeChecked()

  // Và đăng nhập được ngay chỉ bằng mật khẩu — đó mới là giá trị của chức năng.
  await tabMoi.fill('#matKhau', MAT_KHAU_MOI)
  await tabMoi.click('button[type=submit]')
  await tabMoi.waitForURL((u) => u.pathname === '/', { timeout: 15_000 })

  // ---------- Bỏ tích thì quên NGAY ----------
  await tabMoi.getByRole('button', { name: /Đăng xuất|Thoát/i }).first().click()
  await tabMoi.waitForURL(/dang-nhap/, { timeout: 15_000 })

  await tabMoi.getByRole('checkbox').uncheck()

  // Quên ngay lúc bỏ tích, KHÔNG đợi lần đăng nhập thành công kế tiếp: người ở máy dùng chung
  // bỏ tích rồi đổi ý không đăng nhập nữa thì thông tin của họ phải biến mất ngay lúc đó.
  const tabBa = await page.context().newPage()
  await tabBa.goto('/dang-nhap')
  await expect(tabBa.locator('#maTrungTam')).toHaveValue('')
  await expect(tabBa.locator('#username')).toHaveValue('')
})
