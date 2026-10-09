import { useQuery } from '@tanstack/react-query'
import { api } from '@/lib/api'

/**
 * Giá trị của mục "không nối đuôi" trong ô chọn.
 *
 * Chuỗi chứ không `''`: `SelectTimKiem` coi chuỗi rỗng là "chưa chọn gì" và sẽ hiện
 * placeholder, nên người dùng không thấy mình đã chủ động chọn không nối.
 */
export const KHONG_NOI_DUOI = 'khong-noi'

export interface ThietLapTaoTaiKhoan {
  duoiTenDangNhap: string | null
  duoiTenDangNhap2: string | null
  duoiTenDangNhap3: string | null
  matKhauMacDinh: string | null
}

/**
 * Thiết lập trung tâm cần cho **mọi form tạo tài khoản**: ba đuôi tên đăng nhập và mật khẩu
 * mặc định.
 *
 * ## Vì sao là hook dùng chung, không copy vào từng màn
 *
 * Có **hai** màn tạo được tài khoản — Tài khoản (cấp cho người đã có hồ sơ) và Người dùng
 * (tạo người kèm tài khoản). Chúng phải hành xử giống nhau: cùng ô chọn đuôi, cùng mật khẩu
 * mặc định điền sẵn.
 *
 * Copy logic sang màn thứ hai là cách nó trôi: 09/10/2026 màn Người dùng thiếu cả hai, nên
 * người tạo phải **tự gõ đuôi vào ô tên đăng nhập** và tự nhớ mật khẩu mặc định — trong khi
 * màn Tài khoản ngay cạnh làm hộ cả hai việc.
 *
 * `queryKey` dùng chung với màn Thiết lập: đổi đuôi ở Thiết lập xong sang đây phải thấy ngay,
 * khoá khác nhau thì vẫn là giá trị cũ cho tới khi tải lại trang.
 */
export function useThietLapTaoTaiKhoan() {
  return useQuery({
    queryKey: ['thiet-lap'],
    queryFn: async () =>
      (await api.get<ThietLapTaoTaiKhoan>('/thiet-lap')).data,
  })
}

/**
 * Các đuôi trung tâm ĐÃ khai, kèm số thứ tự gốc.
 *
 * Giữ số gốc chứ không đánh lại từ 1: backend chọn cột theo số này, nên khai ô 1 và ô 3 mà
 * đánh lại thành 1-2 sẽ nối nhầm đuôi — và nhầm im lặng, không lỗi nào báo.
 */
export function duoiDaKhaiCua(tl: ThietLapTaoTaiKhoan | undefined) {
  return [tl?.duoiTenDangNhap, tl?.duoiTenDangNhap2, tl?.duoiTenDangNhap3]
    .map((duoi, i) => ({ so: i + 1, duoi }))
    .filter((x): x is { so: number; duoi: string } => !!x.duoi)
}
