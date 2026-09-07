import { useQuery } from '@tanstack/react-query'
import { api } from './api'
import { useAuth } from './auth'

/** Bốn thao tác của hệ phân quyền động — khớp enum `HanhDong` ở backend. */
export type HanhDong = 'Xem' | 'Them' | 'Sua' | 'Xoa'

/**
 * Tên chức năng. Không khai enum đầy đủ 16 giá trị: danh sách này thay đổi theo backend, và
 * một chuỗi sai sẽ chỉ khiến `coQuyen` trả false — ẩn nút, không phải mở nút.
 */
export type ChucNang = string

interface DongQuyen {
  chucNang: ChucNang
  hanhDong: HanhDong
}

/**
 * Quyền của chính người đang đăng nhập, để ẩn menu và nút.
 *
 * **Đây là tiện lợi, KHÔNG phải bảo vệ.** Mọi endpoint tự gác quyền của nó; hook này chỉ để
 * người dùng không phải bấm vào rồi mới biết mình không được phép. Đừng bao giờ dựa vào nó
 * để giấu dữ liệu — dữ liệu nhạy cảm phải được backend che trước khi rời máy chủ.
 *
 * Cache theo phiên: `staleTime: Infinity` vì quyền chỉ đổi khi admin sửa nhóm quyền, và lúc
 * đó backend đã xoá cache của nó — người dùng đăng nhập lại là có quyền mới.
 */
export function useQuyen() {
  const { daDangNhap } = useAuth()

  const { data, isLoading } = useQuery({
    queryKey: ['toi-quyen'],
    queryFn: async () => (await api.get<DongQuyen[]>('/toi/quyen')).data,
    enabled: daDangNhap,
    staleTime: Infinity,
    retry: false,
  })

  const tap = new Set((data ?? []).map((x) => `${x.chucNang}:${x.hanhDong}`))

  return {
    /** Chưa biết quyền (đang tải). Dùng để không nhấp nháy menu lúc mới vào. */
    dangTai: isLoading,

    /** Có quyền thực hiện thao tác này trên chức năng này không. */
    coQuyen: (chucNang: ChucNang, hanhDong: HanhDong = 'Xem') =>
      tap.has(`${chucNang}:${hanhDong}`),

    /**
     * Được xem SỐ TIỀN của cả lớp không — cần cả `HocPhi.Xem` và `LopHocToanTrungTam.Xem`,
     * đúng điều kiện `IPhamViHocPhi.ThayToanBoSo` ở backend.
     *
     * Học viên có `HocPhi.Xem` nhưng không có `LopHocToanTrungTam` nên trả false: họ vẫn vào
     * được tab Học phí để tra công nợ của mình, chỉ không thấy con số của cả lớp.
     */
    xemTienCaLop: () =>
      tap.has('HocPhi:Xem') && tap.has('LopHocToanTrungTam:Xem'),
  }
}
