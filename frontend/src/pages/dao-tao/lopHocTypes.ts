/**
 * Kiểu và tiện ích dùng chung giữa danh sách lớp và view chi tiết lớp.
 *
 * Tách ra file riêng thay vì export từ `LopHoc.tsx`: view chi tiết import ngược lại trang danh
 * sách sẽ tạo vòng phụ thuộc khi danh sách cần điều hướng sang chi tiết.
 */

export type HinhThucHoc = 'ChuaChon' | 'Online' | 'Offline' | 'KetHop'
export type TrangThaiLopHoc = 'Nhap' | 'SapKhaiGiang' | 'DangHoc' | 'DaKetThuc' | 'DaHuy'

export const CAC_HINH_THUC: HinhThucHoc[] = ['Online', 'Offline', 'KetHop']

export interface LopHocDto {
  id: string
  ten: string
  giaoVienChinhId: string
  tenGiaoVienChinh: string
  hinhThuc: HinhThucHoc
  phongHoc: string | null
  linkHoc: string | null
  hocPhi: number | null
  sucChuaToiDa: number | null
  ngayKhaiGiang: string | null
  ngayKetThuc: string | null
  trangThai: TrangThaiLopHoc
  ghiChu: string | null
  troGiangIds: string[]
  tenTroGiangs: string[]
  soHocVien: number
  /** Khoá học lớp này dạy — tối đa 3 (12/09/2026). Rỗng = chưa gán. */
  khoaHocs: KhoaHocCuaLopDto[]
}

/** Một khoá học mà lớp dạy — đủ để hiện tên và đối chiếu với đơn CRM. */
export interface KhoaHocCuaLopDto {
  id: string
  ten: string
}

export interface NguoiDungNgan {
  id: string
  hoTen: string
  email: string | null
  loaiNguoiDung: string
}

export interface HocVienTrongLop {
  id: string
  hocVienId: string
  hoTen: string
  email: string | null
  soDienThoai: string | null
  ngayVaoLop: string
  trangThai: string
  /** null = người đang xem không được phép biết mức này (xem IPhamViHocPhi). */
  hocPhiApDung: number | null
  /** Ghi chú riêng cho học viên này trong lớp. Backend vốn đã trả, type cũ thiếu. */
  ghiChu: string | null
  /**
   * Nhân viên kinh doanh đã tạo hồ sơ khách hàng (12/09/2026).
   *
   * null có BA nghĩa — đừng hiển thị thành một: không đủ quyền xem (thiếu `KhachHang.Xem`),
   * học viên thêm tay không qua CRM, hoặc khách tạo trước 12/09/2026.
   */
  tenNhanVienKinhDoanh: string | null
}

export const tienVN = (n: number | null) =>
  n === null ? '—' : n.toLocaleString('vi-VN') + '₫'

export const ngayVN = (s: string | null) =>
  s ? new Date(s).toLocaleDateString('vi-VN') : '—'

/** Màu badge theo trạng thái — nháp mờ, đang học nổi, đã huỷ đỏ. */
export const mauTrangThai = (tt: TrangThaiLopHoc) =>
  tt === 'DangHoc' ? 'ok' : tt === 'DaHuy' ? 'loi' : tt === 'Nhap' ? 'cho' : 'accent'
