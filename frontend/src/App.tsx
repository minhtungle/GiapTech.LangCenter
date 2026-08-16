import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { AuthProvider, useAuth } from '@/lib/auth'
import Layout from '@/components/Layout'
import DangNhap from '@/pages/DangNhap'
import DoiMatKhau from '@/pages/DoiMatKhau'
import QuenMatKhau from '@/pages/QuenMatKhau'
import CauThu from '@/pages/quan-tri/CauThu'
import TaiKhoan from '@/pages/quan-tri/TaiKhoan'
import PhanQuyen from '@/pages/quan-tri/PhanQuyen'
import ThietLap from '@/pages/quan-tri/ThietLap'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Không tự thử lại lỗi 401/403: interceptor đã lo làm mới token, thử lại chỉ
      // làm chậm phản hồi lỗi tới người dùng.
      retry: (soLan, loi) => {
        const status = (loi as { response?: { status?: number } })?.response?.status
        if (status === 401 || status === 403 || status === 404) return false
        return soLan < 2
      },
      staleTime: 30_000,
    },
  },
})

/** Chặn route cần đăng nhập; còn cờ buộc đổi mật khẩu thì ép về màn đổi. */
function CanDangNhap({ children }: { children: React.ReactNode }) {
  const { daDangNhap, phaiDoiMatKhau } = useAuth()

  if (!daDangNhap) return <Navigate to="/dang-nhap" replace />
  if (phaiDoiMatKhau) return <Navigate to="/doi-mat-khau" replace />
  return <>{children}</>
}

function ChuaLam({ ten }: { ten: string }) {
  return (
    <Card className="max-w-lg">
      <CardHeader>
        <CardTitle>{ten}</CardTitle>
      </CardHeader>
      <CardContent>
        <p className="text-sm text-muted-foreground">Module này chưa được triển khai.</p>
      </CardContent>
    </Card>
  )
}

function TongQuan() {
  const { phien } = useAuth()
  return (
    <Card className="max-w-lg">
      <CardHeader>
        <CardTitle>Xin chào, {phien?.username}</CardTitle>
      </CardHeader>
      <CardContent>
        <p className="text-sm text-muted-foreground">
          Bạn đang đăng nhập vào CLB <strong>{phien?.maDoi}</strong>.
        </p>
      </CardContent>
    </Card>
  )
}

export default function App() {
  const { t } = useTranslation()

  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AuthProvider>
          <Routes>
            <Route path="/dang-nhap" element={<DangNhap />} />
            <Route path="/quen-mat-khau" element={<QuenMatKhau />} />
            <Route path="/doi-mat-khau" element={<DoiMatKhau />} />

            <Route
              element={
                <CanDangNhap>
                  <Layout />
                </CanDangNhap>
              }
            >
              <Route path="/" element={<TongQuan />} />
              <Route path="/lich-thi-dau" element={<ChuaLam ten={t('menu.lichThiDau')} />} />
              <Route path="/thong-ke" element={<ChuaLam ten={t('menu.thongKe')} />} />
              <Route path="/tai-chinh" element={<ChuaLam ten={t('menu.taiChinh')} />} />
              <Route path="/quan-tri/tai-khoan" element={<TaiKhoan />} />
              <Route path="/quan-tri/cau-thu" element={<CauThu />} />
              <Route path="/quan-tri/phan-quyen" element={<PhanQuyen />} />
              <Route path="/quan-tri/thiet-lap" element={<ThietLap />} />
            </Route>

            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </AuthProvider>
      </BrowserRouter>
    </QueryClientProvider>
  )
}
