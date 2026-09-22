import { expect, test } from '@playwright/test'
import { MAT_KHAU_MOI, taoTrungTam, dangNhap } from './tro-giup'

/**
 * **Một phiên mỗi tài khoản** (20/09/2026) — *"chỉ cho phép 1 người đăng nhập tài khoản cùng
 * lúc"*.
 *
 * Chốt: **đẩy phiên CŨ ra** (người vừa đăng nhập được vào, như Facebook/Zalo), **hiệu lực
 * ngay** chứ không chờ access token hết hạn (60 phút).
 *
 * Kiểm bằng HAI trình duyệt độc lập (`browser.newContext`), không phải hai tab: hai tab dùng
 * chung `localStorage` nên tab sau ghi đè token của tab trước — sẽ không tái hiện được tình
 * huống thật là *hai người ở hai máy*.
 */
test('Đăng nhập nơi khác đẩy phiên cũ ra, người mới vẫn dùng bình thường', async ({
  browser, request,
}) => {
  const tt = await taoTrungTam(request, 'mot-phien')

  const ctxA = await browser.newContext()
  const ctxB = await browser.newContext()
  const a = await ctxA.newPage()
  const b = await ctxB.newPage()

  // Đổi mật khẩu lần đầu ở A, để B đăng nhập bằng mật khẩu mới.
  await dangNhap(a, tt)

  const vao = async (p: typeof a) => {
    await p.goto('/dang-nhap')
    await p.fill('#maTrungTam', tt.maTrungTam)
    await p.fill('#username', tt.username)
    await p.fill('#matKhau', MAT_KHAU_MOI)
    await p.click('button[type=submit]')
    await p.waitForURL((u) => !u.pathname.includes('dang-nhap'), { timeout: 30_000 })
  }

  // ---------- A đang dùng bình thường ----------
  await a.goto('/lms/lop-hoc')
  await expect(a).toHaveURL(/lop-hoc/)

  // ---------- B đăng nhập cùng nick ----------
  await vao(b)

  // Người MỚI phải dùng được NGAY — không chờ cache hết hạn.
  //
  // Đây là nửa dễ quên: bản đầu cache `phien_hien_tai` mà không xoá lúc đăng nhập, nên chính
  // người vừa đăng nhập cũng nhận 401. Chỉ kiểm "A bị đẩy ra" thì lỗi đó vẫn xanh.
  await b.goto('/lms/lop-hoc')
  await expect(b).toHaveURL(/lop-hoc/)
  await expect(b.locator('nav a').first()).toBeVisible({ timeout: 15_000 })

  // ---------- A thao tác tiếp → bị đẩy về màn đăng nhập KÈM LÝ DO ----------
  //
  // `goto` ở đây hay bị `ERR_ABORTED`: interceptor bắt 401 rồi tự đặt `window.location.href`,
  // tức điều hướng của chính nó CẮT NGANG lần điều hướng mà test vừa yêu cầu. Đó là hành vi
  // đúng, không phải lỗi — nên bỏ qua ngoại lệ và chỉ chờ kết quả cuối: về màn đăng nhập.
  //
  // `waitForURL` cũng phải bọc `catch` (22/09/2026), không chỉ `goto`: cú điều hướng bị cắt
  // ngang có thể rơi vào ĐÚNG lúc `waitForURL` đang chờ, và nó ném `ERR_ABORTED` y hệt. Trước
  // đây hiếm gặp nên trông như flaky; từ khi thêm `POST /auth/dang-xuat` thì mỗi lần thoát
  // phiên có thêm một request nữa, cửa sổ đua rộng ra và nó đỏ đều.
  //
  // Bọc xong vẫn KHÔNG mất sức canh: khẳng định thật nằm ở `expect(a).toHaveURL` ngay dưới —
  // `expect` tự thử lại nên không quan tâm điều hướng bị cắt mấy lần, chỉ quan tâm ĐÍCH ĐẾN.
  await a.goto('/lms/lop-hoc').catch(() => { /* interceptor cắt ngang, xem trên */ })
  await a.waitForURL(/dang-nhap/, { timeout: 20_000 })
    .catch(() => { /* điều hướng bị cắt ngang giữa chừng — xem trên */ })
  await expect(a).toHaveURL(/dang-nhap/, { timeout: 20_000 })

  // Nói RÕ vì sao, không để người dùng về màn đăng nhập trắng trơn mà không hiểu chuyện gì.
  await expect(a.locator('[role=alert]')).toContainText(/đăng nhập ở nơi khác/)

  // ---------- B vẫn không bị ảnh hưởng ----------
  await b.goto('/lms/khoa-online')
  await expect(b).toHaveURL(/khoa-online/)
})
