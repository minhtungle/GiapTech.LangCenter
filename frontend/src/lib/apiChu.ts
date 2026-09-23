import axios from 'axios'

/**
 * Client HTTP riêng cho **site chủ hệ thống** (ADR-0009).
 *
 * ## Vì sao KHÔNG dùng chung `api.ts`
 *
 * `api.ts` mang cả một bộ máy của phiên tenant: interceptor tự làm mới token qua
 * `/auth/lam-moi-token`, khử đua, và đá về `/dang-nhap` khi 401. Tài khoản chủ **không có
 * refresh token** và **không thuộc tenant nào**, nên dùng chung sẽ:
 *
 * - kích hoạt luồng làm mới vô nghĩa mỗi lần token hết hạn, rồi đá người dùng về màn đăng
 *   nhập của TENANT — sai màn;
 * - và nguy hiểm hơn: hai loại danh tính dùng chung một biến token trong RAM, nên mở site chủ
 *   ở cùng tab với một phiên tenant là hai bên ghi đè nhau.
 *
 * Tách client là cách rẻ nhất để hai danh tính không thể lẫn vào nhau.
 *
 * ## Token giữ trong RAM, không `localStorage`
 *
 * Cùng lý do với ADR-0007: `localStorage` đọc được bằng JS cùng origin. Tài khoản chủ còn
 * đáng giá hơn tài khoản tenant — chiếm được là tạo tenant, đổi domain, cấp lại mật khẩu
 * admin của mọi trung tâm.
 *
 * Đánh đổi đã biết và **chấp nhận**: tải lại trang là phải đăng nhập lại. Site này dùng thưa
 * (tạo trung tâm, gắn domain) nên phiền ít; đổi lại không có refresh token nào để bị trộm.
 */
let tokenChu: string | null = null

export const luuTokenChu = (token: string) => {
  tokenChu = token
}

export const xoaTokenChu = () => {
  tokenChu = null
}

export const layTokenChu = () => tokenChu

export const apiChu = axios.create({ baseURL: '/api/v1/chu-he-thong' })

apiChu.interceptors.request.use((config) => {
  if (tokenChu) config.headers.Authorization = `Bearer ${tokenChu}`
  return config
})

/**
 * 401 ⇒ xoá token và để component tự điều hướng.
 *
 * KHÔNG tự `window.location` như `api.ts`: ở đây chỉ có một màn đăng nhập và một màn danh
 * sách, nên React Router xử lý gọn hơn — và tránh reload cả trang làm mất trạng thái form.
 */
apiChu.interceptors.response.use(
  (res) => res,
  (err) => {
    if (axios.isAxiosError(err) && err.response?.status === 401) xoaTokenChu()
    return Promise.reject(err)
  },
)
