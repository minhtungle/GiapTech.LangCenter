/** Kiểu và tiện ích dùng chung cho ba màn CRM (FR-17 → FR-19). */

export type DonViTien = 'VND' | 'USD' | 'EUR' | 'CAD'
export const CAC_DON_VI: DonViTien[] = ['VND', 'USD', 'EUR', 'CAD']

export type PhuongThucThanhToan = 'TienMat' | 'ChuyenKhoan' | 'Khac'
export const CAC_PHUONG_THUC: PhuongThucThanhToan[] = ['TienMat', 'ChuyenKhoan', 'Khac']

export interface KhachHangDto {
  id: string
  hoTen: string
  email: string | null
  soDienThoai: string | null
  linkFacebook: string | null
  ghiChu: string | null
  phuongThucThanhToan: PhuongThucThanhToan
  /** Có giá trị = khách đã thành học viên; hồ sơ học tập nằm ở NGUOI_DUNG. */
  nguoiDungId: string | null
  tenHocVien: string | null
  soDangKy: number
  tongMuaVnd: number
}

export interface KhoaHocDto {
  id: string
  ten: string
  ghiChu: string | null
  giaTien: number
  donViTien: DonViTien
  soBuoi: number
  dangBan: boolean
  /** > 0 = không xoá được, chỉ ngừng bán. */
  soDangKy: number
}

export interface DangKyDto {
  id: string
  khachHangId: string
  tenKhachHang: string
  soDienThoai: string | null
  linkFacebook: string | null
  khoaHocId: string
  tenKhoaHoc: string
  soBuoi: number
  giaGoc: number
  soTien: number
  donViTien: DonViTien
  tyGiaVeVnd: number
  quyDoiVnd: number
  /** null khi giá gốc = 0 — hiện dấu gạch, không chia cho 0. */
  phanTramTrenGiaGoc: number | null
  ngayDangKy: string
  phuongThuc: PhuongThucThanhToan
  ghiChu: string | null
}

export interface TongHopDoanhThuDto {
  soDangKy: number
  soKhachHang: number
  tongVnd: number
  theoDonVi: { donViTien: DonViTien; tong: number; tongVnd: number }[]
}

/**
 * Định dạng tiền theo đơn vị của chính nó.
 *
 * Không quy hết về VND khi hiển thị từng dòng: người bán cần thấy đúng con số họ đã báo giá
 * cho khách (1.200 EUR), còn cột quy đổi là để tổng hợp.
 */
export const tien = (so: number, donVi: DonViTien = 'VND') =>
  new Intl.NumberFormat('vi-VN', {
    style: 'currency',
    currency: donVi,
    // VND không có phần thập phân; ngoại tệ giữ 2 số.
    maximumFractionDigits: donVi === 'VND' ? 0 : 2,
  }).format(so)

/** Phần trăm trên giá gốc — làm tròn 1 số, không hiện "80.00%". */
export const phanTram = (p: number | null) => (p === null ? '—' : `${p.toFixed(1)}%`)

/**
 * Màu badge cho % giá: đủ giá = ok, giảm nhẹ = chờ, giảm sâu = lỗi (cần chú ý).
 *
 * Ngưỡng 70% không phải quy tắc nghiệp vụ chốt — chỉ là mốc để người quản lý nhìn ra đơn giảm
 * sâu. Đổi ngưỡng thì đổi ở đây, một chỗ.
 */
export const mauPhanTram = (p: number | null) =>
  p === null ? 'muted' : p >= 100 ? 'ok' : p >= 70 ? 'cho' : 'loi'

export const ngayVN = (iso: string) => new Date(iso).toLocaleDateString('vi-VN')
export const ngayChoInput = (iso: string | null) => (iso ? iso.slice(0, 10) : '')
