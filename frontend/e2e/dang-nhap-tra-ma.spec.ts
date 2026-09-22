import { expect, test } from '@playwright/test'
import { taoTrungTam } from './tro-giup'

/**
 * FR-01 — trang đăng nhập tra tên trung tâm theo mã, để người dùng biết mình đang vào đâu
 * TRƯỚC khi gõ mật khẩu. Gõ sai một chữ mà chỉ biết sau khi điền hết form rồi nhận "sai thông
 * tin đăng nhập" thì không phân biệt được sai mã hay sai mật khẩu.
 *
 * **Không có đường tìm theo tên** — quyết định 20/08 của chủ sản phẩm. Endpoint này ẩn danh
 * nên cho tìm theo tên đồng nghĩa với việc ai cũng liệt kê được mọi trung tâm kèm mã.
 */

const GOI_Y = '7 ký tự, không phân biệt hoa thường'

/**
 * Kết quả tra nằm ngay dưới ô mã. Neo bằng vùng chứa ô `#maTrungTam` thay vì
 * `text=<tên trung tâm>`: tên hiện trong `<span class="truncate">`, mà `truncate` cắt theo thị
 * giác nên `page.locator('text=...')` không khớp chuỗi tên đầy đủ khi tên dài. Test đầu tiên
 * đỏ vì lý do đó chứ không phải vì chức năng sai — API trả đủ tên, DOM cũng có đủ.
 *
 * **22/09/2026 — đổi từ `~ p` sang `~ *`**: kết quả tra nay là một THẺ NHẬN DIỆN (`<div>` chứa
 * logo + tên + tên viết tắt), không còn là một thẻ `<p>`. Ba trạng thái kia (đang tra / không
 * tìm thấy / câu gợi ý) vẫn là `<p>`, nên `~ *` bắt được cả bốn.
 *
 * Dùng `.first()` ở chỗ gọi: `~ *` khớp MỌI anh em sau ô mã, không chỉ phần tử kế tiếp.
 */
const DONG_KET_QUA = '#maTrungTam ~ *'

test.describe('Đăng nhập — tra mã trung tâm', () => {
  test('mã đúng hiện tên trung tâm, mã sai hiện không tìm thấy', async ({ page, request }) => {
    const trungTam = await taoTrungTam(request, 'tra-ma')

    await page.goto('/dang-nhap')
    // Chưa gõ gì: giữ câu gợi ý, KHÔNG được hiện "không tìm thấy" khi chưa ai tra gì —
    // cùng loại lỗi đã gặp ở sổ đối thủ rỗng hôm 17/08.
    await expect(page.locator('text=Không tìm thấy trung tâm tương ứng')).toHaveCount(0)
    await expect(page.locator(`text=${GOI_Y}`)).toBeVisible()

    await page.fill('#maTrungTam', trungTam.maTrungTam)
    await expect(page.locator(DONG_KET_QUA).first())
      // `toContainText` chứ không `toHaveText`: thẻ nhận diện còn có tên viết tắt bên dưới.
      .toContainText(trungTam.tenTrungTam, { timeout: 10_000 })

    await page.fill('#maTrungTam', 'ZZZZZZZ')
    await expect(page.locator('text=Không tìm thấy trung tâm tương ứng')).toBeVisible({ timeout: 10_000 })

    // Xoá một ký tự: tên/thông báo cũ phải mất, về câu gợi ý. Giữ lại là nói sai về mã
    // đang nằm trong ô.
    await page.fill('#maTrungTam', 'ZZZZZZ')
    await expect(page.locator('text=Không tìm thấy trung tâm tương ứng')).toHaveCount(0)
    await expect(page.locator(`text=${GOI_Y}`)).toBeVisible()
  })

  test('gõ chữ thường vẫn tra ra tên trung tâm', async ({ page, request }) => {
    const trungTam = await taoTrungTam(request, 'tra-ma-thuong')

    await page.goto('/dang-nhap')
    await page.fill('#maTrungTam', trungTam.maTrungTam.toLowerCase())
    await expect(page.locator(DONG_KET_QUA).first())
      // `toContainText` chứ không `toHaveText`: thẻ nhận diện còn có tên viết tắt bên dưới.
      .toContainText(trungTam.tenTrungTam, { timeout: 10_000 })
  })

  test('gõ TÊN trung tâm vào ô mã thì không tra ra gì', async ({ page, request }) => {
    // Chốt chặn cho quyết định "chỉ tìm theo mã chính xác". Tên do `taoTrungTam` sinh luôn
    // dài hơn 7 ký tự nên phải cắt ra 7 ký tự đầu — nếu backend có nhánh `Contains` thì
    // đoạn này sẽ khớp và test đỏ.
    const trungTam = await taoTrungTam(request, 'go-ten')
    const bayChuDauTen = trungTam.tenTrungTam.slice(0, 7)

    await page.goto('/dang-nhap')
    await page.fill('#maTrungTam', bayChuDauTen)

    await expect(page.locator(DONG_KET_QUA).first())
      .toHaveText('Không tìm thấy trung tâm tương ứng', { timeout: 10_000 })
  })
})
