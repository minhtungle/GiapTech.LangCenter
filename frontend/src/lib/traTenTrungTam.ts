import { useQuery } from '@tanstack/react-query'
import axios from 'axios'
import { api } from '@/lib/api'

/**
 * Tra tên trung tâm theo mã, dùng ở trang đăng nhập để người dùng thấy mình đang đăng nhập vào
 * đâu trước khi gõ mật khẩu.
 *
 * **Chỉ khớp mã CHÍNH XÁC 7 ký tự.** Không tìm theo tên — quyết định của chủ sản phẩm ngày
 * 20/08 sau khi cân nhắc: endpoint này ẩn danh, nên cho tìm theo tên đồng nghĩa với việc bất
 * kỳ ai gõ một chữ cũng liệt kê được toàn bộ trung tâm trong hệ thống kèm mã. Backend có
 * `TraTenTrungTamTests.Go_TEN_vao_o_ma_thi_KHONG_tra_gi` canh điều này.
 *
 * Trả `null` khi không có trung tâm nào dùng mã đó (API trả 404) — phân biệt với `undefined` là
 * "chưa tra" để UI không hiện "không tìm thấy" lúc người dùng còn đang gõ.
 */
export function useTraTenTrungTam(maTrungTam: string) {
  const ma = maTrungTam.trim().toUpperCase()
  const duDai = ma.length === 7

  const { data, isFetching } = useQuery({
    queryKey: ['ten-trung-tam', ma],
    enabled: duDai,
    // Mã đội không đổi tên trong một phiên đăng nhập; giữ luôn để gõ lui gõ lại không gọi lại.
    staleTime: Infinity,
    retry: false,
    queryFn: async () => {
      try {
        return (await api.get<{ tenTrungTam: string }>(`/auth/ten-trung-tam/${ma}`)).data.tenTrungTam
      } catch (e) {
        // 404 là câu trả lời hợp lệ "không có trung tâm nào", không phải lỗi hệ thống. Ném tiếp các
        // lỗi khác để `data` là undefined và UI im lặng thay vì báo sai là "không tìm thấy".
        if (axios.isAxiosError(e) && e.response?.status === 404) return null
        throw e
      }
    },
  })

  return { tenTrungTam: data, dangTra: duDai && isFetching }
}
