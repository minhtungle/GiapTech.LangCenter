import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider, useAuth } from '@/lib/auth'
import Layout from '@/components/Layout'
import DangNhap from '@/pages/DangNhap'
import DoiMatKhau from '@/pages/DoiMatKhau'
import QuenMatKhau from '@/pages/QuenMatKhau'
import DangKyTrungTam from '@/pages/DangKyTrungTam'
import TongQuan from '@/pages/TongQuan'
import LopHoc from '@/pages/dao-tao/LopHoc'
import TaiLieu from '@/pages/dao-tao/TaiLieu'
import HocPhi from '@/pages/dao-tao/HocPhi'
import NguoiDungVaTaiKhoan from '@/pages/quan-tri/NguoiDungVaTaiKhoan'
import PhanQuyen from '@/pages/quan-tri/PhanQuyen'
import ThietLap from '@/pages/quan-tri/ThietLap'

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

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AuthProvider>
          <Routes>
            <Route path="/dang-nhap" element={<DangNhap />} />
            <Route path="/quen-mat-khau" element={<QuenMatKhau />} />
            <Route path="/dang-ky" element={<DangKyTrungTam />} />
            <Route path="/doi-mat-khau" element={<DoiMatKhau />} />

            <Route
              element={
                <CanDangNhap>
                  <Layout />
                </CanDangNhap>
              }
            >
              <Route path="/" element={<TongQuan />} />
              <Route path="/lop-hoc" element={<LopHoc />} />
              <Route path="/tai-lieu" element={<TaiLieu />} />
              <Route path="/hoc-phi" element={<HocPhi />} />
              <Route path="/quan-tri/tai-khoan" element={<NguoiDungVaTaiKhoan />} />
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
