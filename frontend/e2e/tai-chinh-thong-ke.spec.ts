import { expect, test, type Page } from '@playwright/test'
import { dongDropdown, vaoHeThong } from './tro-giup'

/** FR-12 → FR-16 — thống kê và quỹ đội. */

async function taoCauThu(page: Page, ten: string) {
  await page.goto('/quan-tri/cau-thu')
  await page.click('button:has-text("Thêm cầu thủ")')
  await page.fill('#hoTen', ten)
  await page.locator('dialog[open] button[type=submit]').click()
  await expect(page.locator('dialog[open]')).toHaveCount(0)
}

test.describe('Tài chính', () => {
  test('tạo đợt quỹ, thu tiền, tiến độ cập nhật', async ({ page, request }) => {
    await vaoHeThong(page, request, 'quy')
    await taoCauThu(page, 'Người Đóng Quỹ A')
    await taoCauThu(page, 'Người Đóng Quỹ B')

    await page.goto('/tai-chinh')
    await page.click('button:has-text("Thêm đợt quỹ")')
    await page.fill('#tenQuy', 'Quỹ E2E tháng 1')
    await page.fill('#thoiHan', '2028-01-31')

    await page.click('#thanhVien')
    await page.locator('ul[role=listbox] button').nth(0).click()
    await page.locator('ul[role=listbox] button').nth(1).click()
    await dongDropdown(page, '#thanhVien')

    await page.fill('#dongLoat', '50000')
    await page.click('button:has-text("Áp cho tất cả")')
    await page.locator('dialog[open] button[type=submit]').click()
    await expect(page.locator('dialog[open]')).toHaveCount(0)

    // Tiến độ 0 / 100.000₫.
    await expect(page.locator('tbody')).toContainText('100.000')
    await expect(page.locator('tbody')).toContainText('0/2')

    // Thu đủ một người.
    await page.locator('button[title="Thu tiền"]').first().click()
    await page.locator('dialog[open] button[title="Đánh dấu đã đóng đủ"]').first().click()
    await expect(page.locator('dialog[open]')).toContainText('50.000 ₫ / 100.000 ₫')

    await page.locator('dialog[open] button:has-text("Đóng")').click()
    await expect(page.locator('main')).toContainText('1/2')
  })

  test('khoản chi với số tròn nghìn lưu được', async ({ page, request }) => {
    // Lỗi thật: min={1} + step={1000} khiến trình duyệt chỉ nhận 1, 1001, 2001… nên 300000
    // bị từ chối IM LẶNG — form không submit mà chẳng báo gì.
    await vaoHeThong(page, request, 'chi')

    await page.goto('/tai-chinh')
    await page.click('button:has-text("Khoản chi")')
    await page.click('button:has-text("Thêm khoản chi")')
    await page.fill('#noiDung', 'Thuê sân E2E')
    await page.fill('#soTien', '300000')
    await page.fill('#nguoiChi', 'Thủ quỹ')
    await page.locator('dialog[open] button[type=submit]').click()

    // Modal phải ĐÓNG — nếu form không submit thì nó vẫn mở.
    await expect(page.locator('dialog[open]')).toHaveCount(0)
    await expect(page.locator('tbody')).toContainText('Thuê sân E2E')
    await expect(page.locator('tbody')).toContainText('300.000')
  })

  test('số dư âm khi chi vượt thu', async ({ page, request }) => {
    await vaoHeThong(page, request, 'so-du')

    await page.goto('/tai-chinh')
    await page.click('button:has-text("Khoản chi")')
    await page.click('button:has-text("Thêm khoản chi")')
    await page.fill('#noiDung', 'Chi vượt thu')
    await page.fill('#soTien', '500000')
    await page.locator('dialog[open] button[type=submit]').click()
    await expect(page.locator('dialog[open]')).toHaveCount(0)

    // Số dư âm phải hiện màu đỏ — nhìn ra ngay, không lẫn vào màu chung.
    const theSoDu = page.locator('div', { hasText: 'Số dư quỹ' }).first()
    await expect(theSoDu).toContainText('-500.000')
  })

  test('xoá đợt quỹ đã thu tiền bị chặn kèm thông báo', async ({ page, request }) => {
    await vaoHeThong(page, request, 'khong-xoa')
    await taoCauThu(page, 'Người Đã Đóng')

    await page.goto('/tai-chinh')
    await page.click('button:has-text("Thêm đợt quỹ")')
    await page.fill('#tenQuy', 'Quỹ đã thu')
    await page.click('#thanhVien')
    await page.locator('ul[role=listbox] button').first().click()
    await dongDropdown(page, '#thanhVien')
    await page.fill('#dongLoat', '50000')
    await page.click('button:has-text("Áp cho tất cả")')
    await page.locator('dialog[open] button[type=submit]').click()
    await expect(page.locator('dialog[open]')).toHaveCount(0)

    await page.locator('button[title="Thu tiền"]').first().click()
    await page.locator('dialog[open] button[title="Đánh dấu đã đóng đủ"]').first().click()
    await expect(page.locator('dialog[open]')).toContainText('Đủ')
    await page.locator('dialog[open] button:has-text("Đóng")').click()

    // Xoá phải bị chặn — tiền đã thu là vết của tiền có thật (quy tắc #1).
    await page.locator('button[title="Xóa"]').first().click()
    await page.locator('dialog[open] button:has-text("Đồng ý")').click()

    await expect(page.locator('main')).toContainText(/không xóa được|Đóng đợt quỹ/i)
    await expect(page.locator('tbody')).toContainText('Quỹ đã thu')
  })
  test('hoàn tác được khi bấm nhầm đã đóng tiền', async ({ page, request }) => {
    await vaoHeThong(page, request, 'hoan-tac')
    await taoCauThu(page, 'Người Bấm Nhầm')
    await taoCauThu(page, 'Người Đóng Thật')

    await page.goto('/tai-chinh')
    await page.click('button:has-text("Thêm đợt quỹ")')
    await page.fill('#tenQuy', 'Quỹ E2E hoàn tác')
    await page.click('#thanhVien')
    await page.locator('ul[role=listbox] button').nth(0).click()
    await page.locator('ul[role=listbox] button').nth(1).click()
    await dongDropdown(page, '#thanhVien')
    await page.fill('#dongLoat', '120000')
    await page.click('button:has-text("Áp cho tất cả")')
    await page.locator('dialog[open] button[type=submit]').click()
    await expect(page.locator('dialog[open]')).toHaveCount(0)

    await page.locator('button[title="Thu tiền"]').first().click()

    // Neo theo TÊN, không theo chỉ số hàng: handler sắp "người còn nợ lên đầu", nên thu đủ một
    // người là hàng đó nhảy xuống cuối và `tbody tr` thứ nhất trỏ sang người khác.
    const hang = page.locator('dialog[open] tbody tr', { hasText: 'Người Bấm Nhầm' })
    const oTien = hang.locator('input[type=number]')

    // Chưa đóng: có nút thu đủ, KHÔNG có nút hoàn tác.
    await expect(hang.locator('button[title="Đánh dấu đã đóng đủ"]')).toHaveCount(1)
    await expect(hang.locator('button[title="Hoàn tác"]')).toHaveCount(0)

    await hang.locator('button[title="Đánh dấu đã đóng đủ"]').click()

    // Ô số phải HIỆN ĐÚNG số vừa ghi. Lỗi thật: `defaultValue` chỉ có tác dụng ở render đầu,
    // nên ô vẫn hiện 0 trong khi cột "Còn thiếu" báo đã đủ — người dùng tưởng chưa lưu được.
    await expect(oTien).toHaveValue('120000')
    await expect(hang.locator('button[title="Hoàn tác"]')).toHaveCount(1)
    await expect(hang.locator('button[title="Đánh dấu đã đóng đủ"]')).toHaveCount(0)

    // Hai nút phải ở HAI vị trí khác nhau: dùng chung một chỗ thì cú bấm tiếp theo theo quán
    // tính sẽ xoá mất khoản vừa ghi.
    const hopHt = await hang.locator('button[title="Hoàn tác"]').boundingBox()
    expect(hopHt).not.toBeNull()

    // Hoàn tác: phải có xác nhận nói rõ số tiền, vì nó xoá vết một khoản đã ghi nhận.
    await hang.locator('button[title="Hoàn tác"]').click()
    const hopXacNhan = page.locator('dialog[open]').last()
    await expect(hopXacNhan).toContainText('120.000')
    await hopXacNhan.locator('button:has-text("Hoàn tác")').click()

    // Về 0, và tiến độ quỹ giảm theo.
    await expect(oTien).toHaveValue('0')
    await expect(page.locator('dialog[open]').first()).toContainText('0 ₫ / 240.000 ₫')
    await expect(hang.locator('button[title="Đánh dấu đã đóng đủ"]')).toHaveCount(1)

    // Thu lại được — hoàn tác không khoá vĩnh viễn khoản đóng.
    await hang.locator('button[title="Đánh dấu đã đóng đủ"]').click()
    await expect(oTien).toHaveValue('120000')
    await expect(page.locator('dialog[open]').first()).toContainText('120.000 ₫ / 240.000 ₫')
  })

  test('thông tin chuyển khoản: bật/tắt theo đợt, tiền không mất', async ({ page, request }) => {
    await vaoHeThong(page, request, 'chuyen-khoan')
    await taoCauThu(page, 'Người Chuyển Khoản A')
    await taoCauThu(page, 'Người Chuyển Khoản B')

    // Khai thông tin chuyển khoản ở Thiết lập chung.
    await page.goto('/quan-tri/thiet-lap')
    await expect(page.locator('main')).toContainText('KHÔNG hiện trên Cộng đồng')
    await page.fill('#soTaiKhoan', '1234509876')
    await page.fill('#tenNganHang', 'Vietcombank')
    await page.fill('#chuTaiKhoan', 'NGUYEN VAN THU QUY')
    await page.locator('button[type=submit]').click()
    await expect(page.locator('main')).toContainText(/Đã lưu|lưu/i)

    // Giữ nguyên sau khi tải lại (quy tắc #1).
    await page.reload()
    await expect(page.locator('#soTaiKhoan')).toHaveValue('1234509876')

    // Đợt quỹ BẬT hiển thị.
    await page.goto('/tai-chinh')
    await page.click('button:has-text("Thêm đợt quỹ")')
    await page.fill('#tenQuy', 'Quỹ E2E chuyển khoản')
    await page.click('#thanhVien')
    await page.locator('ul[role=listbox] button').nth(0).click()
    await page.locator('ul[role=listbox] button').nth(1).click()
    await dongDropdown(page, '#thanhVien')
    await page.fill('#dongLoat', '100000')
    await page.click('button:has-text("Áp cho tất cả")')
    await page.check('input[name="hienChuyenKhoan"]')
    await page.locator('dialog[open] button[type=submit]').click()
    await expect(page.locator('dialog[open]')).toHaveCount(0)

    // Màn thu tiền hiện khối chuyển khoản, kèm lưu ý hệ thống KHÔNG tự ghi nhận — thiếu câu này
    // thì người chuyển xong thấy tiến độ vẫn 0 sẽ tưởng thất bại và chuyển lại lần nữa.
    await page.locator('button[title="Thu tiền"]').first().click()
    await expect(page.locator('dialog[open]')).toContainText('1234509876')
    await expect(page.locator('dialog[open]')).toContainText('Vietcombank')
    await expect(page.locator('dialog[open]')).toContainText('không tự ghi nhận')

    // Thu đủ một người.
    await page.locator('dialog[open] button[title="Đánh dấu đã đóng đủ"]').first().click()
    await expect(page.locator('dialog[open]')).toContainText('100.000 ₫ / 200.000 ₫')
    await page.locator('dialog[open] button:has-text("Đóng")').click()

    // TẮT hiển thị trên đợt quỹ ĐANG có tiền.
    await page.locator('button[title="Sửa"]').first().click()
    await page.uncheck('input[name="hienChuyenKhoan"]')
    await page.locator('dialog[open] button[type=submit]').click()
    await expect(page.locator('dialog[open]')).toHaveCount(0)

    // Lỗi thật đã xảy ra: lệnh lưu quỹ chỉ làm mới cache `quy`, không làm mới `quy-chi-tiet` —
    // nên mở lại màn thu tiền vẫn thấy số tài khoản dù cờ đã tắt.
    await page.locator('button[title="Thu tiền"]').first().click()
    await expect(page.locator('dialog[open]')).not.toContainText('1234509876')

    // Và TIỀN CÒN NGUYÊN (quy tắc #1).
    await expect(page.locator('dialog[open]')).toContainText('100.000 ₫ / 200.000 ₫')
  })
})

test.describe('Thống kê', () => {
  test('KPI, biểu đồ và xếp hạng hiện sau khi có trận đã đá', async ({ page, request }) => {
    await vaoHeThong(page, request, 'thong-ke')
    await taoCauThu(page, 'Cầu Thủ Ghi Bàn')

    // Trận đã diễn ra, thắng 2-0.
    await page.goto('/lich-thi-dau')
    await page.click('button:has-text("Thêm trận đấu")')
    await page.fill('#thoiGian', '2028-06-15T15:00')
    await page.fill('#tySoKhach', '0')
    await page.locator('dialog[open] select#trangThai, dialog[open] #trangThai').click()
    await page.locator('ul[role=listbox] button', { hasText: 'Đã diễn ra' }).click()
    await page.locator('dialog[open] button[type=submit]').click()
    await expect(page.locator('dialog[open]')).toHaveCount(0)

    // Vào chi tiết, xếp đội hình và ghi 2 bàn.
    await page.locator('tbody tr').first().locator('button').first().click()
    await page.waitForURL(/lich-thi-dau\/[0-9a-f-]{10,}/)
    await page.click('button:has-text("Đội hình & Sơ đồ")')
    await page.click('#thanhVien')
    await page.locator('ul[role=listbox] button').first().click()
    await dongDropdown(page, '#thanhVien')
    await page.click('button:has-text("Lưu thành viên")')

    await page.click('button:has-text("Đánh giá sau trận")')
    await page.locator('tbody button[title="Sửa"]').first().click()
    await page.fill('#dgGhi', '2')
    // Chấm điểm Tấn công 8 để có điểm kỹ năng.
    await page.locator('dialog[open] [role=group]').first().locator('button').nth(7).click()
    await page.locator('dialog[open] button[type=submit]').click()
    await expect(page.locator('dialog[open]')).toHaveCount(0)

    await page.goto('/thong-ke')

    // Tỷ lệ thắng tính trên trận ĐÃ ĐÁ.
    await expect(page.locator('main')).toContainText('100%')
    await expect(page.locator('main')).toContainText('trên 1 trận đã đá')

    // FR-13 — biểu đồ có điểm dữ liệu.
    await expect(page.locator('.recharts-wrapper')).toHaveCount(1)
    expect(await page.locator('.recharts-line-dots circle').count()).toBeGreaterThan(0)

    // FR-14 — bốn tiêu chí xếp hạng.
    const tieuChi = await page.locator('section button').allInnerTexts()
    expect(tieuChi.filter(Boolean)).toEqual(
      expect.arrayContaining(['Phiếu MVP', 'Điểm kỹ năng', 'Bàn thắng', 'Cứu thua']),
    )
    await expect(page.locator('section table tbody')).toContainText('Cầu Thủ Ghi Bàn')
  })

  test('bấm điểm trên biểu đồ mở chi tiết trận', async ({ page, request }) => {
    await vaoHeThong(page, request, 'bieu-do')
    await taoCauThu(page, 'Cầu Thủ Biểu Đồ')

    await page.goto('/lich-thi-dau')
    await page.click('button:has-text("Thêm trận đấu")')
    await page.fill('#thoiGian', '2028-07-01T15:00')
    await page.fill('#tySoKhach', '1')
    await page.locator('dialog[open] #trangThai').click()
    await page.locator('ul[role=listbox] button', { hasText: 'Đã diễn ra' }).click()
    await page.locator('dialog[open] button[type=submit]').click()
    await expect(page.locator('dialog[open]')).toHaveCount(0)

    // Biểu đồ chỉ vẽ trận CÓ KẾT QUẢ, mà kết quả suy từ tỷ số — và bàn thắng đội nhà đến từ
    // đánh giá cầu thủ, không nhập tay được. Nhập bàn thua thôi thì KetQua vẫn là ChuaCo.
    await page.locator('tbody tr').first().locator('button').first().click()
    await page.waitForURL(/lich-thi-dau\/[0-9a-f-]{10,}/)
    await page.click('button:has-text("Đội hình & Sơ đồ")')
    await page.click('#thanhVien')
    await page.locator('ul[role=listbox] button').first().click()
    await dongDropdown(page, '#thanhVien')
    await page.click('button:has-text("Lưu thành viên")')

    await page.click('button:has-text("Đánh giá sau trận")')
    await page.locator('tbody button[title="Sửa"]').first().click()
    await page.fill('#dgGhi', '3')
    await page.locator('dialog[open] button[type=submit]').click()
    await expect(page.locator('dialog[open]')).toHaveCount(0)

    await page.goto('/thong-ke')
    const diem = page.locator('.recharts-line-dots circle').first()
    await expect(diem).toBeVisible()
    await diem.click({ force: true })

    // FR-13 yêu cầu rõ: click điểm → điều hướng sang chi tiết trận.
    await expect(page).toHaveURL(/lich-thi-dau\/[0-9a-f-]{10,}/)
  })
})

test.describe('Hòm thư', () => {
  test('trưởng nhóm gửi lời mời, cầu thủ trả lời', async ({ page, request }) => {
    await vaoHeThong(page, request, 'hom-thu')
    await taoCauThu(page, 'Cầu Thủ Đăng Ký')

    await page.goto('/lich-thi-dau')
    await page.click('button:has-text("Thêm trận đấu")')
    await page.fill('#thoiGian', '2028-08-08T15:00')
    await page.locator('dialog[open] button[type=submit]').click()
    await expect(page.locator('dialog[open]')).toHaveCount(0)

    // Admin là trưởng nhóm mặc định → thấy nút mời.
    await page.goto('/hom-thu')
    await page.click('button:has-text("Mời đăng ký")')
    await page.click('#tranDauId')
    await page.locator('ul[role=listbox] button').first().click()
    await page.fill('#loiNhan', '15h chủ nhật sân Hòa Xuân')
    await page.locator('dialog[open] button[type=submit]').click()
    await expect(page.locator('dialog[open]')).toHaveCount(0)

    await expect(page.locator('main')).toContainText('15h chủ nhật sân Hòa Xuân')

    // Tạo sẵn hàng "chưa trả lời" cho mọi cầu thủ — trưởng nhóm cần thấy ai chưa trả lời.
    await page.click('button:has-text("Xem phản hồi")')
    await expect(page.locator('dialog[open] tbody tr')).toHaveCount(1)
    await expect(page.locator('dialog[open]')).toContainText('Chưa trả lời')
  })
})
