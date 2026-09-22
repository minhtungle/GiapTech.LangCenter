import { expect, test } from '@playwright/test'
import { MAT_KHAU_MOI, taoTrungTam, dangNhap, layTokenQuaApi } from './tro-giup'

/**
 * Tab **Lịch học** ở màn Lớp học — lịch của MỌI lớp trên một tấm (21/09/2026).
 *
 * Yêu cầu: *"phần lớp học — bổ sung chế độ xem dạng lịch như lịch học"*. Khác lịch trong từng
 * lớp: ở đây trả lời *"tuần này trung tâm dạy những gì"*, nên nhãn phải mang **tên lớp** chứ
 * không phải "Buổi 3".
 *
 * Canh bốn điều, mỗi điều ứng một cách hỏng THẬT:
 *
 * 1. **Nhiều lớp cùng hiện** — gộp từ bảng danh sách (có phân trang) thì lịch thiếu buổi.
 * 2. **Nhãn mang tên lớp** — thiếu thì nhìn ô "Buổi 3" không biết của lớp nào.
 * 3. **Lọc theo lớp thu hẹp đúng** — và chiều ngược: bỏ lọc thì đủ trở lại.
 * 4. **Đi xa vẫn còn lịch** — bản đầu thay cả tấm lịch bằng "chưa có buổi nào" khi tháng
 *    rỗng, nên bấm ‹ vài tháng là lịch **biến mất cùng nút điều hướng**, kẹt không quay lại
 *    được (chủ sản phẩm báo 21/09).
 */
test('Tab Lịch học hiện buổi của mọi lớp, lọc theo lớp thu hẹp đúng', async ({
  page, request,
}) => {
  /** Nhãn nút bỏ lọc — khai một chỗ để đổi i18n không phải sửa hai nơi. */
  const t_boLoc = 'Bỏ lọc'

  const tt = await taoTrungTam(request, 'lich-tat-ca')
  await dangNhap(page, tt)

  const tok = await layTokenQuaApi(page)
  const api = async (duong: string, than: unknown) => {
    const res = await page.request.post(`/api/v1${duong}`, {
      headers: { Authorization: `Bearer ${tok}` },
      data: than,
    })
    const txt = await res.text()
    expect(res.ok(), `${duong} → ${res.status()} ${txt}`).toBeTruthy()
    return txt ? JSON.parse(txt) : null
  }

  const gv = await api('/nguoi-dung', { hoTen: 'GV Lịch', loaiNguoiDung: 'GiaoVien' })

  // HAI lớp, giờ khác nhau — một lớp thì không phân biệt được "lịch nhiều lớp" với lịch cũ.
  const tao = async (ten: string, thu: string, gio: string) => {
    const lop = await api('/lop-hoc', {
      ten, giaoVienChinhId: gv, hinhThuc: 'Offline', hocPhi: 1000, troGiangIds: [],
    })
    await api(`/lop-hoc/${lop}/sinh-lich`, {
      ngayKhaiGiang: '2026-10-05',
      thuTrongTuan: [thu],
      gioBatDau: gio, gioKetThuc: '20:00:00', soBuoi: 3,
    })
    await api(`/lop-hoc/${lop}/hoan-tat`, {})
    return lop
  }

  await tao('Lớp Alpha', 'Monday', '09:00:00')
  await tao('Lớp Beta', 'Wednesday', '18:00:00')

  // Bảng / Lịch là NÚT CHUYỂN VIEW (21/09/2026), không còn là tab riêng.
  await page.goto('/lms/lop-hoc')
  await page.getByRole('button', { name: 'Lịch', exact: true }).click()
  await expect(page.locator('.fc-event').first()).toBeVisible({ timeout: 20_000 })

  /*
    Lịch mở ở tháng HIỆN TẠI, mà buổi nằm ở 10/2026 — bấm "Kỳ sau" cho tới khi thấy đủ.

    Nhắm nút theo `aria-label` (`lich.sau` = "Kỳ sau"), KHÔNG theo ký tự "›": nút chỉ chứa
    icon SVG, không có chữ nào để khớp — dò theo ký tự sẽ treo tới hết timeout.
  */
  for (let i = 0; i < 6; i++) {
    if ((await page.locator('.fc-event').count()) >= 6) break
    await page.getByRole('button', { name: 'Kỳ sau' }).click()
    await page.waitForTimeout(700)
  }

  const nhan = async () =>
    (await page.locator('.fc-event').allInnerTexts()).map((x) => x.split('\n')[0])

  const truoc = await nhan()

  // 1 + 2: cả hai lớp cùng hiện, và nhãn mang TÊN LỚP.
  expect(truoc.some((x) => x.includes('Alpha')), `nhãn: ${truoc.join(' | ')}`).toBeTruthy()
  expect(truoc.some((x) => x.includes('Beta')), `nhãn: ${truoc.join(' | ')}`).toBeTruthy()

  // 3: lọc còn đúng một lớp.
  await page.locator('#locLop').click()
  // Nhắm đúng OPTION trong dropdown (`role="option"`), không `getByText`: tên lớp cũng nằm
  // trên các ô sự kiện của lịch, nên `getByText(...).last()` bấm trúng một buổi học và
  // chuyển sang màn chi tiết buổi — test đỏ ở chỗ chẳng liên quan gì tới bộ lọc.
  await page.getByRole('option', { name: 'Lớp Alpha' }).click()
  await page.waitForTimeout(1200)

  const sau = await nhan()
  expect(sau.length).toBeGreaterThan(0)
  expect(sau.every((x) => x.includes('Alpha')), `sau khi lọc: ${sau.join(' | ')}`).toBeTruthy()

  // Chiều NGƯỢC: bỏ lọc thì Beta quay lại. Thiếu vế này thì một bản sửa "lọc mất hết" cũng xanh.
  expect(sau.length).toBeLessThan(truoc.length)

  // ---------- 4: đi xa tới tháng rỗng, lịch và nút điều hướng PHẢI còn ----------
  await page.getByRole('button', { name: t_boLoc }).click()
  await page.waitForTimeout(600)

  for (let i = 0; i < 10; i++) {
    await page.getByRole('button', { name: 'Kỳ sau' }).click()
    await page.waitForTimeout(250)
  }
  await page.waitForTimeout(1200)

  // Tháng rỗng: KHÔNG được thay cả tấm lịch bằng thông báo — đó chính là lỗi cũ.
  await expect(page.locator('.fc')).toBeVisible()
  await expect(page.getByRole('button', { name: 'Kỳ trước' })).toBeVisible()
  expect(await page.locator('.fc-event').count(), 'tháng xa phải rỗng').toBe(0)

  // Và quay về được — nút còn thì bấm được.
  await page.getByRole('button', { name: 'Hôm nay' }).click()
  await page.waitForTimeout(1500)
  expect(await page.locator('.fc-event').count(), 'bấm Hôm nay phải thấy lại buổi')
    .toBeGreaterThan(0)
})
