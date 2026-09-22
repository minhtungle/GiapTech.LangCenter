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
 *
 * Từ 22/09/2026 trả thêm **tên viết tắt** và **cờ có logo** (yêu cầu chủ sản phẩm: *"nhập đúng
 * mã trung tâm sẽ load đúng thông tin trung tâm như trong thiết lập"*). Backend cố ý **không**
 * trả khoá ảnh — khoá mang `tenantId` ở đầu; xem `TenTrungTamTheoMaDto`.
 */
export interface NhanDienTrungTam {
  tenTrungTam: string
  tenVietTat: string | null
  coLogo: boolean
  /** Mô tả ngắn và địa chỉ — hiện trên banner màn đăng nhập (22/09/2026). */
  moTa: string | null
  diaChi: string | null
  coAnhBia: boolean
}

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
        return (await api.get<NhanDienTrungTam>(`/auth/ten-trung-tam/${ma}`)).data
      } catch (e) {
        // 404 là câu trả lời hợp lệ "không có trung tâm nào", không phải lỗi hệ thống. Ném tiếp các
        // lỗi khác để `data` là undefined và UI im lặng thay vì báo sai là "không tìm thấy".
        if (axios.isAxiosError(e) && e.response?.status === 404) return null
        throw e
      }
    },
  })

  return {
    /** Giữ tên cũ để chỗ gọi hiện có không phải sửa — vẫn là chuỗi tên hoặc null/undefined. */
    tenTrungTam: data === null ? null : data?.tenTrungTam,
    /** Toàn bộ nhận diện: tên, tên viết tắt, cờ logo. */
    trungTam: data,
    /**
     * Đường dẫn logo — `undefined` khi trung tâm chưa tải logo.
     *
     * Dùng `<img src>` THẲNG, không qua component `Anh`: endpoint này ẩn danh nên không cần
     * header token, mà `Anh` lại đi qua `api` (có interceptor gắn token và xử lý 401) — ở màn
     * đăng nhập thì 401 sẽ kích hoạt luồng làm mới token vô nghĩa.
     */
    duongDanLogo: data?.coLogo ? `/api/v1/auth/logo/${ma}` : undefined,
    /** Ảnh bìa cho banner; `undefined` = chưa tải, UI vẽ nền gradient mặc định. */
    duongDanAnhBia: data?.coAnhBia ? `/api/v1/auth/anh-bia/${ma}` : undefined,
    dangTra: duDai && isFetching,
  }
}
