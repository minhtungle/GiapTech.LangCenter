import { expect, test, type Page, type APIRequestContext } from '@playwright/test'
import { MAT_KHAU_MOI, taoClb, vaoHeThong } from './tro-giup'

/**
 * CỘNG ĐỒNG + lời mời thách đấu.
 *
 * Mọi CLB đăng ký đều lên cộng đồng kèm thành tích (quyết định của chủ sản phẩm), nên bộ test này
 * cũng canh phía "không lộ": liên hệ KHÔNG hiện trong cộng đồng, chỉ hiện sau khi hai bên đồng ý.
 */

/** Đăng nhập một CLB khác trong cùng phiên browser — cần cho luồng hai chiều. */
async function dangNhapClb(page: Page, maDoi: string, matKhauDau: string) {
  await page.goto('/dang-nhap')
  await page.fill('#maDoi', maDoi)
  await page.fill('#username', 'admin')
  await page.fill('#matKhau', matKhauDau)
  await page.click('button[type=submit]')
  await page.waitForURL(/doi-mat-khau/, { timeout: 15_000 })
  await page.fill('#matKhauCu', matKhauDau)
  await page.fill('#matKhauMoi', MAT_KHAU_MOI)
  const oXacNhan = page.locator('#xacNhan')
  if (await oXacNhan.count()) await oXacNhan.fill(MAT_KHAU_MOI)
  await page.locator('button[type=submit]').click()
  await page.waitForURL((u) => u.pathname === '/', { timeout: 15_000 })
}

/** Khai khu vực + liên hệ qua API — nhanh hơn đi qua form thiết lập cho mỗi test. */
async function khaiThongTinSan(
  request: APIRequestContext,
  token: string,
  tenDoi: string,
  khuVuc: string,
  lienHe: string,
) {
  const res = await request.put('/api/v1/thiet-lap', {
    headers: { Authorization: `Bearer ${token}` },
    data: {
      tenDoi,
      tenVietTat: null,
      ngayThanhLap: null,
      moTa: 'Đội E2E, tìm đối cân sức.',
      logoUrl: null,
      anhBiaUrl: null,
      mauAo: [],
      khuVuc,
      sanNha: 'Sân E2E',
      lienHeCongKhai: lienHe,
    },
  })
  expect(res.ok()).toBeTruthy()
}

async function layToken(request: APIRequestContext, maDoi: string, matKhau: string) {
  const res = await request.post('/api/v1/auth/dang-nhap', {
    data: { maDoi, username: 'admin', matKhau },
  })
  return (await res.json()).accessToken as string
}

test.describe('Cộng đồng', () => {
  test('tìm được CLB khác và KHÔNG thấy số điện thoại của họ', async ({ page, request }) => {
    const doiKia = await taoClb(request, 'tren-san')
    // Đổi mật khẩu để dùng API: CLB mới bị middleware buộc đổi trước khi làm gì khác.
    const tokenDau = await layToken(request, doiKia.maDoi, doiKia.matKhau)
    await request.post('/api/v1/auth/doi-mat-khau', {
      headers: { Authorization: `Bearer ${tokenDau}` },
      data: { matKhauCu: doiKia.matKhau, matKhauMoi: MAT_KHAU_MOI },
    })
    const token = await layToken(request, doiKia.maDoi, MAT_KHAU_MOI)
    await khaiThongTinSan(request, token, doiKia.tenDoi, 'Liên Chiểu, Đà Nẵng', '0911222333')

    await vaoHeThong(page, request, 'di-xem-san')
    await page.goto('/cong-dong')

    // Nói rõ ngay đầu trang là dữ liệu công khai — người dùng cần biết đội mình cũng đang hiện.
    await expect(page.locator('main')).toContainText('Đội của bạn cũng đang hiện')

    await page.fill('#timClb', doiKia.tenDoi)
    await page.press('#timClb', 'Enter')

    // Kiểm trên `main`, không neo vào một `div` cụ thể: `locator('div').last()` chọn div lồng
    // sâu nhất khớp mã đội, mà nó chỉ chứa mã — không chứa khu vực nằm ở div anh em.
    await expect(page.locator('main')).toContainText(doiKia.tenDoi)
    await expect(page.locator('main')).toContainText('Liên Chiểu, Đà Nẵng')
    await expect(page.locator('main')).toContainText('Chưa có trận nào')

    // Số điện thoại KHÔNG được có trong cộng đồng: một lần gọi API là thu số của mọi CLB → cửa spam.
    await expect(page.locator('main')).not.toContainText('0911222333')
  })

  test('gửi lời mời rồi bên kia đồng ý, trận vào lịch CẢ HAI đội', async ({
    page,
    request,
    browser,
  }) => {
    const benNhan = await taoClb(request, 'ben-nhan')
    const benGui = await vaoHeThong(page, request, 'ben-gui')

    // Gửi lời mời từ sàn.
    await page.goto('/cong-dong')
    await page.fill('#timClb', benNhan.tenDoi)
    await page.press('#timClb', 'Enter')
    await page.locator('button:has-text("Gửi lời mời")').first().click()

    await page.fill('#moiThoiGian', '2028-11-11T15:00')
    await page.fill('#moiDiaDiem', 'Sân E2E Thách Đấu')
    await page.fill('#moiLoiNhan', 'Chiều CN đá 7 người nhé')
    await page.locator('dialog[open] button[type=submit]').click()
    await expect(page.locator('dialog[open]')).toHaveCount(0)

    // Thẻ đổi sang "đang chờ" — không cho gửi trùng.
    await expect(page.locator('main')).toContainText('Đang chờ họ trả lời')
    await expect(page.locator('button:has-text("Gửi lời mời")')).toHaveCount(0)

    // Bên nhận, trong ngữ cảnh browser riêng (token khác nhau).
    const ctx = await browser.newContext()
    const trangNhan = await ctx.newPage()
    await dangNhapClb(trangNhan, benNhan.maDoi, benNhan.matKhau)

    await trangNhan.goto('/hom-thu')
    await expect(trangNhan.locator('main')).toContainText(benGui.tenDoi)
    await expect(trangNhan.locator('main')).toContainText('Chiều CN đá 7 người nhé')

    // "Đồng ý" khớp cả nút trong hộp xác nhận, nên neo vào khối lời mời thách đấu.
    const khoiThachDau = trangNhan.locator('section', { hasText: 'Lời mời thách đấu' })
    await khoiThachDau.locator('button:has-text("Đồng ý")').first().click()

    // Hộp xác nhận phải NÓI TRƯỚC là sẽ tạo trận ở cả hai lịch: người dùng bấm đồng ý rồi
    // thấy trận tự mọc trong lịch sẽ tưởng hệ thống làm sai.
    await expect(trangNhan.locator('dialog[open]')).toContainText('lịch của')
    await trangNhan.fill('dialog[open] textarea', 'OK, gặp ở sân')
    await trangNhan.locator('dialog[open] button[type=submit]').click()
    await expect(trangNhan.locator('dialog[open]')).toHaveCount(0)

    await expect(trangNhan.locator('main')).toContainText('Đã đồng ý')

    // Trận vào lịch bên NHẬN.
    await trangNhan.goto('/lich-thi-dau')
    await expect(trangNhan.locator('tbody')).toContainText(benGui.tenDoi)

    // Và vào lịch bên GỬI.
    await page.goto('/lich-thi-dau')
    await expect(page.locator('tbody')).toContainText(benNhan.tenDoi)

    await ctx.close()
  })

  test('từ chối thì không tạo trận', async ({ page, request, browser }) => {
    const benNhan = await taoClb(request, 'tu-choi-nhan')
    const benGui = await vaoHeThong(page, request, 'tu-choi-gui')

    await page.goto('/cong-dong')
    await page.fill('#timClb', benNhan.tenDoi)
    await page.press('#timClb', 'Enter')
    await page.locator('button:has-text("Gửi lời mời")').first().click()
    await page.fill('#moiLoiNhan', 'thử từ chối')
    await page.locator('dialog[open] button[type=submit]').click()
    await expect(page.locator('dialog[open]')).toHaveCount(0)

    const ctx = await browser.newContext()
    const trangNhan = await ctx.newPage()
    await dangNhapClb(trangNhan, benNhan.maDoi, benNhan.matKhau)

    await trangNhan.goto('/hom-thu')
    const khoiThachDau = trangNhan.locator('section', { hasText: 'Lời mời thách đấu' })
    await khoiThachDau.locator('button:has-text("Từ chối")').first().click()
    await trangNhan.locator('dialog[open] button[type=submit]').click()
    await expect(trangNhan.locator('dialog[open]')).toHaveCount(0)

    await expect(trangNhan.locator('main')).toContainText('Đã từ chối')

    // Lịch bên nhận KHÔNG có trận nào của bên gửi.
    await trangNhan.goto('/lich-thi-dau')
    await expect(trangNhan.locator('main')).not.toContainText(benGui.tenDoi)

    await ctx.close()
  })

  test('bên gửi huỷ được lời mời chưa trả lời', async ({ page, request }) => {
    const benNhan = await taoClb(request, 'huy-nhan')
    await vaoHeThong(page, request, 'huy-gui')

    await page.goto('/cong-dong')
    await page.fill('#timClb', benNhan.tenDoi)
    await page.press('#timClb', 'Enter')
    await page.locator('button:has-text("Gửi lời mời")').first().click()
    await page.fill('#moiLoiNhan', 'sẽ huỷ')
    await page.locator('dialog[open] button[type=submit]').click()
    await expect(page.locator('dialog[open]')).toHaveCount(0)

    await page.goto('/hom-thu')
    const khoiThachDau = page.locator('section', { hasText: 'Lời mời thách đấu' })
    await expect(khoiThachDau).toContainText('Bạn gửi')
    await khoiThachDau.locator('button:has-text("Huỷ lời mời")').first().click()

    // Hộp xác nhận phải nói rõ mất gì, không phải "bạn có chắc không".
    await expect(page.locator('dialog[open]')).toContainText('hòm thư của họ')
    await page.locator('dialog[open] button:has-text("Huỷ lời mời")').click()

    await expect(page.locator('main')).toContainText('Chưa có lời mời thách đấu')

    // Huỷ rồi thì gửi lại được — ràng buộc "một lời mời đang chờ" không khoá vĩnh viễn.
    await page.goto('/cong-dong')
    await page.fill('#timClb', benNhan.tenDoi)
    await page.press('#timClb', 'Enter')
    await expect(page.locator('button:has-text("Gửi lời mời")')).toHaveCount(1)
  })

  test('khai khu vực ở Thiết lập rồi lọc được trong cộng đồng', async ({ page, request }) => {
    const clb = await vaoHeThong(page, request, 'khai-khu-vuc')

    await page.goto('/quan-tri/thiet-lap')

    // Ba ô Cộng đồng phải nói rõ là công khai — không thì CLB điền số điện thoại mà không
    // biết ai đọc được.
    await expect(page.locator('main')).toContainText('hiện CÔNG KHAI')

    await page.fill('#khuVuc', 'Cẩm Lệ, Đà Nẵng')
    await page.fill('#sanNha', 'Sân Cẩm Lệ')
    await page.fill('#lienHeCongKhai', '0988777666')
    await page.locator('button[type=submit]').click()
    await expect(page.locator('main')).toContainText(/Đã lưu|lưu/i)

    // Tải lại: giá trị phải còn (quy tắc #1 — lưu không được mất trường khác).
    await page.reload()
    await expect(page.locator('#khuVuc')).toHaveValue('Cẩm Lệ, Đà Nẵng')
    await expect(page.locator('#sanNha')).toHaveValue('Sân Cẩm Lệ')
    await expect(page.locator('#lienHeCongKhai')).toHaveValue('0988777666')

    // Tên đội cũng không bị xoá.
    await expect(page.locator('#tenDoi')).toHaveValue(clb.tenDoi)
  })

  test('bấm vào CLB xem được chi tiết công khai và thách đấu từ đó', async ({
    page,
    request,
  }) => {
    const doiKia = await taoClb(request, 'xem-chi-tiet')
    const tokenDau = await layToken(request, doiKia.maDoi, doiKia.matKhau)
    await request.post('/api/v1/auth/doi-mat-khau', {
      headers: { Authorization: `Bearer ${tokenDau}` },
      data: { matKhauCu: doiKia.matKhau, matKhauMoi: MAT_KHAU_MOI },
    })
    const token = await layToken(request, doiKia.maDoi, MAT_KHAU_MOI)
    await khaiThongTinSan(request, token, doiKia.tenDoi, 'Sơn Trà, Đà Nẵng', '0912000111')

    // Một trận ĐÃ CÓ KẾT QUẢ để lịch sử đấu không rỗng. Bàn thắng nhà chỉ đến từ đánh giá
    // cầu thủ, nên phải đi đủ ba bước: tạo trận → xếp đội hình → ghi bàn.
    const taoCt = await request.post('/api/v1/cau-thu', {
      headers: { Authorization: `Bearer ${token}` },
      data: { hoTen: 'Cầu Thủ Không Được Lộ' },
    })
    expect(taoCt.ok()).toBeTruthy()
    const dsCt = await (
      await request.get('/api/v1/cau-thu?soDong=1', {
        headers: { Authorization: `Bearer ${token}` },
      })
    ).json()
    const cauThuId = dsCt.duLieu[0].id

    const taoTran = await request.post('/api/v1/tran-dau', {
      headers: { Authorization: `Bearer ${token}` },
      data: {
        thoiGian: '2027-09-09T15:00:00Z',
        doiThuId: null,
        tySoKhach: 1,
        trangThai: 'DaDienRa',
        nhanXetChung: 'NHAN XET NOI BO KHONG DUOC LO',
        ghiChu: 'GHI CHU NOI BO KHONG DUOC LO',
      },
    })
    const tranId = await taoTran.json()
    await request.put(`/api/v1/tran-dau/${tranId}/doi-hinh`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { thanhVien: [{ cauThuId, viTri: null }] },
    })
    await request.put(`/api/v1/tran-dau/${tranId}/danh-gia`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { danhGias: [{ cauThuId, soBanGhiDuoc: 3 }] },
    })

    await vaoHeThong(page, request, 'di-xem-chi-tiet')

    await page.goto('/cong-dong')
    await page.fill('#timClb', doiKia.tenDoi)
    await page.press('#timClb', 'Enter')

    // Bấm vào tên CLB → sang chi tiết.
    await page.click(`text=${doiKia.tenDoi}`)
    await page.waitForURL(new RegExp(`/cong-dong/${doiKia.maDoi}`), { timeout: 10_000 })

    // Thông tin công khai.
    await expect(page.locator('main')).toContainText('Sơn Trà, Đà Nẵng')
    await expect(page.locator('main')).toContainText('Đội E2E, tìm đối cân sức.')

    // Thành tích + lịch sử đấu.
    await expect(page.locator('main')).toContainText('Số trận đã đá')
    await expect(page.locator('table tbody')).toContainText('3 - 1')
    await expect(page.locator('table tbody')).toContainText('Thắng')

    // KHÔNG lộ: tên cầu thủ, ghi chú/nhận xét nội bộ, số điện thoại.
    const noiDung = await page.locator('main').innerText()
    expect(noiDung).not.toContain('Cầu Thủ Không Được Lộ')
    expect(noiDung).not.toContain('NHAN XET NOI BO')
    expect(noiDung).not.toContain('GHI CHU NOI BO')
    expect(noiDung).not.toContain('0912000111')

    // Thách đấu ngay từ trang chi tiết.
    await page.click('button:has-text("Gửi lời mời thách đấu")')
    await page.fill('#moiLoiNhan', 'Xem hồ sơ thấy hợp, đá không?')
    await page.locator('dialog[open] button[type=submit]').click()
    await expect(page.locator('dialog[open]')).toHaveCount(0)

    // Gửi xong nút biến mất, thay bằng trạng thái chờ — không cho gửi trùng.
    await expect(page.locator('main')).toContainText('Đang chờ họ trả lời')
    await expect(page.locator('button:has-text("Gửi lời mời thách đấu")')).toHaveCount(0)

    await page.goto('/hom-thu')
    await expect(page.locator('main')).toContainText('Xem hồ sơ thấy hợp, đá không?')
  })

  test('mã đội lạ báo rõ và có đường quay lại', async ({ page, request }) => {
    await vaoHeThong(page, request, 'ma-la')

    await page.goto('/cong-dong/ZZZZZZZ')

    // 404 ở đây là ca thường gặp (gõ nhầm mã, hoặc mã của chính mình), không phải sự cố.
    await expect(page.locator('main')).toContainText('Không tìm thấy')
    await page.click('button:has-text("Về Cộng đồng")')
    await expect(page).toHaveURL(/\/cong-dong$/)
  })
})
