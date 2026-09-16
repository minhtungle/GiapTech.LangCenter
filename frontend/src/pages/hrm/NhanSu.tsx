import NguoiDung, { type PhamViNguoiDung } from '@/pages/quan-tri/NguoiDung'

/**
 * HRM — nhân sự: nhân viên, giáo viên, trợ giảng.
 *
 * Học viên **không** ở đây (chốt 08/09/2026): học viên là khách, không phải nhân sự. Người phụ
 * trách tuyển sinh cần thêm học viên nhưng không nên thấy hợp đồng, lương của giáo viên — mà
 * nếu hai nhóm dùng chung một màn thì họ buộc phải cùng một quyền. Xem `HocVien.tsx` bên LMS.
 *
 * Trợ giảng đi cùng giáo viên vì dùng chung hồ sơ `HO_SO_GIAO_VIEN`.
 */
const PHAM_VI: PhamViNguoiDung = {
  duong: '/nhan-su',
  vaiTro: ['NhanVien', 'NhanVienKinhDoanh', 'GiaoVien', 'TroGiang'],
  can: 'NhanSu',
  khoaTieuDe: 'menu.nhanSuNguoiDung',
  duongChiTiet: '/hrm/nhan-su',
  // Bộ lọc phòng ban + chức vụ (16/09/2026): hai chiều tra cứu chính của quản lý nhân sự.
  // Học viên không vào cơ cấu nên màn Học viên KHÔNG bật cờ này.
  locCoCau: true,
}

export default function NhanSu() {
  return <NguoiDung phamVi={PHAM_VI} />
}
