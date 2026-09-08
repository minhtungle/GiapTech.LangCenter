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
import ChiTietLopHoc from '@/pages/dao-tao/ChiTietLopHoc'
import ChiTietBuoiHoc from '@/pages/dao-tao/ChiTietBuoiHoc'
import NhanVienKinhDoanh from '@/pages/hrm/NhanVienKinhDoanh'
import GiaoVienNhanSu from '@/pages/hrm/GiaoVienNhanSu'
import DoanhThu from '@/pages/crm/DoanhThu'
import KhachHang from '@/pages/crm/KhachHang'
import ChiTietKhachHang from '@/pages/crm/ChiTietKhachHang'
import KhoaHoc from '@/pages/crm/KhoaHoc'
import SanPham from '@/pages/crm/SanPham'
import TaiLieu from '@/pages/dao-tao/TaiLieu'
import HocPhi from '@/pages/dao-tao/HocPhi'
import TaiKhoan from '@/pages/quan-tri/TaiKhoan'
import NhanSu from '@/pages/hrm/NhanSu'
import HocVien from '@/pages/dao-tao/HocVien'
import PhanQuyen from '@/pages/quan-tri/PhanQuyen'
import ThietLap from '@/pages/quan-tri/ThietLap'
import NhatKy from '@/pages/quan-tri/NhatKy'

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
              {/* HRM · CRM — hiện là khung trống, xem components/ui/DangPhatTrien.tsx */}
              <Route path="/hrm/nhan-su" element={<NhanSu />} />
              <Route path="/hrm/nhan-vien-kinh-doanh" element={<NhanVienKinhDoanh />} />
              <Route path="/hrm/giao-vien" element={<GiaoVienNhanSu />} />
              <Route path="/crm/khach-hang" element={<KhachHang />} />
              <Route path="/crm/khach-hang/:id" element={<ChiTietKhachHang />} />
              <Route path="/crm/doanh-thu" element={<DoanhThu />} />
              <Route path="/crm/khoa-hoc" element={<KhoaHoc />} />
              <Route path="/crm/san-pham" element={<SanPham />} />

              <Route path="/hoc-vien" element={<HocVien />} />
              <Route path="/lop-hoc" element={<LopHoc />} />
              <Route path="/lop-hoc/:id" element={<ChiTietLopHoc />} />
              <Route path="/buoi-hoc/:id" element={<ChiTietBuoiHoc />} />
              <Route path="/tai-lieu" element={<TaiLieu />} />
              <Route path="/hoc-phi" element={<HocPhi />} />
              <Route path="/quan-tri/tai-khoan" element={<TaiKhoan />} />
              <Route path="/quan-tri/phan-quyen" element={<PhanQuyen />} />
              <Route path="/quan-tri/thiet-lap" element={<ThietLap />} />
              <Route path="/quan-tri/nhat-ky" element={<NhatKy />} />
            </Route>

            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </AuthProvider>
      </BrowserRouter>
    </QueryClientProvider>
  )
}
