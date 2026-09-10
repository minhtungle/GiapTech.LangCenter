import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import {
  Users, ShieldCheck, Settings, LogOut, Home, GraduationCap, BookOpen, Wallet,
  PanelLeftClose, PanelLeft, Menu, X, ScrollText, Briefcase, UserCog, TrendingUp,
  LayoutGrid, Check, KeyRound, Contact, PackageOpen, BookMarked, UserPlus, Network,
} from 'lucide-react'
import { useAuth } from '@/lib/auth'
import { useQuyen } from '@/lib/quyen'
import { useHeThong, type MaHeThong } from '@/lib/heThong'
import { Button } from '@/components/ui'
import { cn } from '@/lib/utils'

const KHOA_THU_GON = 'lms_sidebar_thu_gon'

/**
 * Chữ viết tắt cho ô logo: lấy chữ cái đầu của 2 từ cuối, bỏ các từ chung ("trung tâm",
 * "ngoại ngữ") vì gần như tên nào cũng có — để lại thì mọi ô đều hiện "TT".
 */
function vietTat(tenTrungTam?: string) {
  if (!tenTrungTam) return 'TT'
  const tu = tenTrungTam
    .trim()
    .split(/\s+/)
    .filter((t) => !['trung', 'tam', 'tt', 'ngoai', 'ngu'].includes(t.toLowerCase()))
  const lay = tu.slice(-2)
  return (lay.map((t) => t[0]).join('') || tenTrungTam[0]).toUpperCase()
}

/** Sidebar thu gọn được (docs/frontend/ui-ux-nguyen-tac.md). */
export default function Layout() {
  const { t } = useTranslation()
  const { phien, dangXuat } = useAuth()
  const location = useLocation()
  const navigate = useNavigate()

  // Nhớ lựa chọn giữa các phiên: người thích sidebar hẹp không muốn thu gọn lại mỗi lần vào.
  const [thuGon, setThuGon] = useState(() => localStorage.getItem(KHOA_THU_GON) === '1')
  const [moMobile, setMoMobile] = useState(false)

  useEffect(() => {
    localStorage.setItem(KHOA_THU_GON, thuGon ? '1' : '0')
  }, [thuGon])

  const { coQuyen, dangTai: dangTaiQuyen } = useQuyen()
  const heThong = useHeThong()
  const [moChonHeThong, setMoChonHeThong] = useState(false)

  // Đóng ngăn kéo sau khi điều hướng — trên mobile nó phủ toàn màn hình.
  useEffect(() => setMoMobile(false), [location.pathname])

  /**
   * URL THẮNG lựa chọn đã lưu: mở link `/crm/...` thì hệ thống phải nhảy sang CRM.
   *
   * Không có bước này thì mở bookmark (hoặc link đồng nghiệp gửi) sẽ hiện sidebar của hệ thống
   * lưu trong `localStorage` — trang là CRM mà menu là HRM, và tên trang ở header trống vì
   * đường dẫn hiện tại không có trong menu đang hiện. Gặp thật 08/09/2026 khi lái UI.
   */
  useEffect(() => {
    // Cả ba hệ thống con nay đều có tiền tố (LMS thêm `/lms` ngày 10/09/2026), nên chỉ cần
    // một bảng tra. Trước đó LMS phải liệt kê tường minh 5 đường và thêm màn mới mà quên khai
    // thì sidebar hiện sai hệ thống con — lỗi im lặng, không có lỗi biên dịch.
    const theoDuong: Record<string, MaHeThong> = { '/hrm': 'Hrm', '/crm': 'Crm', '/lms': 'Lms' }
    const tienTo = Object.keys(theoDuong).find((x) => location.pathname.startsWith(x))
    const suyRa = tienTo ? theoDuong[tienTo] : null

    // Chỉ đổi khi người dùng THẬT SỰ vào được hệ thống đó — tránh kẹt ở sidebar trống.
    if (suyRa && suyRa !== heThong.hienTai && heThong.duocPhep.includes(suyRa))
      heThong.doi(suyRa)
  }, [location.pathname, heThong])

  interface MucMenu {
    to: string
    nhan: string
    icon: typeof Home
    /** true = chỉ khớp đúng đường dẫn này, không khớp route con. */
    cuoi?: boolean
    /** Chức năng cần có quyền `Xem` để thấy mục này. Bỏ trống = ai cũng thấy. */
    can?: string
  }

  interface NhomMenu {
    tieuDe?: string
    muc: MucMenu[]
    /**
     * Nhóm thuộc hệ thống nào. Bỏ trống = hiện ở MỌI hệ thống (Tổng quan, cụm quản trị) —
     * khớp với `ChucNang.DungChung` ở backend: quyền quản trị dùng chung cả ba hệ thống, ép
     * nó vào một hệ thống thì người quản trị nhân sự phải sang LMS mới sửa được tài khoản.
     */
    heThong?: MaHeThong
  }

  const nhomGoc: NhomMenu[] = [
    {
      muc: [{ to: '/', nhan: t('menu.tongQuan'), icon: Home, cuoi: true }],
    },

    // ---------- HRM ----------
    {
      tieuDe: t('menu.nhanSu'),
      heThong: 'Hrm',
      muc: [
        // Hồ sơ con người của BA vai trò nhân sự. Học viên ở LMS — xem pages/hrm/NhanSu.tsx.
        {
          to: '/hrm/co-cau', nhan: t('menu.coCauToChuc'),
          icon: Network, can: 'PhongBan',
        },
        {
          to: '/hrm/nhan-su', nhan: t('menu.nhanSuNguoiDung'),
          icon: UserCog, can: 'NhanSu',
        },
        {
          to: '/hrm/chuc-vu', nhan: t('menu.chucVu'), icon: Briefcase, can: 'NhanSu',
        },
      ],
    },

    // ---------- CRM ----------
    {
      tieuDe: t('menu.khachHang'),
      heThong: 'Crm',
      muc: [
        // Thứ tự theo dòng chảy nghiệp vụ: khách quan tâm → mua → danh mục bán.
        { to: '/crm/khach-hang', nhan: t('menu.khachHangDs'), icon: Contact, can: 'KhachHang' },
        { to: '/crm/doanh-thu', nhan: t('menu.doanhThu'), icon: TrendingUp, can: 'DoanhThu' },
        { to: '/crm/khoa-hoc', nhan: t('menu.khoaHoc'), icon: PackageOpen, can: 'KhoaHoc' },
        { to: '/crm/san-pham', nhan: t('menu.sanPham'), icon: BookMarked, can: 'SanPham' },
      ],
    },

    // ---------- LMS ----------
    {
      tieuDe: t('menu.daoTao'),
      heThong: 'Lms',
      muc: [
        { to: '/lms/hoc-vien', nhan: t('menu.hocVien'), icon: Users, can: 'TaiKhoan' },
        { to: '/lms/lop-hoc', nhan: t('menu.lopHoc'), icon: GraduationCap, can: 'LopHoc' },
        {
          to: '/lms/lop-hoc/cho-xep-lop', nhan: t('menu.choXepLop'), icon: UserPlus,
          can: 'LopHoc',
        },
        { to: '/lms/tai-lieu', nhan: t('menu.taiLieu'), icon: BookOpen, can: 'TaiLieu' },
        { to: '/lms/hoc-phi', nhan: t('menu.hocPhi'), icon: Wallet, can: 'HocPhi' },
      ],
    },

    {
      tieuDe: t('menu.quanTri'),
      muc: [
        { to: '/quan-tri/tai-khoan', nhan: t('menu.taiKhoan'), icon: KeyRound, can: 'TaiKhoan' },
        {
          to: '/quan-tri/phan-quyen', nhan: t('menu.phanQuyen'), icon: ShieldCheck,
          can: 'PhanQuyen',
        },
        {
          to: '/quan-tri/thiet-lap', nhan: t('menu.thietLap'), icon: Settings,
          can: 'ThietLapChung',
        },
        {
          to: '/quan-tri/nhat-ky', nhan: t('menu.nhatKy'), icon: ScrollText,
          can: 'NhatKyHeThong',
        },
      ],
    },
  ]

  // Ẩn mục không có quyền, rồi bỏ luôn nhóm trống — để lại tiêu đề "Quản trị hệ thống" không
  // có mục nào bên dưới trông như giao diện hỏng.
  //
  // Trong lúc CHƯA biết quyền thì hiện đủ: nếu ẩn trước rồi hiện sau, menu sẽ nhấp nháy mỗi
  // lần tải trang. Bấm nhầm lúc đó cùng lắm nhận thông báo không có quyền.
  // Lọc HAI tầng: hệ thống đang chọn, rồi quyền. Nhóm không khai `heThong` (Tổng quan, Quản
  // trị) đi qua tầng một — chúng dùng chung cho cả ba hệ thống.
  const theoHeThong = nhomGoc.filter(
    (n) => !n.heThong || !heThong.hienTai || n.heThong === heThong.hienTai,
  )

  const nhomMenu = dangTaiQuyen
    ? theoHeThong
    : theoHeThong
        .map((n) => ({ ...n, muc: n.muc.filter((m) => !m.can || coQuyen(m.can)) }))
        .filter((n) => n.muc.length > 0)

  /**
   * Trang đầu tiên vào được của một hệ thống — dùng khi bấm bộ chuyển.
   *
   * Suy từ CHÍNH menu đã lọc quyền, không hard-code: thêm/bỏ module hay thu quyền của người
   * dùng thì đích đến tự đúng theo. Trả null khi hệ thống đó không có mục nào (đang tải quyền,
   * hoặc người dùng không có quyền nào trong đó) — lúc đó chỉ đổi lựa chọn, không điều hướng.
   */
  const trangDauCua = (ma: MaHeThong): string | null =>
    nhomGoc
      .filter((n) => n.heThong === ma)
      .flatMap((n) => n.muc)
      .find((m) => !m.can || coQuyen(m.can))?.to ?? null

  /**
   * Đổi hệ thống = đổi lựa chọn **và** điều hướng sang trang của hệ thống đó.
   *
   * Phải điều hướng, không chỉ `doi()`. Lỗi gặp 09/09/2026: đứng ở `/crm/khach-hang` bấm HRM
   * thì `doi('Hrm')` ghi localStorage, rồi effect "URL thắng" ở trên đọc lại đường dẫn `/crm/...`
   * và ghi đè về `Crm` sau 16ms — bộ chuyển như chết trên MỌI trang thuộc hệ thống. Đo được
   * bằng cách chặn `Storage.prototype.setItem`: hai lần ghi liên tiếp "Hrm" rồi "Crm".
   *
   * Điều hướng làm URL và lựa chọn nói cùng một chuyện, nên effect kia không còn gì để sửa.
   */
  const doiHeThong = (ma: MaHeThong) => {
    heThong.doi(ma)
    const dich = trangDauCua(ma)
    // `/` (Tổng quan) thuộc mọi hệ thống nên không kéo lựa chọn về đâu — đích an toàn khi hệ
    // thống đích chưa có mục nào hiện được.
    navigate(dich ?? '/')
  }

  const tenTrang = nhomMenu
    .flatMap((n) => n.muc)
    .find((m) => (m.cuoi ? location.pathname === m.to : location.pathname.startsWith(m.to)))?.nhan

  const noiDungSidebar = (
    <>
      {/*
        Tên trung tâm là dòng chính, mã là dòng phụ: người dùng nhận ra trung tâm qua tên, mã chỉ
        cần khi đăng nhập hoặc đọc cho người khác.
      */}
      <div
        className={cn(
          'flex h-14 shrink-0 items-center gap-2.5 border-b border-border',
          thuGon ? 'justify-center px-2' : 'px-3',
        )}
      >
        <div
          className="flex h-8 w-8 shrink-0 items-center justify-center rounded bg-primary text-xs font-bold text-primary-foreground"
          title={thuGon ? `${phien?.tenTrungTam ?? ''} · ${phien?.maTrungTam ?? ''}` : undefined}
        >
          {vietTat(phien?.tenTrungTam)}
        </div>
        {!thuGon && (
          <div className="min-w-0">
            <p className="truncate text-sm font-semibold leading-tight" title={phien?.tenTrungTam}>
              {/* Token cũ chưa có claim ten_doi: hiện mã đội thay vì để trống. */}
              {phien?.tenTrungTam || phien?.maTrungTam || '—'}
            </p>
            {phien?.tenTrungTam && (
              <p className="truncate font-mono text-[11px] leading-tight tracking-wide text-muted-foreground">
                {phien.maTrungTam}
              </p>
            )}
          </div>
        )}
      </div>

      <nav className="flex-1 overflow-y-auto p-2">
        {nhomMenu.map((nhom, i) => (
          <div key={i} className="mb-3">
            {nhom.tieuDe && !thuGon && (
              <p className="px-3 py-1.5 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                {nhom.tieuDe}
              </p>
            )}
            {/* Khi thu gọn, dùng đường kẻ thay tiêu đề nhóm để vẫn phân tách được các cụm. */}
            {nhom.tieuDe && thuGon && <div className="mx-2 mb-2 border-t border-border" />}
            {nhom.muc.map((m) => (
              <NavLink
                key={m.to}
                to={m.to}
                end={m.cuoi}
                title={thuGon ? m.nhan : undefined}
                className={({ isActive }) =>
                  cn(
                    'flex items-center gap-2.5 rounded-md py-2 text-sm transition-colors',
                    thuGon ? 'justify-center px-2' : 'px-3',
                    isActive
                      ? 'bg-primary/10 font-medium text-primary'
                      : 'text-foreground/80 hover:bg-muted',
                  )
                }
              >
                <m.icon className="h-4 w-4 shrink-0" />
                {!thuGon && <span className="truncate">{m.nhan}</span>}
              </NavLink>
            ))}
          </div>
        ))}
      </nav>

      <div className="shrink-0 border-t border-border p-2">
        {/*
          Bộ chuyển hệ thống — đặt ngay trên Đăng xuất theo yêu cầu.

          Chỉ hiện khi vào được từ 2 hệ thống trở lên (`coTheChuyen`): học viên và giáo viên
          chỉ có LMS, một nút với đúng một lựa chọn là nhiễu chứ không phải tiện.
        */}
        {heThong.coTheChuyen && heThong.hienTai && (
          <div className="relative mb-1">
            <Button
              variant="outline"
              size="sm"
              title={thuGon ? t(`heThong.${heThong.hienTai}`) : undefined}
              className={cn('w-full', thuGon ? 'justify-center px-0' : 'justify-between')}
              onClick={() => setMoChonHeThong((v) => !v)}
              aria-expanded={moChonHeThong}
              aria-label={t('heThong.chuyenHeThong')}
            >
              <span className="flex min-w-0 items-center gap-2">
                <LayoutGrid className="h-4 w-4 shrink-0" />
                {!thuGon && (
                  <span className="truncate">{t(`heThong.${heThong.hienTai}`)}</span>
                )}
              </span>
              {!thuGon && <span className="text-xs text-muted-foreground">▾</span>}
            </Button>

            {moChonHeThong && (
              <>
                {/* Lớp phủ để bấm ra ngoài là đóng — không cần nghe click toàn tài liệu. */}
                <div
                  className="fixed inset-0 z-40"
                  onClick={() => setMoChonHeThong(false)}
                  aria-hidden
                />
                <div className="absolute bottom-full left-0 z-50 mb-1 w-full min-w-44 overflow-hidden rounded-md border border-border bg-background shadow-lg">
                  {heThong.duocPhep.map((ma) => (
                    <button
                      key={ma}
                      type="button"
                      onClick={() => {
                        doiHeThong(ma)
                        setMoChonHeThong(false)
                      }}
                      className={cn(
                        'flex w-full items-center gap-2 px-3 py-2 text-left text-sm transition-colors hover:bg-muted',
                        ma === heThong.hienTai && 'font-medium text-primary',
                      )}
                    >
                      {ma === heThong.hienTai ? (
                        <Check className="h-3.5 w-3.5 shrink-0" />
                      ) : (
                        <span className="w-3.5 shrink-0" />
                      )}
                      <span className="truncate">{t(`heThong.${ma}`)}</span>
                    </button>
                  ))}
                </div>
              </>
            )}
          </div>
        )}

        {!thuGon && (
          <div className="px-3 py-1.5 text-xs text-muted-foreground">{phien?.username}</div>
        )}
        <Button
          variant="ghost"
          size="sm"
          title={thuGon ? t('chung.dangXuat') : undefined}
          className={cn('w-full', thuGon ? 'justify-center px-0' : 'justify-start')}
          onClick={dangXuat}
        >
          <LogOut className="h-4 w-4" />
          {!thuGon && t('chung.dangXuat')}
        </Button>
      </div>
    </>
  )

  return (
    <div className="flex min-h-screen bg-muted/20">
      {/*
        Sidebar cố định — desktop.

        `sticky top-0` + `h-screen`: nếu để nó là flex item thường thì nó giãn theo chiều cao của
        cả trang. Đo thật 21/08 trên màn Thống kê: viewport 700px, trang 1413px, **sidebar cao
        1412px** — cuộn xuống đáy thì logo và menu trôi hẳn khỏi màn hình (top = -713), người dùng
        phải cuộn ngược lên mới đổi được trang.

        `h-screen` khoá đúng một viewport, `sticky` giữ nó tại chỗ khi trang cuộn. `nav` bên trong
        đã có `overflow-y-auto` nên menu dài tự cuộn riêng — trước đây thuộc tính đó vô hiệu vì
        phần tử cha không bị giới hạn chiều cao.
      */}
      <aside
        className={cn(
          'sticky top-0 hidden h-screen shrink-0 flex-col overflow-hidden border-r border-border bg-background transition-[width] duration-200 md:flex',
          thuGon ? 'w-16' : 'w-56',
        )}
      >
        {noiDungSidebar}
      </aside>

      {/* Ngăn kéo — mobile. Sidebar cố định chiếm quá nhiều bề ngang màn hình nhỏ. */}
      {moMobile && (
        <div className="fixed inset-0 z-50 md:hidden">
          <div
            className="absolute inset-0 bg-black/40"
            onClick={() => setMoMobile(false)}
            aria-hidden
          />
          <aside className="absolute left-0 top-0 flex h-full w-64 flex-col border-r border-border bg-background">
            <button
              type="button"
              onClick={() => setMoMobile(false)}
              aria-label={t('chung.dong')}
              className="absolute right-2 top-3 rounded p-1 hover:bg-muted"
            >
              <X className="h-4 w-4" />
            </button>
            {noiDungSidebar}
          </aside>
        </div>
      )}

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex h-14 shrink-0 items-center gap-2 border-b border-border bg-background px-3">
          <Button
            variant="ghost"
            size="sm"
            className="md:hidden"
            onClick={() => setMoMobile(true)}
            aria-label={t('chung.moMenu')}
          >
            <Menu className="h-4 w-4" />
          </Button>

          <Button
            variant="ghost"
            size="sm"
            className="hidden md:inline-flex"
            onClick={() => setThuGon((v) => !v)}
            aria-label={thuGon ? t('chung.moSidebar') : t('chung.thuGonSidebar')}
            title={thuGon ? t('chung.moSidebar') : t('chung.thuGonSidebar')}
          >
            {thuGon ? <PanelLeft className="h-4 w-4" /> : <PanelLeftClose className="h-4 w-4" />}
          </Button>

          <h1 className="truncate text-sm font-semibold">{tenTrang}</h1>
        </header>
        <main className="flex-1 overflow-x-hidden p-5">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
