import { useQuery } from '@tanstack/react-query'
import axios from 'axios'
import { api } from '@/lib/api'

/**
 * Tra tên CLB theo mã đội, dùng ở trang đăng nhập để người dùng thấy mình đang đăng nhập vào
 * đâu trước khi gõ mật khẩu.
 *
 * **Chỉ khớp mã CHÍNH XÁC 7 ký tự.** Không tìm theo tên — quyết định của chủ sản phẩm ngày
 * 20/08 sau khi cân nhắc: endpoint này ẩn danh, nên cho tìm theo tên đồng nghĩa với việc bất
 * kỳ ai gõ một chữ cũng liệt kê được toàn bộ CLB trong hệ thống kèm mã đội. Backend có
 * `TraTenDoiTests.Go_TEN_doi_vao_o_ma_thi_KHONG_tra_gi` canh điều này.
 *
 * Trả `null` khi không có CLB nào dùng mã đó (API trả 404) — phân biệt với `undefined` là
 * "chưa tra" để UI không hiện "không tìm thấy" lúc người dùng còn đang gõ.
 */
export function useTraTenDoi(maDoi: string) {
  const ma = maDoi.trim().toUpperCase()
  const duDai = ma.length === 7

  const { data, isFetching } = useQuery({
    queryKey: ['ten-doi', ma],
    enabled: duDai,
    // Mã đội không đổi tên trong một phiên đăng nhập; giữ luôn để gõ lui gõ lại không gọi lại.
    staleTime: Infinity,
    retry: false,
    queryFn: async () => {
      try {
        return (await api.get<{ tenDoi: string }>(`/auth/ten-doi/${ma}`)).data.tenDoi
      } catch (e) {
        // 404 là câu trả lời hợp lệ "không có CLB nào", không phải lỗi hệ thống. Ném tiếp các
        // lỗi khác để `data` là undefined và UI im lặng thay vì báo sai là "không tìm thấy".
        if (axios.isAxiosError(e) && e.response?.status === 404) return null
        throw e
      }
    },
  })

  return { tenDoi: data, dangTra: duDai && isFetching }
}
