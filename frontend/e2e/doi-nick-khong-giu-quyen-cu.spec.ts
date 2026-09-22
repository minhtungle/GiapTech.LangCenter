import { expect, test } from '@playwright/test'
import { MAT_KHAU_MOI, taoTrungTam, dangNhap, layTokenQuaApi } from './tro-giup'

/**
 * Đăng xuất rồi đăng nhập nick khác → giao diện phải theo quyền NICK MỚI ngay (17/09/2026).
 *
 * Người dùng báo: *"đăng xuất và đăng nhập tài khoản khác thì giao diện quyền đang ở nick cũ,
 * phải ctrl shift R mới refresh được"*. Tái hiện được: đăng nhập `admin` rồi đổi sang học viên
 * thì học viên **thấy nguyên menu quản trị** — kể cả *Phân quyền* và *Nhật ký hệ thống*.
 *
 * ## Gốc rễ
 *
 * Mọi `queryKey` đều là hằng (`['toi-quyen']`, `['toi-he-thong']`…) — **không mang danh tính
 * người đăng nhập** — cộng với `staleTime: Infinity` ở `useQuyen`. Chữa bằng `queryClient.clear()`
 * tại chỗ đổi phiên, không đi thêm `username` vào ~20 khoá (chỗ thứ 21 thêm sau sẽ quên).
 *
 * ## ⚠️ Vì sao test này TUYỆT ĐỐI không được `goto('/dang-nhap')` giữa hai lần đăng nhập
 *
 * Bản đầu của test này dùng `page.goto()` để quay về màn đăng nhập, và **nó xanh cả khi đã gỡ
 * bỏ hoàn toàn bản sửa** — vì `goto` là tải lại trang, mà tải lại trang thì cache nằm trong bộ
 * nhớ JS mất sạch, đúng cái "ctrl shift R" mà người dùng đang phải làm thủ công. Test khi ấy
 * kiểm đúng cái đường đã lành, bỏ qua đường đang hỏng.
 *
 * Nên ở đây **phải bấm nút Đăng xuất** rồi điền thẳng vào form: đó là đường người dùng đi, và
 * là đường duy nhất giữ nguyên `QueryClient` để lộ ra lỗi. Kiểm chứng bằng mutation: gỡ cả hai
 * `qc.clear()` thì test này đỏ (học viên thấy `Phân quyền`).
 *
 * ## Vì sao kiểm CẢ HAI CHIỀU
 *
 * Chỉ kiểm "quản trị → học viên" thì một bản sửa quá tay (ẩn hết menu cho chắc) cũng xanh.
 * Test đi một vòng: quản trị → học viên → quản trị, menu phải **co lại rồi nở ra** đúng.
 */
test('Đổi nick thì menu theo quyền nick mới, không cần F5', async ({ page, request }) => {
  const tt = await taoTrungTam(request, 'doi-nick')
  await dangNhap(page, tt)

  const tok = await layTokenQuaApi(page)
  const goiApi = async (duong: string, than: unknown) => {
    const res = await page.request.post(`/api/v1${duong}`, {
      headers: { Authorization: `Bearer ${tok}` },
      data: than,
    })
    expect(res.ok(), `${duong} → ${res.status()} ${await res.text()}`).toBeTruthy()
  }

  const quyens = await (
    await page.request.get('/api/v1/quyen', { headers: { Authorization: `Bearer ${tok}` } })
  ).json()
  const idQuyenHocVien = (quyens as { id: string; tenQuyen: string }[]).find(
    (q) => q.tenQuyen === 'Học viên',
  )!.id

  await goiApi('/nguoi-dung', {
    hoTen: 'HV đổi nick',
    loaiNguoiDung: 'HocVien',
    taiKhoan: {
      username: 'hv-doinick',
      matKhau: MAT_KHAU_MOI,
      quyenIds: [idQuyenHocVien],
      phaiDoiMatKhau: false,
    },
  })

  const loiJs: string[] = []
  page.on('pageerror', (e) => loiJs.push(e.message))

  const menu = async () => (await page.locator('nav a').allInnerTexts()).filter(Boolean).join(' | ')

  /**
   * Đổi nick ĐÚNG CÁCH NGƯỜI DÙNG LÀM: bấm Đăng xuất rồi điền form — **không** `goto`,
   * không `reload`. Xem khối chú thích đầu file: `goto` che mất chính lỗi đang kiểm.
   */
  const doiNick = async (username: string) => {
    await page.getByRole('button', { name: /Đăng xuất/ }).click()
    await page.waitForURL(/dang-nhap/, { timeout: 15_000 })

    await page.fill('#maTrungTam', tt.maTrungTam)
    await page.fill('#username', username)
    await page.fill('#matKhau', MAT_KHAU_MOI)
    await page.click('button[type=submit]')
    await page.waitForURL((u) => !u.pathname.includes('dang-nhap'), { timeout: 30_000 })
    // `useQuyen` có trạng thái `dangTai`; chờ quyền về rồi mới đọc menu.
    await page.waitForTimeout(1500)
  }

  // ---------- Quản trị: thấy mục quản trị ----------
  expect(await menu(), 'quản trị phải thấy Phân quyền').toContain('Phân quyền')

  // ---------- Đổi sang HỌC VIÊN: menu phải CO LẠI ngay, không cần F5 ----------
  await doiNick('hv-doinick')

  const menuHv = await menu()
  expect(menuHv, 'học viên KHÔNG được giữ menu Phân quyền của nick cũ').not.toContain('Phân quyền')
  expect(menuHv, 'học viên KHÔNG được thấy Nhật ký hệ thống').not.toContain('Nhật ký hệ thống')
  expect(menuHv, 'học viên vẫn phải thấy mục của mình').toContain('Lớp học')

  // F5 KHÔNG được làm đổi gì nữa — trước bản sửa, chỉ F5 mới ra đúng.
  await page.reload()
  await page.waitForTimeout(2000)
  expect(await menu(), 'menu trước và sau F5 phải giống nhau').toBe(menuHv)

  // ---------- CHIỀU NGƯỢC: về quản trị, menu phải NỞ RA lại ----------
  await doiNick(tt.username)
  expect(await menu(), 'quản trị đăng nhập lại phải thấy đủ menu').toContain('Phân quyền')

  expect(loiJs, 'có lỗi JS chưa xử lý').toEqual([])
})
