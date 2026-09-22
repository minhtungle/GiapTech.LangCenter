import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios'

/*
  TOKEN KHÔNG CÒN Ở `localStorage` — ADR-0007 (22/09/2026).

  - **Refresh token** đi bằng cookie `httpOnly` do server đặt. JavaScript **không đọc được**,
    kể cả mã ở đây; trình duyệt tự gửi kèm khi gọi `/api/v1/auth/*`.
  - **Access token** nằm trong biến dưới đây, tức trong RAM của tab.

  Vì sao: `localStorage` đọc được bằng JS cùng origin, nên một lỗ XSS — hoặc một gói npm bị
  chiếm, thực tế hơn nhiều — lấy được refresh token và mạo danh **30 ngày**. Xoay vòng token
  không cứu được vì kẻ tấn công cũng xoay vòng theo.

  Đánh đổi đã biết: **tải lại trang là mất access token**, nên `AuthProvider` gọi
  `khoiPhucPhien()` lúc khởi động để đổi cookie lấy access token mới.
*/
let accessToken: string | null = null

/** Token CSRF (double-submit) — gửi lại trong header khi gọi endpoint nhận cookie phiên. */
let tokenCsrf: string | null = null

export const luuToken = (access: string, csrf: string) => {
  accessToken = access
  tokenCsrf = csrf
}

export const xoaToken = () => {
  accessToken = null
  tokenCsrf = null
}

export const layAccessToken = () => accessToken

/*
  Cửa cho TEST E2E lấy access token đang giữ trong RAM.

  Vì sao cần: từ ADR-0007 token không còn ở `localStorage`, nên test không đọc ra được nữa. Mà
  test **không thể** tự đăng nhập lại qua API để lấy token — đăng nhập lần hai sẽ **đẩy phiên
  của trình duyệt ra** (một phiên mỗi tài khoản, 20/09/2026) và chính trang đang test bị đá về
  màn đăng nhập. Đã gặp đúng vậy: 17 test đỏ.

  Vì sao KHÔNG phải lỗ hổng: đây chỉ là đọc lại thứ mà mã trong trang vốn đã giữ trong biến.
  Kẻ tấn công chạy được JavaScript trong trang thì cũng đọc được biến đó bằng cách khác — hàm
  này không mở thêm quyền gì. Thứ ADR-0007 bảo vệ là **refresh token**, và nó nằm trong cookie
  `httpOnly` mà hàm này không chạm tới.

  Gắn vào `window` chỉ khi chạy dev/test (`import.meta.env.DEV`), nên bản build production
  không có nó.
*/
if (import.meta.env.DEV) {
  ;(window as unknown as { __layTokenTest?: () => string | null }).__layTokenTest =
    () => accessToken
}

/** Tên header double-submit — phải khớp `CookiePhien.HeaderCsrf` bên backend. */
const HEADER_CSRF = 'X-CSRF-Token'

/*
  `withCredentials: true` cho TOÀN BỘ instance (ADR-0007).

  Cần cho cả hai chiều: nhận `Set-Cookie` lúc đăng nhập, và gửi cookie kèm lúc gọi
  `lam-moi-token`/`dang-xuat`. Đặt riêng ở lời gọi làm mới là chưa đủ — thiếu ở `dang-nhap` thì
  trình duyệt **không lưu cookie phiên**, và mọi thứ sau đó hỏng theo kiểu khó lần: đăng nhập
  báo thành công, nhưng F5 hoặc mở tab mới là văng về màn đăng nhập vì không có cookie để khôi
  phục. Đã gặp đúng vậy khi chạy E2E lần đầu sau khi chuyển sang cookie.

  Cùng origin (Nginx phục vụ cả frontend lẫn `/api`) nên cờ này không kéo theo ràng buộc CORS
  nào.
*/
export const api = axios.create({ baseURL: '/api/v1', withCredentials: true })

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
  try {
    /*
      KHÔNG gửi refresh token trong body nữa (ADR-0007) — trình duyệt tự đính cookie `httpOnly`.
      `withCredentials: true` là bắt buộc để axios cho phép gửi cookie kèm.

      Vẫn dùng axios trần, không qua `api`: đi qua interceptor thì lỗi 401 của chính request
      làm mới sẽ kích hoạt vòng lặp làm mới vô tận.
    */
    const { data } = await axios.post('/api/v1/auth/lam-moi-token', null, {
      withCredentials: true,
      // Cookie CSRF đọc được, nhưng dùng giá trị đang giữ trong RAM khi có — cùng một giá trị,
      // mà không phải phân tích chuỗi `document.cookie`.
      headers: { [HEADER_CSRF]: tokenCsrf ?? docCookieCsrf() ?? '' },
    })
    luuToken(data.accessToken, data.tokenCsrf)
    return data.accessToken
  } catch (e) {
    /*
      GIỮ LÝ DO bị đá ra (ADR-0007).

      Từ khi refresh token đi bằng cookie, phiên bị đẩy ra lộ diện **ở đây** chứ không còn ở
      interceptor 401: backend thu hồi refresh token ngay lúc người kia đăng nhập, nên lời gọi
      làm mới này thất bại trước. Nuốt lỗi trơn thì người dùng về màn đăng nhập **trắng trơn**,
      không hiểu vì sao đang dùng thì bị văng — đúng cái mà thông báo này sinh ra để tránh.

      Chỉ ghi khi backend nói rõ là bị đẩy ra; lỗi mạng hay "chưa từng đăng nhập" thì im lặng.
    */
    const maLoi = axios.isAxiosError(e)
      ? (e.response?.data as { errorCode?: string } | undefined)?.errorCode
      : undefined

    if (maLoi === 'PHIEN_DA_BI_DAY_RA') {
      try {
        sessionStorage.setItem('lms_ly_do_thoat', maLoi)
      } catch { /* chế độ riêng tư chặn storage — vẫn phải đá ra được */ }
    }

    xoaToken()
    return null
  }
}

/**
 * Đọc cookie CSRF (**không** `httpOnly` nên JS đọc được — cố ý, xem `CookiePhien`).
 *
 * Cần khi mở tab mới: RAM trống nhưng cookie vẫn còn, và lần gọi `lam-moi-token` đầu tiên phải
 * có header CSRF khớp thì server mới chấp nhận.
 */
function docCookieCsrf(): string | null {
  const khop = document.cookie.match(/(?:^|;\s*)lms_csrf=([^;]*)/)
  return khop ? decodeURIComponent(khop[1]) : null
}

/**
 * Khôi phục phiên lúc mở app — đổi cookie `httpOnly` lấy access token mới.
 *
 * Cần vì access token nằm trong RAM: F5 hay mở tab mới là mất. Trả `null` khi không có phiên
 * hợp lệ (chưa đăng nhập, cookie hết hạn, token đã bị thu hồi).
 *
 * ## PHẢI đi qua `dangLamMoi` — không được gọi thẳng `lamMoiToken()`
 *
 * Refresh token **xoay vòng**: token cũ chết ngay khi đổi, và dùng lại token đã thu hồi bị
 * backend hiểu là **bị đánh cắp** ⇒ thu hồi TOÀN BỘ phiên (`LamMoiTokenCommand`).
 *
 * Nên hai lời gọi song song là tự đá mình ra: lời gọi thứ hai cầm token vừa bị lời gọi thứ
 * nhất thu hồi. Xảy ra thật ngay lần chạy E2E đầu sau ADR-0007 — `React.StrictMode` gọi
 * `useEffect` **hai lần** ở dev, và người dùng bị đăng xuất mỗi lần F5.
 *
 * `dangLamMoi` vốn đã có sẵn để chống đúng tình huống này ở phía interceptor; dùng chung nó
 * cho cả đường khởi động thì một lần mở app chỉ sinh **một** request, dù có bao nhiêu chỗ gọi.
 */
export async function khoiPhucPhien(): Promise<string | null> {
  /*
    ĐỪNG gọi làm mới nếu phiên trước đã bị đẩy ra.

    Backend coi "dùng lại refresh token đã thu hồi" là dấu hiệu **bị đánh cắp** và thu hồi
    TOÀN BỘ phiên của tài khoản — kể cả token của người vừa đăng nhập ở máy khác. Nên phiên cũ
    gọi làm mới không chỉ vô ích, mà còn **đá luôn người vừa đăng nhập ra**: hai người cùng
    văng, không ai vào được.

    Chốt chặn đó là đúng và không nới lỏng (xem `LamMoiTokenCommand`); chỗ phải sửa là đây —
    người đã biết mình bị đẩy ra thì không gõ cửa nữa. Cờ nằm ở `sessionStorage` nên sống qua
    lần tải lại trang, và `DangNhap` xoá nó sau khi hiện thông báo.
  */
  try {
    if (sessionStorage.getItem('lms_ly_do_thoat') === 'PHIEN_DA_BI_DAY_RA') return null
  } catch { /* chế độ riêng tư chặn storage — cứ thử làm mới như bình thường */ }

  dangLamMoi ??= lamMoiToken().finally(() => {
    dangLamMoi = null
  })
  return dangLamMoi
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
