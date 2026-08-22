import { expect, test, type Page } from '@playwright/test'
import { dongDropdown, vaoHeThong } from './tro-giup'

/**
 * FR-07 → FR-11 — lịch thi đấu, đội hình, sơ đồ, đánh giá, video.
 *
 * Nhiều test ở đây canh lỗi đã xảy ra thật và lọt qua toàn bộ test backend: lịch cắt tên đối
 * thủ, kéo-thả cầu thủ đứng yên, form không submit vì `min`+`step` xung khắc.
 */

/** Tạo một trận đã diễn ra, trả về ngày để tìm lại trong bảng. */
async function taoTran(page: Page, ngay: string, gio = '15:00') {
  await page.goto('/lich-thi-dau')
  await page.click('button:has-text("Thêm trận đấu")')
  await page.fill('#thoiGian', `${ngay}T${gio}`)
  await page.locator('dialog[open] button[type=submit]').click()
  await expect(page.locator('dialog[open]')).toHaveCount(0)
}

/** Mở màn chi tiết của trận đầu bảng. */
async function moChiTiet(page: Page) {
  await page.locator('tbody tr').first().locator('button').first().click()
  await page.waitForURL(/lich-thi-dau\/[0-9a-f-]{10,}/)
}

test.describe('Lịch thi đấu', () => {
  test('thêm trận và thấy nó trong bảng', async ({ page, request }) => {
    await vaoHeThong(page, request, 'them-tran')
    await taoTran(page, '2027-09-12')

    await expect(page.locator('tbody tr')).toHaveCount(1)
    await expect(page.locator('tbody')).toContainText('12/09/2027')
  })

  test('sắp xếp bảng đổi thứ tự và về trang 1', async ({ page, request }) => {
    await vaoHeThong(page, request, 'sort')
    for (const ngay of ['2027-10-05', '2027-10-20', '2027-10-12']) await taoTran(page, ngay)

    await page.goto('/lich-thi-dau')
    const cotThoiGian = () => page.locator('tbody tr td:first-child').allInnerTexts()

    // Mặc định: mới nhất lên đầu.
    const macDinh = await cotThoiGian()
    expect(macDinh[0]).toContain('20/10')

    // Bấm một lần → đảo chiều.
    await page.locator('thead button', { hasText: 'Thời gian' }).click()
    await expect
      .poll(async () => (await cotThoiGian())[0], { timeout: 10_000 })
      .toContain('05/10')

    // aria-sort để trình đọc màn hình biết trạng thái.
    await expect(page.locator('thead th[aria-sort="ascending"]')).toHaveCount(1)
  })

  test('chế độ Lịch hiện TÊN đối thủ, không chỉ chấm màu', async ({ page, request }) => {
    // Lỗi thật: ô ngày cao 4.25rem cắt mất viên thứ ba, và tên bị ellipsis thành "FC Hà…".
    await vaoHeThong(page, request, 'lich')

    // Cần một đối thủ để trận có tên.
    await page.goto('/doi-thu')
    await page.click('button:has-text("Thêm đối thủ")')
    await page.fill('#tenDoi', 'FC Kiểm Lịch')
    await page.locator('dialog[open] button[type=submit]').click()
    await expect(page.locator('dialog[open]')).toHaveCount(0)

    await page.goto('/lich-thi-dau')
    await page.click('button:has-text("Thêm trận đấu")')
    await page.fill('#thoiGian', '2027-11-08T15:00')
    await page.click('#doiThuId')
    await page.locator('ul[role=listbox] button', { hasText: 'FC Kiểm Lịch' }).click()
    await page.locator('dialog[open] button[type=submit]').click()
    await expect(page.locator('dialog[open]')).toHaveCount(0)

    await page.click('button:has-text("Lịch")')

    // Chuyển tới tháng 11/2027.
    for (let i = 0; i < 30; i++) {
      const nhan = await page.locator('span.min-w-40').innerText()
      if (nhan.includes('11 / 2027')) break
      await page.click('button[aria-label="Tháng sau"]')
    }

    const ten = page.locator('.sr-tran-ten')
    await expect(ten.first()).toBeVisible()
    await expect(ten.first()).toHaveText('FC Kiểm Lịch')

    // Tên KHÔNG bị cắt — scrollWidth vượt clientWidth là dấu hiệu ellipsis.
    const biCat = await ten.first().evaluate((n) => n.scrollWidth > n.clientWidth + 1)
    expect(biCat, 'Tên đối thủ bị cắt trong ô lịch').toBeFalsy()
  })

  test('năm tab ở màn chi tiết trận, đúng thứ tự', async ({ page, request }) => {
    // Thứ tự phản ánh trình tự làm việc thật: xem thông tin → mời đăng ký → xếp đội hình từ
    // người đã nhận → dán video → đánh giá. "Đăng ký" thêm 21/08 (FR-19) và phải nằm TRƯỚC
    // đội hình vì đăng ký xảy ra trước khi xếp đội.
    await vaoHeThong(page, request, 'tabs')
    await taoTran(page, '2027-12-01')
    await moChiTiet(page)

    const tabs = await page.locator('.border-b button').allInnerTexts()
    expect(tabs.filter(Boolean)).toEqual([
      'Thông tin chung',
      'Đăng ký',
      'Đội hình & Sơ đồ',
      'Video sau trận',
      'Đánh giá sau trận',
    ])
  })

  test('kéo cầu thủ trên sân thì áo di chuyển', async ({ page, request }) => {
    // Lỗi thật: pointer capture đặt trên thẻ áo khiến pointermove không nổi bọt lên sân,
    // kéo 90px mà toạ độ không đổi một chút nào.
    await vaoHeThong(page, request, 'keo-tha')

    await page.goto('/quan-tri/cau-thu')
    for (const ten of ['Cầu Thủ Kéo A', 'Cầu Thủ Kéo B']) {
      await page.click('button:has-text("Thêm cầu thủ")')
      await page.fill('#hoTen', ten)
      await page.locator('dialog[open] button[type=submit]').click()
      await expect(page.locator('dialog[open]')).toHaveCount(0)
    }

    await taoTran(page, '2028-01-10')
    await moChiTiet(page)
    await page.click('button:has-text("Đội hình & Sơ đồ")')

    // Chọn thành viên.
    await page.click('#thanhVien')
    await page.locator('ul[role=listbox] button').first().click()
    await page.locator('ul[role=listbox] button').nth(1).click()
    await dongDropdown(page, '#thanhVien')
    await page.click('button:has-text("Lưu thành viên")')

    // Áp sơ đồ dựng sẵn.
    await page.locator('button', { hasText: /^4-4-2$/ }).click()
    const ao = page.locator('[data-testid=san] > button.z-20')
    await expect(ao.first()).toBeVisible()

    // Cuộn sân vào giữa khung nhìn TRƯỚC khi kéo.
    //
    // Ở màn hình 800px cao, sân (62vh) nằm dưới header + tab + hai hàng nút nên áo hàng dưới
    // rơi ra ngoài viewport — chuột không tới được và test đỏ dù kéo-thả hoàn toàn đúng.
    await page.locator('[data-testid=san]').scrollIntoViewIfNeeded()
    await ao.first().scrollIntoViewIfNeeded()

    const truoc = await ao.first().boundingBox()
    expect(truoc).not.toBeNull()

    // Kéo bằng pointer events chuỗi: mouse.* của Playwright không sinh pointerdown/move đầy đủ
    // cho phần tử dùng setPointerCapture ở phần tử KHÁC (sân, không phải áo).
    await page.mouse.move(truoc!.x + truoc!.width / 2, truoc!.y + 8)
    await page.mouse.down()
    // Nhiều bước nhỏ: capture ở sân cần pointermove thật sự đi qua vùng sân.
    for (let i = 1; i <= 12; i++) {
      await page.mouse.move(truoc!.x + (90 * i) / 12, truoc!.y - (110 * i) / 12)
    }
    await page.mouse.up()
    await page.waitForTimeout(300)

    const sau = await ao.first().boundingBox()
    const dich = Math.hypot(sau!.x - truoc!.x, sau!.y - truoc!.y)
    expect(dich, 'Áo không di chuyển khi kéo').toBeGreaterThan(20)
  })

  test('thao tác xoá hết phải xác nhận và Huỷ giữ nguyên', async ({ page, request }) => {
    await vaoHeThong(page, request, 'xac-nhan')

    await page.goto('/quan-tri/cau-thu')
    await page.click('button:has-text("Thêm cầu thủ")')
    await page.fill('#hoTen', 'Cầu Thủ Xác Nhận')
    await page.locator('dialog[open] button[type=submit]').click()
    await expect(page.locator('dialog[open]')).toHaveCount(0)

    await taoTran(page, '2028-02-14')
    await moChiTiet(page)
    await page.click('button:has-text("Đội hình & Sơ đồ")')

    await page.click('#thanhVien')
    await page.locator('ul[role=listbox] button').first().click()
    await dongDropdown(page, '#thanhVien')
    await page.click('button:has-text("Lưu thành viên")')

    await page.locator('button', { hasText: /^4-4-2$/ }).click()
    const ao = page.locator('[data-testid=san] > button.z-20')
    const soTruoc = await ao.count()
    expect(soTruoc).toBeGreaterThan(0)

    await page.click('button:has-text("Xóa hết")')

    // Hộp xác nhận phải nói RÕ mất gì, không phải "Bạn có chắc không?".
    const hop = page.locator('dialog[open]')
    await expect(hop).toBeVisible()
    await expect(hop).toContainText(/Sẽ bỏ \d+ cầu thủ/)

    await hop.locator('button:has-text("Hủy")').click()
    await expect(ao).toHaveCount(soTruoc)
  })

  test('thêm nhiều link video và thấy trong thư viện', async ({ page, request }) => {
    await vaoHeThong(page, request, 'video')
    await taoTran(page, '2028-03-20')
    await moChiTiet(page)

    await page.click('button:has-text("Video sau trận")')
    await page.click('button:has-text("Thêm link")')
    await page.fill('#vten_0', 'Hiệp 1')
    await page.fill('#vurl_0', 'https://youtube.com/watch?v=e2e1')
    await page.fill('#vmota_0', 'quay từ khán đài A')

    await page.click('button:has-text("Thêm link")')
    await page.fill('#vten_1', 'Highlight')
    await page.fill('#vurl_1', 'https://drive.google.com/e2e2')

    await page.click('button:has-text("Lưu video")')
    await expect(page.locator('text=Đã lưu')).toBeVisible()

    // Thư viện đọc THẲNG từ VIDEO_TRAN — không có bản sao nào để lệch.
    await page.goto('/thu-vien-video')
    await expect(page.locator('main')).toContainText('Hiệp 1')
    await expect(page.locator('main')).toContainText('Highlight')
    await expect(page.locator('main')).toContainText('quay từ khán đài A')
  })
})
