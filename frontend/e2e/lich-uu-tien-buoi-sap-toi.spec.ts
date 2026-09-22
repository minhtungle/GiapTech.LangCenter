import { expect, test } from '@playwright/test'
import { vaoHeThong, layTokenQuaApi } from './tro-giup'

/**
 * Lịch học **ưu tiên buổi sắp tới lên đầu** (yêu cầu chủ sản phẩm 16/09/2026).
 *
 * API trả buổi theo `ThuTu` (1→N) nên buổi đã dạy nằm trên; lớp 30 buổi thì buổi kế tiếp phải
 * cuộn gần hết bảng mới thấy — mà "buổi tới dạy gì, phòng nào" mới là việc thường ngày.
 *
 * ## Vì sao sắp ở FRONTEND, và vì sao test phải canh điều đó
 *
 * Cùng endpoint `/lop-hoc/{id}/buoi-hoc` có **bốn màn khác** dùng, trong đó `ChiTietBuoiHoc` suy
 * "buổi trước / buổi sau" từ **vị trí trong mảng**. Đổi `OrderBy` ở API thì nút "buổi sau" nhảy
 * về quá khứ — lỗi im lặng, không có gì báo. Nên test này kiểm **cả hai**: màn lịch sắp lại đúng,
 * và điều hướng buổi trước/sau vẫn theo thứ tự thời gian.
 */
test('Buổi sắp tới lên đầu, buổi đã qua xuống dưới', async ({ page, request }) => {
  const ttE2E = await vaoHeThong(page, request, 'lich-sap-toi')
  const token = await layTokenQuaApi(page)

  const api = async (duong: string, than: unknown) => {
    const res = await page.request.post(`http://localhost:5229/api/v1${duong}`, {
      headers: { Authorization: `Bearer ${token}` },
      data: than,
    })
    const txt = await res.text()
    expect(res.ok(), `${duong} → ${res.status()} ${txt}`).toBeTruthy()
    // `hoan-tat` trả 204 không thân — `res.json()` trần sẽ ném "Unexpected end of JSON input".
    return txt ? JSON.parse(txt) : null
  }

  const loiJs: string[] = []
  page.on('pageerror', (e) => loiJs.push(e.message))

  const gv = await api('/nhan-su', { hoTen: 'GV Lịch', loaiNguoiDung: 'GiaoVien' })
  const lop = await api('/lop-hoc', {
    ten: 'Lớp thử lịch', giaoVienChinhId: gv, hinhThuc: 'Offline',
    hocPhi: 1000, troGiangIds: [],
  })

  /*
    Sinh lịch bắt đầu từ HAI THÁNG TRƯỚC hôm nay, đủ dài để vắt qua hiện tại.

    Mốc tính theo ngày chạy test, không cắm ngày cứng: cắm cứng thì test đúng hôm nay và sai vào
    năm sau — mà lúc đó không ai hiểu vì sao đỏ.
  */
  const hai_thang_truoc = new Date()
  hai_thang_truoc.setMonth(hai_thang_truoc.getMonth() - 2)
  const iso = (d: Date) =>
    `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`

  await api(`/lop-hoc/${lop}/sinh-lich`, {
    ngayKhaiGiang: iso(hai_thang_truoc),
    thuTrongTuan: ['Monday', 'Wednesday', 'Friday'],
    gioBatDau: '18:00:00',
    gioKetThuc: '20:00:00',
    soBuoi: 30,
  })

  // Lớp `Nhap` chỉ người tạo mới thấy buổi học (`IPhamViLopHoc`) — bảng sẽ RỖNG nếu quên bước
  // này, và test đỏ ở chỗ không liên quan tới thứ tự.
  await api(`/lop-hoc/${lop}/hoan-tat`, {})

  await page.goto(`/lms/lop-hoc/${lop}?tab=lich`)
  /*
    Chờ bằng SỐ DÒNG, không bằng `getByText(/SẮP TỚI/)`: chữ đó nằm trong `<td>` mà `<td>` lại
    trong `<tr>`, nên locator khớp nhiều phần tử và `toBeVisible` báo strict mode violation —
    tính năng đúng mà test vẫn đỏ (đã gặp).
  */
  await expect(page.locator('tbody tr')).not.toHaveCount(0, { timeout: 15000 })

  /*
    Kiểm bằng TEXT CỦA TỪNG DÒNG, không bằng locator theo chữ.
    `getByText(/SẮP TỚI/)` khớp cả `<td>` lẫn `<tr>` bọc nó (strict mode violation), còn
    `getByRole('cell')` thì không khớp vì bảng này không khai role — cả hai đều làm test đỏ
    trong khi tính năng đúng. Đọc `allInnerTexts()` một lần rồi suy vị trí là cách chắc nhất,
    và cũng chính là thứ cần kiểm: THỨ TỰ.
  */
  const dong = await page.locator('tbody tr').allInnerTexts()

  const iSapToi = dong.findIndex((x) => /SẮP TỚI/.test(x))
  const iDaQua = dong.findIndex((x) => /ĐÃ QUA/.test(x))

  // Nhóm "sắp tới" phải đứng TRƯỚC nhóm "đã qua" — đây là chốt chính.
  expect(iSapToi, 'không thấy tiêu đề SẮP TỚI').toBeGreaterThanOrEqual(0)
  expect(iDaQua, 'nhóm ĐÃ QUA phải nằm dưới nhóm SẮP TỚI').toBeGreaterThan(iSapToi)

  /*
    Dòng ĐẦU TIÊN sau tiêu đề "sắp tới" phải là buổi GẦN NHẤT trong tương lai.

    Lấy số buổi của dòng đó rồi so với số buổi nhỏ nhất trong nhóm: nếu sắp sai chiều (xa nhất
    trước) thì số này sẽ là buổi cuối khoá.
  */
  const trongSapToi = dong.slice(iSapToi + 1, iDaQua)
  const soBuoi = trongSapToi.map((x) => Number(x.split('\t')[0])).filter((n) => !Number.isNaN(n))
  expect(soBuoi.length, 'nhóm sắp tới không có dòng nào').toBeGreaterThan(0)
  expect(soBuoi[0], 'buổi đầu nhóm sắp tới phải là buổi gần nhất')
    .toBe(Math.min(...soBuoi))

  // Nhóm "đã qua" xếp MỚI NHẤT TRƯỚC: buổi vừa dạy hay được xem lại nhất.
  const soDaQua = dong.slice(iDaQua + 1)
    .map((x) => Number(x.split('\t')[0])).filter((n) => !Number.isNaN(n))
  expect(soDaQua[0], 'nhóm đã qua phải xếp mới nhất trước').toBe(Math.max(...soDaQua))

  /*
    CHIỀU NGƯỢC — thứ dễ vỡ nhất khi làm tính năng này: `ChiTietBuoiHoc` suy buổi trước/sau từ
    vị trí trong mảng API. Nếu ai đó "sửa cho gọn" bằng cách đổi `OrderBy` ở backend thì nút
    "buổi sau" sẽ nhảy về quá khứ.
  */
  const ds = await (await page.request.get(
    `http://localhost:5229/api/v1/lop-hoc/${lop}/buoi-hoc`,
    { headers: { Authorization: `Bearer ${await layTokenQuaApi(page)}` } })).json()
  const thuTu = (ds as { thuTu: number }[]).map((b) => b.thuTu)
  expect(thuTu, 'API phải giữ thứ tự theo số buổi — 4 màn khác phụ thuộc vào nó')
    .toEqual([...thuTu].sort((a, b) => a - b))

  expect(loiJs, 'có lỗi JS chưa xử lý').toEqual([])
})
