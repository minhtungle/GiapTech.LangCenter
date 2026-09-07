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
  /**
   * true = người đang xem là học viên đang học của lớp.
   *
   * Quyết định có hiện form gửi nhận xét về buổi hay không: `NHAN_XET_BUOI_HOC` là kênh của
   * học viên, backend chặn người khác bằng `KHONG_THUOC_LOP_NAY`. Thiếu cờ này thì giáo viên
   * thấy form, nhập xong mới nhận lỗi — đúng lỗi người dùng gặp 07/09/2026.
   */
  toiLaHocVien: boolean
  /** Trợ giảng của lớp — chưa có phân công trợ giảng riêng từng buổi. */
  tenTroGiangs: string[]
  /**
   * Phòng học / link CÓ HIỆU LỰC: của buổi nếu ghi riêng, không thì của lớp.
   *
   * Hiện các trường này, đừng hiện `phongHoc`/`linkHoc` thô — buổi sinh theo lịch luôn để
   * null nên sẽ ra dấu gạch, đọc thành "không có phòng" thay vì "theo lớp".
   */
  phongHocHieuLuc: string | null
  linkHocHieuLuc: string | null
  diaDiemRieng: boolean
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
