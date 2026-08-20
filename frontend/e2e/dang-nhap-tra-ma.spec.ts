import { expect, test } from '@playwright/test'
import { taoClb } from './tro-giup'

/**
 * FR-01 — trang đăng nhập tra tên đội theo mã, để người dùng biết mình đang vào CLB nào
 * TRƯỚC khi gõ mật khẩu. Gõ sai một chữ mà chỉ biết sau khi điền hết form rồi nhận "sai thông
 * tin đăng nhập" thì không phân biệt được sai mã hay sai mật khẩu.
 *
 * **Không có đường tìm theo tên** — quyết định 20/08 của chủ sản phẩm. Endpoint này ẩn danh
 * nên cho tìm theo tên đồng nghĩa với việc ai cũng liệt kê được mọi CLB kèm mã đội.
 */

const GOI_Y = '7 ký tự, không phân biệt hoa thường'

/**
 * Dòng kết quả tra nằm ngay dưới ô mã. Neo bằng vùng chứa ô `#maDoi` thay vì `text=<tên đội>`:
 * tên đội hiện trong `<span class="truncate">`, và `truncate` cắt theo thị giác nên
 * `page.locator('text=...')` không khớp được chuỗi tên đầy đủ khi tên dài. Test đầu tiên đỏ
 * vì lý do đó chứ không phải vì chức năng sai — API trả đủ tên, DOM cũng có đủ.
 */
const DONG_KET_QUA = '#maDoi ~ p'

test.describe('Đăng nhập — tra mã đội', () => {
  test('mã đúng hiện tên đội, mã sai hiện không tìm thấy', async ({ page, request }) => {
    const clb = await taoClb(request, 'tra-ma')

    await page.goto('/dang-nhap')
    // Chưa gõ gì: giữ câu gợi ý, KHÔNG được hiện "không tìm thấy" khi chưa ai tra gì —
    // cùng loại lỗi đã gặp ở sổ đối thủ rỗng hôm 17/08.
    await expect(page.locator('text=Không tìm thấy đội tương ứng')).toHaveCount(0)
    await expect(page.locator(`text=${GOI_Y}`)).toBeVisible()

    await page.fill('#maDoi', clb.maDoi)
    await expect(page.locator(DONG_KET_QUA)).toHaveText(clb.tenDoi, { timeout: 10_000 })

    await page.fill('#maDoi', 'ZZZZZZZ')
    await expect(page.locator('text=Không tìm thấy đội tương ứng')).toBeVisible({ timeout: 10_000 })

    // Xoá một ký tự: tên/thông báo cũ phải mất, về câu gợi ý. Giữ lại là nói sai về mã
    // đang nằm trong ô.
    await page.fill('#maDoi', 'ZZZZZZ')
    await expect(page.locator('text=Không tìm thấy đội tương ứng')).toHaveCount(0)
    await expect(page.locator(`text=${GOI_Y}`)).toBeVisible()
  })

  test('gõ chữ thường vẫn tra ra tên đội', async ({ page, request }) => {
    const clb = await taoClb(request, 'tra-ma-thuong')

    await page.goto('/dang-nhap')
    await page.fill('#maDoi', clb.maDoi.toLowerCase())
    await expect(page.locator(DONG_KET_QUA)).toHaveText(clb.tenDoi, { timeout: 10_000 })
  })

  test('gõ TÊN đội vào ô mã thì không tra ra gì', async ({ page, request }) => {
    // Chốt chặn cho quyết định "chỉ tìm theo mã chính xác". Tên CLB do `taoClb` sinh luôn
    // dài hơn 7 ký tự nên phải cắt ra 7 ký tự đầu — nếu backend có nhánh `Contains` thì
    // đoạn này sẽ khớp và test đỏ.
    const clb = await taoClb(request, 'go-ten')
    const bayChuDauTen = clb.tenDoi.slice(0, 7)

    await page.goto('/dang-nhap')
    await page.fill('#maDoi', bayChuDauTen)

    await expect(page.locator(DONG_KET_QUA)).toHaveText('Không tìm thấy đội tương ứng', {
      timeout: 10_000,
    })
  })
})
