import * as React from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { api, layAccessToken, luuToken, xoaToken } from './api'

interface PhienDangNhap {
  nguoiDungId: string
  username: string
  tenantId: string
  maTrungTam: string
  tenTrungTam: string
}

interface AuthContextValue {
  phien: PhienDangNhap | null
  daDangNhap: boolean
  dangNhap: (maTrungTam: string, username: string, matKhau: string) => Promise<{ phaiDoiMatKhau: boolean }>
  dangXuat: () => void
  /** Gọi sau khi đổi mật khẩu để gỡ trạng thái "phải đổi". */
  danhDauDaDoiMatKhau: () => void
  phaiDoiMatKhau: boolean
  /**
   * Cập nhật tên đội hiển thị sau khi sửa ở FR-06. Tên nằm trong JWT nên token đang cầm
   * vẫn mang tên cũ cho tới lần làm mới kế tiếp — không đồng bộ thì sidebar hiện tên cũ
   * dù người dùng vừa đổi xong.
   */
  capNhatTenTrungTam: (tenTrungTam: string) => void
}

const AuthContext = React.createContext<AuthContextValue | null>(null)

/** Giải mã payload JWT (không xác minh chữ ký — việc đó là của backend). */
function docPhienTuToken(token: string | null): PhienDangNhap | null {
  if (!token) return null
  try {
    const payload = token.split('.')[1]
    const json = atob(payload.replace(/-/g, '+').replace(/_/g, '/'))
    const claims = JSON.parse(decodeURIComponent(escape(json))) as Record<string, string>

    const exp = Number(claims.exp)
    if (exp && Date.now() / 1000 > exp) return null

    return {
      nguoiDungId:
        claims['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'] ?? '',
      username: claims['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'] ?? '',
      tenantId: claims.tenant_id ?? '',
      maTrungTam: claims.ma_trung_tam ?? '',
      tenTrungTam: claims.ten_trung_tam ?? '',
    }
  } catch {
    return null
  }
}

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [phien, setPhien] = React.useState<PhienDangNhap | null>(() =>
    docPhienTuToken(layAccessToken()),
  )
  const [phaiDoiMatKhau, setPhaiDoiMatKhau] = React.useState(false)

  const qc = useQueryClient()

  /*
    XOÁ SẠCH cache khi đổi phiên (17/09/2026).

    Mọi `queryKey` trong app đều là hằng — `['toi-quyen']`, `['toi-he-thong']`,
    `['nguoi-dung-ngan']`… — tức **không mang danh tính người đăng nhập**. Cộng với
    `staleTime: Infinity` ở `useQuyen`, đăng xuất rồi đăng nhập nick khác sẽ dùng lại nguyên
    cache của nick cũ: học viên nhìn thấy menu của quản trị (Phân quyền, Nhật ký hệ thống), phải
    Ctrl+Shift+R mới đúng. Người dùng báo 17/09/2026; tôi tái hiện được đúng như vậy.

    **Không rò rỉ dữ liệu** — backend vẫn 403 mọi endpoint ngoài quyền. Nhưng hiện menu mà bấm
    vào nhận lỗi là sai với người dùng, và với dữ liệu nghiệp vụ đã tải sẵn (danh sách người,
    phòng ban…) thì đó là dữ liệu của phiên trước còn nằm trên màn.

    Chữa ở ĐÂY, không đi sửa từng `queryKey`: thêm `username` vào mọi khoá là ~20 chỗ phải nhớ,
    và chỗ thứ 21 thêm sau này sẽ quên. `qc.clear()` là một dòng, đúng ngữ nghĩa "phiên mới thì
    không còn gì của phiên cũ", và không thể quên vì chỉ có một chỗ đổi phiên.
  */
  const dangNhap = React.useCallback(
    async (maTrungTam: string, username: string, matKhau: string) => {
      const { data } = await api.post('/auth/dang-nhap', { maTrungTam, username, matKhau })
      // Xoá TRƯỚC khi đặt phiên mới: đặt phiên làm các query `enabled: daDangNhap` chạy ngay,
      // clear sau đó sẽ vứt luôn kết quả vừa tải và chúng phải gọi lại lần hai.
      qc.clear()
      luuToken(data.accessToken, data.refreshToken)
      setPhien(docPhienTuToken(data.accessToken))
      setPhaiDoiMatKhau(Boolean(data.phaiDoiMatKhau))
      return { phaiDoiMatKhau: Boolean(data.phaiDoiMatKhau) }
    },
    [qc],
  )

  const dangXuat = React.useCallback(() => {
    xoaToken()
    setPhien(null)
    setPhaiDoiMatKhau(false)
    // Cũng xoá ở đây, không chỉ ở `dangNhap`: người dùng đăng xuất rồi bỏ đi, màn đăng nhập vẫn
    // giữ cache của họ trong bộ nhớ trình duyệt cho tới khi đóng tab.
    //
    // THỪA CÓ CHỦ Ý — đừng gỡ vì thấy `dangNhap` đã xoá rồi. Mutation test 17/09 cho thấy gỡ
    // MỘT trong hai chỗ thì `doi-nick-khong-giu-quyen-cu.spec.ts` vẫn xanh (chỉ gỡ CẢ HAI mới
    // đỏ), nên không có test nào giữ dòng này. Hai chỗ lo hai việc khác nhau:
    //   · `dangNhap` lo ĐÚNG — kể cả khi vào thẳng không qua `dangXuat` (phiên hết hạn ở tab
    //     khác, token bị thu hồi, mở lại app rồi đăng nhập nick khác).
    //   · chỗ này lo KÍN — máy dùng chung: đăng xuất xong đứng dậy đi, dữ liệu của người trước
    //     (danh sách nhân sự, khách hàng…) không được nằm lại trong RAM tab chờ người sau.
    qc.clear()
  }, [qc])

  const danhDauDaDoiMatKhau = React.useCallback(() => setPhaiDoiMatKhau(false), [])

  const capNhatTenTrungTam = React.useCallback(
    (tenTrungTam: string) => setPhien((p) => (p ? { ...p, tenTrungTam } : p)),
    [],
  )

  const value = React.useMemo(
    () => ({
      phien,
      daDangNhap: phien !== null,
      dangNhap,
      dangXuat,
      danhDauDaDoiMatKhau,
      phaiDoiMatKhau,
      capNhatTenTrungTam,
    }),
    [phien, dangNhap, dangXuat, danhDauDaDoiMatKhau, phaiDoiMatKhau, capNhatTenTrungTam],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = React.useContext(AuthContext)
  if (!ctx) throw new Error('useAuth phải nằm trong AuthProvider')
  return ctx
}
