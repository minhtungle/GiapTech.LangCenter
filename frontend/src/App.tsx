import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter, Navigate, Route, Routes, useLocation } from 'react-router-dom'
import { AuthProvider, useAuth } from '@/lib/auth'
import Layout from '@/components/Layout'
import DangNhap from '@/pages/DangNhap'
import DoiMatKhau from '@/pages/DoiMatKhau'
import DangNhapChu from '@/pages/chu-he-thong/DangNhapChu'
import DanhSachTrungTam from '@/pages/chu-he-thong/DanhSachTrungTam'
import NoiDungTrangDich from '@/pages/ldp/NoiDungTrangDich'
import LienHeTrangDich from '@/pages/ldp/LienHeTrangDich'
import TrangCongKhai from '@/pages/ldp/TrangCongKhai'
import QuenMatKhau from '@/pages/QuenMatKhau'
import DangKyTrungTam from '@/pages/DangKyTrungTam'
import TongQuan from '@/pages/TongQuan'
import LopHoc from '@/pages/dao-tao/LopHoc'
import ChiTietLopHoc from '@/pages/dao-tao/ChiTietLopHoc'
import ChoXepLop from '@/pages/dao-tao/ChoXepLop'
import ChiTietBuoiHoc from '@/pages/dao-tao/ChiTietBuoiHoc'
import DoanhThu from '@/pages/crm/DoanhThu'
import ThongKeCrm from '@/pages/crm/ThongKe'
import KhachHang from '@/pages/crm/KhachHang'
import ChiTietKhachHang from '@/pages/crm/ChiTietKhachHang'
import KhoaHoc from '@/pages/crm/KhoaHoc'
import SanPham from '@/pages/crm/SanPham'
import KhoaOnline from '@/pages/dao-tao/KhoaOnline'
import ChiTietKhoaOnline from '@/pages/dao-tao/ChiTietKhoaOnline'
import TaiLieu from '@/pages/dao-tao/TaiLieu'
import HocPhi from '@/pages/dao-tao/HocPhi'
import TaiKhoan from '@/pages/quan-tri/TaiKhoan'
import Hrm from '@/pages/hrm/Hrm'
import ChiTietNhanSu from '@/pages/hrm/ChiTietNhanSu'
import ThongKeNhanSu from '@/pages/hrm/ThongKeNhanSu'
import TieuChiDanhGia from '@/pages/hrm/TieuChiDanhGia'
import PhanQuyen from '@/pages/quan-tri/PhanQuyen'
import ChiTietQuyen from '@/pages/quan-tri/phan-quyen/ChiTietQuyen'
import ThietLap from '@/pages/quan-tri/ThietLap'
import NhatKy from '@/pages/quan-tri/NhatKy'
import { DangXuLy } from '@/components/ui/DangXuLy'
import { LinhVat } from '@/components/LinhVat'

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

/**
 * Chuyển đường LMS cũ (không tiền tố) sang `/lms/...`, **giữ nguyên phần còn lại của URL**.
 *
 * `/lop-hoc/abc-123?tab=lich` → `/lms/lop-hoc/abc-123?tab=lich`.
 *
 * `replace` để nút Back không kẹt vòng: không có nó thì bấm Back từ trang mới sẽ về đường cũ,
 * đường cũ lại chuyển tiếp sang mới — người dùng bấm Back mãi không ra được.
 */
function DoiSangLms() {
  const { pathname, search, hash } = useLocation()
  return <Navigate to={`/lms${pathname}${search}${hash}`} replace />
}

/** Chặn route cần đăng nhập; còn cờ buộc đổi mật khẩu thì ép về màn đổi. */
function CanDangNhap({ children }: { children: React.ReactNode }) {
  const { daDangNhap, dangKhoiPhuc, phaiDoiMatKhau } = useAuth()

  /*
    CHỜ khôi phục xong rồi mới quyết (ADR-0007).

    Từ 22/09/2026 access token nằm trong RAM, nên ngay sau khi F5 thì `daDangNhap` là `false`
    dù người dùng vẫn đang đăng nhập — cookie `httpOnly` còn đó, chỉ là app chưa kịp đổi lấy
    access token mới. Không chờ thì **mỗi lần F5 là văng về màn đăng nhập**, rồi một nhịp sau
    lại tự vào — nhấp nháy và mất luôn trang đang xem.

    Trả `null` (không vẽ gì) thay vì một spinner: khoảng chờ chỉ là một request, spinner nhấp
    nháy trong 100ms còn khó chịu hơn màn trắng.
  */
  if (dangKhoiPhuc) return null

  if (!daDangNhap) return <Navigate to="/dang-nhap" replace />
  if (phaiDoiMatKhau) return <Navigate to="/doi-mat-khau" replace />
  return <>{children}</>
}

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      {/*
        Thanh tiến trình và linh vật đặt NGOÀI `Routes` để không bị dựng lại mỗi lần đổi
        trang: dựng lại nghĩa là linh vật nhảy về đầu màn và bộ đếm thoại reset ở mọi cú
        bấm menu.

        Trong `QueryClientProvider` vì `DangXuLy` đọc `useIsFetching`/`useIsMutating`.
      */}
      <DangXuLy />

      <BrowserRouter>
        <AuthProvider>
          <LinhVat />

          <Routes>
            <Route path="/dang-nhap" element={<DangNhap />} />
            <Route path="/quen-mat-khau" element={<QuenMatKhau />} />
            <Route path="/dang-ky" element={<DangKyTrungTam />} />
            <Route path="/doi-mat-khau" element={<DoiMatKhau />} />

            {/*
              Site CHỦ HỆ THỐNG (ADR-0009) — nằm NGOÀI `<CanDangNhap>` vì nó dùng một loại
              danh tính khác hẳn: token chủ không mang `tenant_id`, và giữ trong một biến
              riêng (`apiChu.ts`) để hai phiên không ghi đè nhau khi mở cùng tab.

              Cũng KHÔNG bọc `<Layout />`: Layout gọi `useQuyen`/`useHeThong` — hai thứ tra
              quyền theo tenant, mà tài khoản chủ không thuộc tenant nào.
            */}
            <Route path="/chu" element={<DangNhapChu />} />
            <Route path="/chu/trung-tam" element={<DanhSachTrungTam />} />

            {/*
              TRANG ĐÍCH CÔNG KHAI (FR-30) — khách vãng lai, KHÔNG đăng nhập.

              Ngoài `<CanDangNhap>` và không bọc `<Layout />`: Layout gọi useQuyen/useHeThong,
              hai thứ cần token. Đây cũng là lý do trang dùng axios trần thay vì `api.ts`.

              Hai đường: `/t/{mã}` cho trung tâm chưa trỏ DNS, và `/` khi vào bằng domain
              riêng — lúc đó server tự giải tenant từ domain (ADR-0008) nên không cần mã.
            */}
            <Route path="/t/:ma" element={<TrangCongKhai />} />

            <Route
              element={
                <CanDangNhap>
                  <Layout />
                </CanDangNhap>
              }
            >
              <Route path="/" element={<TongQuan />} />
              {/* HRM · CRM — hiện là khung trống, xem components/ui/DangPhatTrien.tsx */}
              {/*
                HRM gộp thành MỘT trang ba tab (16/09/2026) — xem `pages/hrm/Hrm.tsx`.

                Hai đường cũ chuyển hướng sang tab tương ứng, KHÔNG xoá: link đã lưu, bookmark
                và tài liệu cũ vẫn phải mở được (quy tắc #1 tinh thần — không làm hỏng thứ
                người dùng đang có). `replace` để Back không kẹt ở đường cũ.
              */}
              <Route path="/hrm" element={<Hrm />} />
              <Route
                path="/hrm/co-cau"
                element={<Navigate to="/hrm?tab=so-do" replace />}
              />
              <Route
                path="/hrm/nhan-su"
                element={<Navigate to="/hrm?tab=nhan-su" replace />}
              />
              <Route path="/hrm/nhan-su/:id" element={<ChiTietNhanSu />} />
              <Route
                path="/hrm/chuc-vu"
                element={<Navigate to="/hrm?tab=chuc-vu" replace />}
              />

              {/*
                Thống kê nhân sự và Tiêu chí đánh giá là MODULE RIÊNG, không phải tab của `/hrm`
                (tách 16/09/2026 theo yêu cầu chủ sản phẩm).

                Ba tab còn lại ở `/hrm` nói về **cùng một tập người** nên gộp là đúng; còn hai
                màn này khác hẳn: một là báo cáo toàn trung tâm, một là cấu hình danh mục — và
                mỗi cái gác bằng **quyền riêng**, nên nhồi vào `/hrm` (gác `NhanSu`) thì người có
                quyền xem thống kê mà không có quyền hồ sơ nhân sự sẽ không thấy mục nào để vào.

                Đường `?tab=` cũ vẫn chuyển hướng tới đây — link đã lưu phải mở được.
              */}
              <Route path="/hrm/thong-ke" element={<ThongKeNhanSu />} />
              <Route path="/hrm/tieu-chi-danh-gia" element={<TieuChiDanhGia />} />
              <Route path="/crm/khach-hang" element={<KhachHang />} />
              <Route path="/crm/thong-ke" element={<ThongKeCrm />} />
              <Route path="/crm/khach-hang/:id" element={<ChiTietKhachHang />} />
              <Route path="/crm/doanh-thu" element={<DoanhThu />} />
              <Route path="/crm/khoa-hoc" element={<KhoaHoc />} />
              <Route path="/crm/san-pham" element={<SanPham />} />

              {/* LMS — tiền tố `/lms` cho đồng nhất với `/hrm`, `/crm` (10/09/2026). */}
              <Route path="/lms/lop-hoc" element={<LopHoc />} />
              <Route path="/lms/lop-hoc/cho-xep-lop" element={<ChoXepLop />} />
              <Route path="/lms/lop-hoc/:id" element={<ChiTietLopHoc />} />
              <Route path="/lms/buoi-hoc/:id" element={<ChiTietBuoiHoc />} />
              <Route path="/lms/khoa-online" element={<KhoaOnline />} />
              <Route path="/lms/khoa-online/:id" element={<ChiTietKhoaOnline />} />
              {/* LDP — FR-30 */}
              <Route path="/ldp/noi-dung" element={<NoiDungTrangDich />} />
              <Route path="/ldp/lien-he" element={<LienHeTrangDich />} />
              <Route path="/lms/tai-lieu" element={<TaiLieu />} />
              {/*
                Màn Học phí KHÔNG còn trong sidebar và không còn là tab của lớp (12/09/2026):
                LMS không quản lý, không hiển thị tiền học nữa — chỉ CRM nắm số tiền.

                Route giữ lại có chủ ý: bookmark và link đã gửi không chết, và bật lại chỉ cần
                thêm một dòng menu. Endpoint `/hoc-phi` vẫn gác bằng `HocPhi.Xem` như cũ.
              */}
              <Route path="/lms/hoc-phi" element={<HocPhi />} />

              {/*
                Đường LMS CŨ (không tiền tố) → chuyển sang `/lms/...`.

                Bookmark và link đã gửi cho nhau vẫn phải mở được: không có mấy dòng này thì
                route `*` ở dưới sẽ lặng lẽ đẩy về Tổng quan, người dùng tưởng mất dữ liệu.

                Dùng `<DoiSangLms>` chứ không `<Navigate to="...">` cố định: `to` tĩnh sẽ mất
                `:id` và query (`?tab=lich`), tức link tới đúng một lớp/buổi cụ thể sẽ rơi về
                danh sách.
              */}
              {['/lop-hoc', '/buoi-hoc', '/tai-lieu', '/hoc-phi'].map((cu) => (
                <Route key={cu} path={`${cu}/*`} element={<DoiSangLms />} />
              ))}
              <Route path="/quan-tri/tai-khoan" element={<TaiKhoan />} />
              <Route path="/quan-tri/phan-quyen" element={<PhanQuyen />} />
              <Route path="/quan-tri/phan-quyen/:id" element={<ChiTietQuyen />} />
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
