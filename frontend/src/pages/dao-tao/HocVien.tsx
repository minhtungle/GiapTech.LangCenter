import NguoiDung, { type PhamViNguoiDung } from '@/pages/quan-tri/NguoiDung'

/**
 * LMS — hồ sơ học viên. Đối xứng với `hrm/NhanSu.tsx`: cùng một component, khác phạm vi vai
 * trò, khác endpoint và khác quyền gác.
 *
 * Gác bằng `TaiKhoan` chứ không `LopHoc`: `LopHoc.Xem` là quyền **học viên cũng có**, nên gác
 * bằng nó thì học viên thấy được danh sách mọi học viên (kèm số điện thoại, địa chỉ, liên hệ
 * phụ huynh). Nhóm Giáo viên có `TaiKhoan.Xem` sẵn để "xem học viên lớp mình"; nhóm Học viên
 * không có chức năng `TaiKhoan` nào. Xem chú thích ở `HocVienController`.
 */
const PHAM_VI: PhamViNguoiDung = {
  duong: '/hoc-vien',
  vaiTro: ['HocVien'],
  can: 'TaiKhoan',
  khoaTieuDe: 'menu.hocVien',
}

export default function HocVien() {
  return <NguoiDung phamVi={PHAM_VI} />
}
