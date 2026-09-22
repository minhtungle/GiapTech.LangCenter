import { expect, test } from '@playwright/test'
import { MAT_KHAU_MOI, taoTrungTam, dangNhap } from './tro-giup'

/**
 * Nhận diện trung tâm ở màn ĐĂNG NHẬP và trong sidebar (22/09/2026).
 *
 * Yêu cầu: *"nhập đúng mã trung tâm tại đăng nhập sẽ load đúng thông tin trung tâm như trong
 * thiết lập (logo, tên, ...), bên trong giao diện quản trị cũng vậy"*.
 *
 * Trung tâm mới tạo **chưa có logo** — đó là ca thường gặp nhất và cũng là ca dễ hỏng nhất
 * (chia cho `undefined`, `<img src="">` thành icon ảnh vỡ). Test đi đúng ca đó, rồi đặt tên
 * viết tắt để kiểm phần "load đúng thông tin từ thiết lập".
 */
test('Gõ mã đúng thì hiện nhận diện trung tâm; sidebar dùng tên từ thiết lập', async ({
  page, request,
}) => {
  const tt = await taoTrungTam(request, 'nhan-dien')

  /*
    Màn đăng nhập: gõ mã → hiện tên.

    Neo vào THẺ NHẬN DIỆN dưới ô mã (`#maTrungTam ~ *`), không dùng `getByText(tên)`: từ
    22/09 (bố cục banner) tên trung tâm xuất hiện ở **ba chỗ** — banner trái, thẻ nhận diện,
    và footer — nên `getByText` vi phạm strict mode. Đã đỏ đúng vậy khi chạy cả bộ.
  */
  await page.goto('/dang-nhap')
  await page.fill('#maTrungTam', tt.maTrungTam)

  const theNhanDien = page.locator('#maTrungTam ~ *').first()
  await expect(theNhanDien).toContainText(tt.tenTrungTam, { timeout: 15_000 })

  // Chiều NGƯỢC: mã sai thì thẻ không còn mang tên, và không hiện ảnh vỡ.
  await page.fill('#maTrungTam', 'ZZZZZZZ')
  await expect(theNhanDien).not.toContainText(tt.tenTrungTam)
  await expect(page.locator('img[src*="/auth/logo/"]')).toHaveCount(0)

  // ---------- Đăng nhập, đổi tên viết tắt ở Thiết lập ----------
  await page.fill('#maTrungTam', tt.maTrungTam)
  await dangNhap(page, tt)

  const tok = await page.evaluate(() => localStorage.getItem('lms_access_token'))
  const hienTai = await (await page.request.get('/api/v1/thiet-lap', {
    headers: { Authorization: `Bearer ${tok}` },
  })).json()

  /*
    ĐỔI TÊN — đây là mấu chốt của phép kiểm.

    Token chỉ mang tên lúc đăng nhập. Nếu sidebar đọc JWT thì sau khi đổi tên ở Thiết lập nó
    vẫn hiện tên CŨ tới khi đăng nhập lại. Giữ nguyên tên thì hai nguồn cho cùng kết quả và
    test không phân biệt được — mutation 22/09 xác nhận đúng vậy: bỏ `useNhanDienTrungTam`,
    quay về đọc JWT, test vẫn xanh.

    Gửi LẠI mọi trường (quy tắc #1) — lệnh cập nhật ghi đè, thiếu trường là xoá dữ liệu.
  */
  const tenMoi = `${tt.tenTrungTam} ĐÃ ĐỔI`

  const luu = await page.request.put('/api/v1/thiet-lap', {
    headers: { Authorization: `Bearer ${tok}` },
    data: { ...hienTai, tenTrungTam: tenMoi, tenVietTat: 'VTAT' },
  })
  expect(luu.ok(), `PUT /thiet-lap → ${luu.status()}`).toBeTruthy()

  // ---------- Sidebar lấy tên từ THIẾT LẬP, không từ token ----------
  await page.reload()
  await page.waitForTimeout(2500)

  const sidebar = page.locator('aside').first()
  await expect(sidebar, 'sidebar phải lấy tên MỚI từ thiết lập').toContainText(tenMoi)

  // Chưa tải logo → ô chữ cái, không phải thẻ <img> rỗng.
  await expect(page.locator('aside img')).toHaveCount(0)
})
