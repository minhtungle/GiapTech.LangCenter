import { useQuery } from '@tanstack/react-query'
import { api } from '@/lib/api'

/**
 * Cờ tính năng do API khai (`GET /api/v1/tinh-nang`), đọc được khi chưa đăng nhập.
 *
 * Dùng để KHÔNG hiện lối vào dẫn tới ngõ cụt: nếu đăng ký ẩn danh bị tắt ở máy chủ, mà
 * frontend không tự biết mình đang nói chuyện với môi trường nào. Không có cờ này thì trên
 * frontend vẫn hiện nút thì người dùng bấm "Tạo trung tâm", điền tên, rồi nhận lỗi từ một
 * 404 — trông như app hỏng chứ không phải "chức năng chưa mở".
 */
export interface TinhNang {
  dangKyTrungTam: boolean
}

export function useTinhNang() {
  const { data } = useQuery({
    queryKey: ['tinh-nang'],
    queryFn: async () => (await api.get<TinhNang>('/tinh-nang')).data,
    // Cờ chỉ đổi khi deploy lại, không cần hỏi lại giữa chừng.
    staleTime: Infinity,
    retry: false,
  })

  // Mặc định ẨN khi chưa biết: hiện nhầm rồi bấm vào ngõ cụt tệ hơn là thiếu một link vài trăm
  // mili-giây. Cũng là cách an toàn khi API cũ chưa có endpoint này (404 → data undefined).
  return data ?? { dangKyTrungTam: false }
}
