import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios'

const KHOA_ACCESS = 'sr_access_token'
const KHOA_REFRESH = 'sr_refresh_token'

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

/** Mã lỗi backend trả về — frontend tự dịch (quy tắc #2). */
export function layMaLoi(error: unknown): string {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as { errorCode?: string } | undefined
    if (data?.errorCode) return data.errorCode
  }
  return 'LOI_HE_THONG'
}
