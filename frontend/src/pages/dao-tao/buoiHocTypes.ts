/**
 * Kiểu và tiện ích dùng chung cho buổi học.
 *
 * Tách ra file riêng vì ba nơi dùng: danh sách buổi (`LichVaDiemDanh`), bảng điểm danh
 * (`BangDiemDanh`), và view chi tiết buổi (`ChiTietBuoiHoc`). Trước đó `BuoiHocDto` khai local
 * trong `LichVaDiemDanh.tsx` và **thiếu 6 trường** (`lopHocId`, `tenLopHoc`, `phongHoc`,
 * `linkHoc`, `ghiChu`, `giaoVienId`) — đủ cho bảng nhưng không đủ cho view chi tiết.
 */

export type TrangThaiBuoiHoc = 'DaLenLich' | 'DaHoanThanh' | 'DaHuy'

export interface BuoiHocDto {
  id: string
  lopHocId: string
  tenLopHoc: string
  thuTu: number
  batDau: string
  ketThuc: string
  giaoVienId: string
  tenGiaoVien: string
  giaoVienRieng: boolean
  trangThai: TrangThaiBuoiHoc
  laHocBu: boolean
  phongHoc: string | null
  linkHoc: string | null
  ghiChu: string | null
  soDaDiemDanh: number
  soHocVien: number
}

/**
 * Buổi đã KHOÁ — không sửa giờ, không huỷ, không xoá. Suy từ trạng thái thay vì rải
 * `=== 'DaHoanThanh'` khắp nơi, khớp với `BuoiHoc.DaKhoa` ở backend.
 */
export const daKhoa = (b: Pick<BuoiHocDto, 'trangThai'>) => b.trangThai === 'DaHoanThanh'

/** Giờ hiển thị. TODO: chuyển sang `useMuiGio()` cho nhất quán với lịch (nợ đã ghi). */
export const gioVN = (s: string) =>
  new Date(s).toLocaleString('vi-VN', {
    weekday: 'short', day: '2-digit', month: '2-digit',
    hour: '2-digit', minute: '2-digit',
  })
