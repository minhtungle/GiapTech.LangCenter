import * as React from 'react'
import { api, layAccessToken, luuToken, xoaToken } from './api'

interface PhienDangNhap {
  nguoiDungId: string
  username: string
  tenantId: string
  maDoi: string
}

interface AuthContextValue {
  phien: PhienDangNhap | null
  daDangNhap: boolean
  dangNhap: (maDoi: string, username: string, matKhau: string) => Promise<{ phaiDoiMatKhau: boolean }>
  dangXuat: () => void
  /** Gọi sau khi đổi mật khẩu để gỡ trạng thái "phải đổi". */
  danhDauDaDoiMatKhau: () => void
  phaiDoiMatKhau: boolean
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
      maDoi: claims.ma_doi ?? '',
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

  const dangNhap = React.useCallback(
    async (maDoi: string, username: string, matKhau: string) => {
      const { data } = await api.post('/auth/dang-nhap', { maDoi, username, matKhau })
      luuToken(data.accessToken, data.refreshToken)
      setPhien(docPhienTuToken(data.accessToken))
      setPhaiDoiMatKhau(Boolean(data.phaiDoiMatKhau))
      return { phaiDoiMatKhau: Boolean(data.phaiDoiMatKhau) }
    },
    [],
  )

  const dangXuat = React.useCallback(() => {
    xoaToken()
    setPhien(null)
    setPhaiDoiMatKhau(false)
  }, [])

  const danhDauDaDoiMatKhau = React.useCallback(() => setPhaiDoiMatKhau(false), [])

  const value = React.useMemo(
    () => ({
      phien,
      daDangNhap: phien !== null,
      dangNhap,
      dangXuat,
      danhDauDaDoiMatKhau,
      phaiDoiMatKhau,
    }),
    [phien, dangNhap, dangXuat, danhDauDaDoiMatKhau, phaiDoiMatKhau],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = React.useContext(AuthContext)
  if (!ctx) throw new Error('useAuth phải nằm trong AuthProvider')
  return ctx
}
