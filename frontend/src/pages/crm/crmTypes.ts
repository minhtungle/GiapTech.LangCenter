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

export type LoaiDonHang = 'KhoaHoc' | 'SanPham'

export interface SanPhamDto {
  id: string
  ten: string
  ghiChu: string | null
  giaTien: number
  donViTien: DonViTien
  /** "quyển", "bộ"… chỉ để đọc, không tính toán. */
  donViTinh: string | null
  dangBan: boolean
  /** > 0 = không xoá được, chỉ ngừng bán. */
  soDonHang: number
  tongSoLuongBan: number
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
  /** Loại đơn: mua khoá học hay mua sản phẩm. */
  loai: LoaiDonHang
  /** Id mặt hàng — đúng một trong hai có giá trị, theo `loai`. Cần để form sửa điền lại được. */
  khoaHocId: string | null
  sanPhamId: string | null
  /** Tên thứ đã mua — khoá học hoặc sản phẩm, tuỳ `loai`. */
  tenMatHang: string
  /** Số buổi — chỉ khoá học, null với sản phẩm. */
  soBuoi: number | null
  /** Số lượng: khoá học luôn 1, sản phẩm có thể nhiều. */
  soLuong: number
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

export type HinhThucChamSoc = 'GoiDien' | 'ZaloFacebook' | 'Email' | 'GapTrucTiep' | 'Khac'
export const CAC_HINH_THUC: HinhThucChamSoc[] =
  ['GoiDien', 'ZaloFacebook', 'Email', 'GapTrucTiep', 'Khac']

export type TrangThaiKhachHang = 'Moi' | 'DangTuVan' | 'DaMua' | 'TuChoi'
export const CAC_TRANG_THAI_KH: TrangThaiKhachHang[] =
  ['Moi', 'DangTuVan', 'DaMua', 'TuChoi']

/** Màu phễu: đã mua = ok, từ chối = lỗi, đang tư vấn = chờ, mới = trung tính. */
export const mauTrangThaiKh = (tt: TrangThaiKhachHang) =>
  tt === 'DaMua' ? 'ok' : tt === 'TuChoi' ? 'loi' : tt === 'DangTuVan' ? 'cho' : 'muted'

export interface ChiTietKhachHangDto extends Omit<KhachHangDto, 'soDangKy' | 'tongMuaVnd'> {
  /** Suy từ lần chăm sóc mới nhất — không có cột trong DB. */
  trangThai: TrangThaiKhachHang
  soDangKy: number
  soLanChamSoc: number
  /** Tổng CAM KẾT (quy VND) — khác tổng đã thu. */
  tongCamKetVnd: number
  tongDaThuVnd: number
}

export interface LichSuChamSocDto {
  id: string
  thoiDiem: string
  hinhThuc: HinhThucChamSoc
  noiDung: string
  trangThaiSau: TrangThaiKhachHang
  tenNguoiPhuTrach: string | null
}

export interface LanThuDto {
  id: string
  soTien: number
  ngayThu: string
  phuongThuc: PhuongThucThanhToan
  ghiChu: string | null
  tenNguoiThu: string | null
}

export interface DangKyKemThuDto {
  id: string
  loai: LoaiDonHang
  tenMatHang: string
  soBuoi: number | null
  soLuong: number
  giaGoc: number
  /** Số khách CAM KẾT trả. */
  soTien: number
  donViTien: DonViTien
  tyGiaVeVnd: number
  phanTramTrenGiaGoc: number | null
  ngayDangKy: string
  ghiChu: string | null
  daThu: number
  /** `soTien − daThu`, tính động ở backend. ≤ 0 = đã đóng đủ. */
  conThieu: number
  cacLanThu: LanThuDto[]
  /** FR-21 — mọi lần gửi yêu cầu xếp lớp, mới nhất trước. Rỗng = chưa gửi lần nào. */
  cacLanGuiXepLop: LanGuiXepLopDto[]
  /** Có lần nào đang chờ bên đào tạo xử lý — backend tính, nút gửi ẩn khi true. */
  dangChoXepLop: boolean
  /**
   * Các lớp học viên ĐANG tham gia thật — backend đọc từ `LOP_HOC_HOC_VIEN`, không suy từ
   * trạng thái yêu cầu (12/09/2026). Gỡ khỏi lớp / lớp kết thúc thì danh sách này tự đúng.
   */
  cacLopDangHoc: LopDaThamGiaDto[]
  /** Lớp đã tham gia nhưng không còn hoạt động (lớp kết thúc/huỷ, hoặc học viên đã nghỉ). */
  cacLopDaHoc: LopDaThamGiaDto[]
  /** Đang tham gia lớp nào không — backend tính từ `cacLopDangHoc`. */
  dangThamGiaLop: boolean
  /** Tên các lớp đang học, ghép bằng dấu phẩy. null = chưa vào lớp nào. */
  tenLopDangHoc: string | null
}

/** Một lớp mà học viên có mặt trong bảng ghi danh. */
export interface LopDaThamGiaDto {
  lopHocId: string
  tenLopHoc: string
  trangThaiLop: 'Nhap' | 'SapKhaiGiang' | 'DangHoc' | 'DaKetThuc' | 'DaHuy'
  trangThaiHocVien: 'DangHoc' | 'BaoLuu' | 'ChuyenLop' | 'DaNghi'
  ngayVaoLop: string
  hocPhiApDung: number
}

/** FR-21 — trạng thái một lần gửi yêu cầu xếp lớp. */
export type TrangThaiYeuCauXepLop = 'DangCho' | 'DaXep' | 'DaHuy' | 'TuChoi'

/** FR-21 — một lần gửi yêu cầu, kèm kết quả xử lý. */
export interface LanGuiXepLopDto {
  id: string
  /** Lần thứ mấy — hiện nguyên số này, không đánh lại theo vị trí trong danh sách. */
  lanGui: number
  trangThai: TrangThaiYeuCauXepLop
  thoiDiemGui: string
  tenNguoiGui: string | null
  ghiChu: string | null
  thoiDiemXuLy: string | null
  tenNguoiXuLy: string | null
  tenLopHoc: string | null
  lyDoTuChoi: string | null
}

/** Màu badge theo trạng thái lần gửi — cùng quy ước với các bảng khác. */
export const mauTrangThaiXepLop = (tt: TrangThaiYeuCauXepLop) =>
  tt === 'DaXep' ? 'ok' : tt === 'DangCho' ? 'cho' : 'loi'

/** FR-21 — một học viên đang chờ xếp lớp, dùng ở màn LMS. */
export interface YeuCauXepLopDto {
  id: string
  dangKyId: string
  hocVienId: string
  tenHocVien: string
  soDienThoai: string | null
  khachHangId: string
  khoaHocId: string
  tenKhoaHoc: string
  soBuoi: number
  /** Số tiền đơn CRM — sẽ thành học phí áp dụng khi vào lớp. */
  soTien: number
  donViTien: DonViTien
  tyGiaVeVnd: number
  trangThai: TrangThaiYeuCauXepLop
  /** > 1 = đơn này đã bị từ chối/huỷ trước đó, nên đọc ghi chú trước khi xử lý lại. */
  lanGui: number
  thoiDiemGui: string
  tenNguoiGui: string | null
  lopHocId: string | null
  tenLopHoc: string | null
  ghiChu: string | null
}

export const ngayVN = (iso: string) => new Date(iso).toLocaleDateString('vi-VN')
export const gioNgayVN = (iso: string) =>
  new Date(iso).toLocaleString('vi-VN', {
    day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit',
  })
export const ngayChoInput = (iso: string | null) => (iso ? iso.slice(0, 10) : '')
