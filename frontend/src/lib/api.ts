import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios'

const KHOA_ACCESS = 'lms_access_token'
const KHOA_REFRESH = 'lms_refresh_token'

export const luuToken = (access: string, refresh: string) => {
  localStorage.setItem(KHOA_ACCESS, access)
  localStorage.setItem(KHOA_REFRESH, refresh)
}

export const xoaToken = () => {
  localStorage.removeItem(KHOA_ACCESS)
  localStorage.removeItem(KHOA_REFRESH)
}

export const layAccessToken = () => localStorage.getItem(KHOA_ACCESS)
export const layRefreshToken = () => localStorage.getItem(KHOA_REFRESH)

export const api = axios.create({ baseURL: '/api/v1' })

api.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = layAccessToken()
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

/**
 * Hàng đợi request đang chờ token mới.
 *
 * Cần thiết vì refresh token XOAY VÒNG ở backend: token cũ chết ngay khi đổi. Nếu ba
 * request cùng gặp 401 và cùng gọi làm-mới, request thứ hai sẽ dùng token đã bị thu hồi
 * và backend hiểu nhầm là bị đánh cắp — thu hồi toàn bộ phiên, đá người dùng ra ngoài.
 * Vì vậy chỉ cho MỘT lần làm mới chạy, các request khác chờ kết quả.
 */
let dangLamMoi: Promise<string | null> | null = null

async function lamMoiToken(): Promise<string | null> {
  const refresh = layRefreshToken()
  if (!refresh) return null

  try {
    // Dùng axios trần, không qua `api`: nếu đi qua interceptor thì lỗi 401 của chính
    // request làm mới sẽ kích hoạt vòng lặp làm mới vô tận.
    const { data } = await axios.post('/api/v1/auth/lam-moi-token', {
      refreshToken: refresh,
    })
    luuToken(data.accessToken, data.refreshToken)
    return data.accessToken
  } catch {
    xoaToken()
    return null
  }
}

api.interceptors.response.use(
  (res) => res,
  async (error: AxiosError) => {
    const config = error.config as InternalAxiosRequestConfig & { _daThuLai?: boolean }

    /*
      Phiên bị đẩy ra vì đăng nhập nơi khác (20/09/2026) — KHÔNG thử làm mới token.

      Làm mới cũng vô ích: refresh token của phiên cũ đã bị thu hồi ngay lúc người kia đăng
      nhập. Thử rồi mới về màn đăng nhập chỉ tốn thêm một vòng request, và người dùng mất câu
      giải thích: họ về màn đăng nhập trắng trơn, không hiểu vì sao đang dùng thì bị văng.

      Mang mã lỗi sang màn đăng nhập qua `sessionStorage` (không phải query string): lý do bị
      đá ra không nên nằm trên thanh địa chỉ để người khác đọc hay chia sẻ nhầm.
    */
    const maLoi = (error.response?.data as { errorCode?: string } | undefined)?.errorCode

    if (error.response?.status === 401 && maLoi === 'PHIEN_DA_BI_DAY_RA') {
      try {
        sessionStorage.setItem('lms_ly_do_thoat', maLoi)
      } catch { /* chế độ riêng tư chặn storage — vẫn phải đá ra được */ }
      xoaToken()
      window.location.href = '/dang-nhap'
      return Promise.reject(error)
    }

    const canLamMoi =
      error.response?.status === 401 &&
      config &&
      !config._daThuLai &&
      !config.url?.includes('/auth/')

    if (!canLamMoi) return Promise.reject(error)

    config._daThuLai = true

    dangLamMoi ??= lamMoiToken().finally(() => {
      dangLamMoi = null
    })

    const tokenMoi = await dangLamMoi

    if (!tokenMoi) {
      // Hết đường cứu: về màn đăng nhập.
      window.location.href = '/dang-nhap'
      return Promise.reject(error)
    }

    config.headers.Authorization = `Bearer ${tokenMoi}`
    return api(config)
  },
)

/** Mã lỗi backend trả về — frontend tự dịch (quy tắc #3). */
export function layMaLoi(error: unknown): string {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as { errorCode?: string } | undefined
    if (data?.errorCode) return data.errorCode
  }
  return 'LOI_HE_THONG'
}

/**
 * Dữ liệu kèm theo mã lỗi (`AppException.DuLieu` ở backend) — để frontend dựng câu thông báo
 * cụ thể mà API vẫn chỉ trả MÃ LỖI, không hard-code tiếng Việt (quy tắc #3).
 *
 * Ví dụ `KHOA_HOC_KHONG_KHOP_LOP` mang theo tên khoá của đơn và tên khoá của lớp, nên người
 * duyệt đọc được "lệch ở đâu" thay vì chỉ "không khớp".
 */
export function layDuLieuLoi(error: unknown): Record<string, unknown> | null {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as { duLieu?: Record<string, unknown> } | undefined
    if (data?.duLieu) return data.duLieu
  }
  return null
}

/**
 * Kết quả phân trang từ API.
 *
 * Bốn endpoint danh sách (`/cau-thu`, `/tai-khoan`, `/doi-thu`, `/tran-dau`) trả về hình dạng
 * này thay vì mảng trần. Các endpoint còn lại (vd: nhóm quyền) vẫn trả
 * mảng vì số lượng bị chặn tự nhiên bởi nghiệp vụ.
 */
export interface KetQuaTrang<T> {
  duLieu: T[]
  tongSoDong: number
  trang: number
  soDong: number
  tongSoTrang: number
}

/** Tham số phân trang gửi lên dạng query string. */
export interface ThamSoTrang {
  trang?: number
  soDong?: number
}

/** Trang rỗng — dùng làm giá trị mặc định để component không phải kiểm null ở mọi chỗ. */
export function trangRong<T>(): KetQuaTrang<T> {
  return { duLieu: [], tongSoDong: 0, trang: 1, soDong: 20, tongSoTrang: 1 }
}
