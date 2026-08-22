import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider, useAuth } from '@/lib/auth'
import Layout from '@/components/Layout'
import DangNhap from '@/pages/DangNhap'
import DoiMatKhau from '@/pages/DoiMatKhau'
import QuenMatKhau from '@/pages/QuenMatKhau'
import DangKyClb from '@/pages/DangKyClb'
import LichThiDau from '@/pages/lich-thi-dau/LichThiDau'
import DoiThu from '@/pages/lich-thi-dau/DoiThu'
import CongDong from '@/pages/cong-dong/CongDong'
import ChiTietClb from '@/pages/cong-dong/ChiTietClb'
import XemLoiMoiLink from '@/pages/moi-qua-link/XemLoiMoiLink'
import DangKyNhanh from '@/pages/dang-ky-nhanh/DangKyNhanh'
import TongQuan from '@/pages/TongQuan'
import ChiTietTran from '@/pages/lich-thi-dau/ChiTietTran'
import HomThu from '@/pages/lich-thi-dau/HomThu'
import MauDoiHinh from '@/pages/lich-thi-dau/MauDoiHinh'
import ThuVienVideo from '@/pages/lich-thi-dau/ThuVienVideo'
import TaiChinh from '@/pages/tai-chinh/TaiChinh'
import ThongKe from '@/pages/thong-ke/ThongKe'
import CauThu from '@/pages/quan-tri/CauThu'
import TaiKhoan from '@/pages/quan-tri/TaiKhoan'
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
            <Route path="/dang-ky" element={<DangKyClb />} />
            {/* Trang xem lời mời qua link — CÔNG KHAI (FR-18): người nhận có thể chưa có tài
                khoản. Đặt cùng nhóm với /dang-nhap, ngoài <CanThietDangNhap>. */}
            <Route path="/loi-moi" element={<XemLoiMoiLink />} />
            {/* FR-19 — trang đăng ký nhanh, KHÔNG cần đăng nhập (người dùng chưa có tài khoản). */}
            <Route path="/dang-ky-nhanh" element={<DangKyNhanh />} />
            <Route path="/doi-mat-khau" element={<DoiMatKhau />} />

            <Route
              element={
                <CanDangNhap>
                  <Layout />
                </CanDangNhap>
              }
            >
              <Route path="/" element={<TongQuan />} />
              <Route path="/lich-thi-dau" element={<LichThiDau />} />
              <Route path="/lich-thi-dau/:id" element={<ChiTietTran />} />
              <Route path="/mau-doi-hinh" element={<MauDoiHinh />} />
              <Route path="/thu-vien-video" element={<ThuVienVideo />} />
              <Route path="/hom-thu" element={<HomThu />} />
              <Route path="/doi-thu" element={<DoiThu />} />
              <Route path="/cong-dong" element={<CongDong />} />
              <Route path="/cong-dong/:maDoi" element={<ChiTietClb />} />
              <Route path="/thong-ke" element={<ThongKe />} />
              <Route path="/tai-chinh" element={<TaiChinh />} />
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
