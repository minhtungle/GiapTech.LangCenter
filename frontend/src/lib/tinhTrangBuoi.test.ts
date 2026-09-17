import { describe, expect, it } from 'vitest'
import {
  MAU_TINH_TRANG,
  TRANG_THAI_DAT_DUOC,
  type TinhTrangBuoi,
  tinhTrangHienTai,
} from './tinhTrangBuoi'

/**
 * Tình trạng buổi học — **bản sao frontend** của `TinhTrangBuoiHocExt` (backend, 18/09/2026).
 *
 * Hai bản sao là có chủ ý (xem chú thích trong `tinhTrangBuoi.ts`), nên bộ test này cố tình
 * lặp lại **đúng các ca** của `TinhTrangBuoiHocTests.cs` ở backend. Hai bên lệch luật thì một
 * trong hai bộ test sẽ đỏ.
 */
describe('tinhTrangHienTai', () => {
  const bayGio = new Date('2026-09-18T10:00:00Z')
  const gio = (h: number) => new Date(bayGio.getTime() + h * 3600_000).toISOString()

  it('buổi còn DaLenLich thì GIỜ quyết định', () => {
    // Chưa tới giờ.
    expect(tinhTrangHienTai('DaLenLich', gio(2), gio(4), bayGio)).toBe('ChuaBatDau')
    // Đang trong khoảng.
    expect(tinhTrangHienTai('DaLenLich', gio(-1), gio(1), bayGio)).toBe('DangDienRa')
    // Giờ đã qua mà chưa ai chốt — ĐÂY là ca chữa lỗi người dùng báo ("buổi đã qua vẫn hiện
    // đã lên lịch"): phải ra `ChuaChot`, không phải `ChuaBatDau`.
    expect(tinhTrangHienTai('DaLenLich', gio(-4), gio(-2), bayGio)).toBe('ChuaChot')
  })

  it('đúng phút bắt đầu và kết thúc đều tính là đang diễn ra', () => {
    const moc = bayGio.toISOString()
    expect(tinhTrangHienTai('DaLenLich', moc, gio(2), bayGio)).toBe('DangDienRa')
    expect(tinhTrangHienTai('DaLenLich', gio(-2), moc, bayGio)).toBe('DangDienRa')

    // Quá một giây là hết — chốt biên trên, `<` thay `<=` sẽ trượt đúng ở đây.
    const vuaQua = new Date(bayGio.getTime() - 1000).toISOString()
    expect(tinhTrangHienTai('DaLenLich', gio(-2), vuaQua, bayGio)).toBe('ChuaChot')
  })

  it('trạng thái người đặt THẮNG giờ, ở mọi vị trí thời gian', () => {
    const cases: [Parameters<typeof tinhTrangHienTai>[0], TinhTrangBuoi][] = [
      ['DaHoanThanh', 'DaXong'],
      ['DaHuy', 'DaHuy'],
      ['ChuyenLich', 'ChuyenLich'],
    ]

    for (const [daLuu, mongDoi] of cases) {
      // Buổi đã huỷ mà đang trong khung giờ KHÔNG được hiện "đang diễn ra" — người dùng sẽ
      // vào một lớp trống.
      expect(tinhTrangHienTai(daLuu, gio(2), gio(4), bayGio)).toBe(mongDoi)
      expect(tinhTrangHienTai(daLuu, gio(-1), gio(1), bayGio)).toBe(mongDoi)
      expect(tinhTrangHienTai(daLuu, gio(-4), gio(-2), bayGio)).toBe(mongDoi)
    }
  })

  it('mọi tình trạng đều có màu khai sẵn', () => {
    const moiTinhTrang: TinhTrangBuoi[] = [
      'ChuaBatDau', 'DangDienRa', 'ChuaChot', 'DaXong', 'ChuyenLich', 'DaHuy',
    ]
    // Thiếu một dòng thì Tailwind nhận `undefined` và ô mất màu — không lỗi nào ném ra.
    for (const tt of moiTinhTrang) {
      expect(MAU_TINH_TRANG[tt]?.bg, tt).toBeTruthy()
      expect(MAU_TINH_TRANG[tt]?.chu, tt).toBeTruthy()
    }
  })

  it('chỉ liệt kê trạng thái người ĐẶT ĐƯỢC, không có thứ suy từ giờ', () => {
    expect(TRANG_THAI_DAT_DUOC).toEqual(
      ['DaLenLich', 'DaHoanThanh', 'ChuyenLich', 'DaHuy'])

    // Chiều NGƯỢC, mới là phần đáng canh: cho đặt tay "đang diễn ra" là tạo hai nguồn sự thật
    // cho cùng một câu hỏi, và giờ thật sẽ ghi đè lựa chọn của người dùng.
    for (const suyTuGio of ['ChuaBatDau', 'DangDienRa', 'ChuaChot']) {
      expect(TRANG_THAI_DAT_DUOC).not.toContain(suyTuGio)
    }
  })
})
