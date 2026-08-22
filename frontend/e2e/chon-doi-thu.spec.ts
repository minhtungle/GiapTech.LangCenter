import { expect, test } from '@playwright/test'
import { taoClb, vaoHeThong } from './tro-giup'

/**
 * FR-10 — chọn đối thủ khi thêm trận, ba đường vào: chọn từ sổ, gõ tên tạo mới, tra mã CLB.
 *
 * Bốn lỗi trong bộ này chỉ nhìn màn hình mới thấy, 220 test backend đều xanh khi chúng còn:
 * "Không tìm thấy" hiện lúc sổ rỗng, placeholder mono chồng chữ, thông báo lỗi nằm dưới lớp
 * dropdown nên không ai thấy, và cùng một câu in hai lần.
 */

/** Ô tra mã nằm trong dropdown; "Tra" khớp cả tên đội chứa chữ "Tra" nên phải neo chính xác. */
const O_MA = 'input[placeholder="ZAYE3TM"]'
const NUT_TRA = `button:near(${O_MA}) >> text=/^Tra$/`

test.describe('Chọn đối thủ', () => {
  test('gõ tên lạ tạo được đội ngay trong dropdown', async ({ page, request }) => {
    await vaoHeThong(page, request, 'tao-doi-thu')

    await page.goto('/lich-thi-dau')
    await page.click('button:has-text("Thêm trận đấu")')
    await page.click('#doiThuId')

    // Sổ đối thủ của CLB mới là rỗng. "Không tìm thấy" ở đây là câu vô nghĩa — chưa ai tìm gì.
    await expect(page.locator('ul[role=listbox]')).not.toContainText('Không tìm thấy')

    const ten = `FC Gõ Tay ${Date.now()}`
    await page.fill('input[placeholder*="Gõ tên"]', ten)
    await page.click('ul[role=listbox] button:has-text("Tạo đội")')

    // Tạo xong phải TỰ CHỌN luôn: bắt người dùng mở lại dropdown chọn tay là thừa một bước.
    await expect(page.locator('#doiThuId')).toContainText(ten)

    await page.fill('#thoiGian', '2028-09-09T15:00')
    await page.locator('dialog[open] button[type=submit]').click()
    await expect(page.locator('dialog[open]')).toHaveCount(0)
    await expect(page.locator('tbody')).toContainText(ten)
  })

  test('tra mã CLB khác rồi thêm vào sổ, lưu được thành đối thủ của trận', async ({
    page,
    request,
  }) => {
    // Cần HAI CLB: một để đăng nhập, một để đem đi tra. `vaoHeThong` chỉ tạo một.
    const doiDuocTra = await taoClb(request, 'duoc-tra')
    await vaoHeThong(page, request, 'di-tra')

    await page.goto('/lich-thi-dau')
    await page.click('button:has-text("Thêm trận đấu")')
    await page.click('#doiThuId')

    await page.fill(O_MA, doiDuocTra.maDoi)
    await page.click(NUT_TRA)

    // Kết quả tra nằm NGOÀI `ul[role=listbox]` (ul chỉ chứa sổ đối thủ), nên neo vào khối
    // chứa nút "Thêm vào sổ" thay vì listbox.
    const khoiKetQua = page.locator('div', { has: page.locator('button:has-text("Thêm vào sổ")') })
    await expect(khoiKetQua.last()).toContainText(doiDuocTra.tenDoi)
    await page.click('button:has-text("Thêm vào sổ")')
    await expect(page.locator('#doiThuId')).toContainText(doiDuocTra.tenDoi)

    await page.fill('#thoiGian', '2028-10-10T15:00')
    await page.locator('dialog[open] button[type=submit]').click()
    await expect(page.locator('dialog[open]')).toHaveCount(0)
    await expect(page.locator('tbody')).toContainText(doiDuocTra.tenDoi)

    // Tra lại chính mã đó: đã trong sổ nên chỉ còn badge, không còn nút thêm (tránh trùng bản ghi).
    await page.click('button:has-text("Thêm trận đấu")')
    await page.click('#doiThuId')
    await page.fill(O_MA, doiDuocTra.maDoi)
    await page.click(NUT_TRA)
    await expect(page.locator('text=Đã có trong sổ')).toHaveCount(1)
    await expect(page.locator('button:has-text("Thêm vào sổ")')).toHaveCount(0)
  })

  test('mã không tồn tại báo lỗi NGAY TRONG dropdown', async ({ page, request }) => {
    // Lỗi thật: CanhBaoLoi đặt dưới select, mà dropdown là lớp nổi che phần dưới → người dùng
    // bấm "Tra" và không thấy gì, tưởng nút hỏng. Thông báo phải nằm trong vùng dropdown.
    await vaoHeThong(page, request, 'tra-truot')

    await page.goto('/lich-thi-dau')
    await page.click('button:has-text("Thêm trận đấu")')
    await page.click('#doiThuId')

    await page.fill(O_MA, 'ZZZZZZZ')
    await page.click(NUT_TRA)

    const canhBao = page.locator('text=Không có CLB nào dùng mã này')
    await expect(canhBao).toBeVisible()

    // Nằm TRONG dropdown, không phải dưới nó: so hộp bao của cảnh báo với hộp của ô tra mã.
    const hopCanhBao = await canhBao.boundingBox()
    const hopO = await page.locator(O_MA).boundingBox()
    expect(hopCanhBao).not.toBeNull()
    expect(hopO).not.toBeNull()
    // Cách ô tra không quá 60px — đủ gần để mắt bắt được cùng lúc.
    expect(hopCanhBao!.y - (hopO!.y + hopO!.height)).toBeLessThan(60)
  })

  test('Enter trong ô mã không lưu trận', async ({ page, request }) => {
    // Người dùng đang tra mã, chưa điền xong trận. Enter theo phản xạ mà submit form thì họ
    // lưu một trận thiếu dữ liệu mà không cố ý.
    await vaoHeThong(page, request, 'enter-o-ma')

    await page.goto('/lich-thi-dau')
    await page.click('button:has-text("Thêm trận đấu")')
    await page.click('#doiThuId')

    await page.fill(O_MA, 'ZZZZZZZ')
    await page.press(O_MA, 'Enter')

    // Modal phải CÒN MỞ.
    await expect(page.locator('dialog[open]')).toHaveCount(1)
  })

  test('không tra được CLB của chính mình', async ({ page, request }) => {
    // Đá với chính mình là vô nghĩa; quan trọng hơn là nó phải im lặng giống mã không tồn tại,
    // đừng để phân biệt được mã nào là thật (xem TraCuuClbTests ở backend).
    const clb = await vaoHeThong(page, request, 'tra-chinh-minh')

    await page.goto('/lich-thi-dau')
    await page.click('button:has-text("Thêm trận đấu")')
    await page.click('#doiThuId')

    await page.fill(O_MA, clb.maDoi)
    await page.click(NUT_TRA)

    await expect(page.locator('text=Không có CLB nào dùng mã này')).toBeVisible()
    await expect(page.locator('button:has-text("Thêm vào sổ")')).toHaveCount(0)
  })
})
