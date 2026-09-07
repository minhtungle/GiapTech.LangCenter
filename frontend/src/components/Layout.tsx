import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { NavLink, Outlet, useLocation } from 'react-router-dom'
import {
  Users, ShieldCheck, Settings, LogOut, Home, GraduationCap, BookOpen, Wallet,
  PanelLeftClose, PanelLeft, Menu, X, ScrollText,
} from 'lucide-react'
import { useAuth } from '@/lib/auth'
import { useQuyen } from '@/lib/quyen'
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

  // Nhớ lựa chọn giữa các phiên: người thích sidebar hẹp không muốn thu gọn lại mỗi lần vào.
  const [thuGon, setThuGon] = useState(() => localStorage.getItem(KHOA_THU_GON) === '1')
  const [moMobile, setMoMobile] = useState(false)

  useEffect(() => {
    localStorage.setItem(KHOA_THU_GON, thuGon ? '1' : '0')
  }, [thuGon])

  const { coQuyen, dangTai: dangTaiQuyen } = useQuyen()

  // Đóng ngăn kéo sau khi điều hướng — trên mobile nó phủ toàn màn hình.
  useEffect(() => setMoMobile(false), [location.pathname])

  interface MucMenu {
    to: string
    nhan: string
    icon: typeof Home
    /** true = chỉ khớp đúng đường dẫn này, không khớp route con. */
    cuoi?: boolean
    /** Chức năng cần có quyền `Xem` để thấy mục này. Bỏ trống = ai cũng thấy. */
    can?: string
  }

  const nhomGoc: { tieuDe?: string; muc: MucMenu[] }[] = [
    {
      muc: [{ to: '/', nhan: t('menu.tongQuan'), icon: Home, cuoi: true }],
    },
    {
      tieuDe: t('menu.daoTao'),
      muc: [
        { to: '/lop-hoc', nhan: t('menu.lopHoc'), icon: GraduationCap, can: 'LopHoc' },
        { to: '/tai-lieu', nhan: t('menu.taiLieu'), icon: BookOpen, can: 'TaiLieu' },
        { to: '/hoc-phi', nhan: t('menu.hocPhi'), icon: Wallet, can: 'HocPhi' },
      ],
    },
    {
      tieuDe: t('menu.quanTri'),
      muc: [
        { to: '/quan-tri/tai-khoan', nhan: t('menu.taiKhoan'), icon: Users, can: 'TaiKhoan' },
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
  const nhomMenu = dangTaiQuyen
    ? nhomGoc
    : nhomGoc
        .map((n) => ({ ...n, muc: n.muc.filter((m) => !m.can || coQuyen(m.can)) }))
        .filter((n) => n.muc.length > 0)

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
