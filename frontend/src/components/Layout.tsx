import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { NavLink, Outlet, useLocation } from 'react-router-dom'
import {
  CalendarDays, BarChart3, Wallet, Users, UserCircle, ShieldCheck, Settings, LogOut, Home, Swords, MailOpen,
  ClipboardList, Video,
  PanelLeftClose, PanelLeft, Menu, X,
} from 'lucide-react'
import { useAuth } from '@/lib/auth'
import { Button } from '@/components/ui'
import { cn } from '@/lib/utils'

const KHOA_THU_GON = 'sr_sidebar_thu_gon'

/**
 * Chữ viết tắt cho ô logo: lấy chữ cái đầu của 2 từ cuối, bỏ tiền tố "FC"/"CLB" vì gần như
 * CLB nào cũng có, để lại thì mọi ô đều hiện "FC".
 */
function vietTat(tenDoi?: string) {
  if (!tenDoi) return 'SR'
  const tu = tenDoi
    .trim()
    .split(/\s+/)
    .filter((t) => !['fc', 'clb', 'cau', 'lac', 'bo'].includes(t.toLowerCase()))
  const lay = tu.slice(-2)
  return (lay.map((t) => t[0]).join('') || tenDoi[0]).toUpperCase()
}

/** Sidebar theo 5 module, thu gọn được (docs/frontend/ui-ux-nguyen-tac.md). */
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

  // Đóng ngăn kéo sau khi điều hướng — trên mobile nó phủ toàn màn hình.
  useEffect(() => setMoMobile(false), [location.pathname])

  interface MucMenu {
    to: string
    nhan: string
    icon: typeof Home
    /** true = chỉ khớp đúng đường dẫn này, không khớp route con. */
    cuoi?: boolean
  }

  const nhomMenu: { tieuDe?: string; muc: MucMenu[] }[] = [
    {
      muc: [{ to: '/', nhan: t('menu.tongQuan'), icon: Home, cuoi: true }],
    },
    {
      muc: [
        { to: '/lich-thi-dau', nhan: t('menu.lichThiDau'), icon: CalendarDays },
        { to: '/hom-thu', nhan: t('menu.homThu'), icon: MailOpen },
        { to: '/doi-thu', nhan: t('menu.doiThu'), icon: Swords },
        { to: '/mau-doi-hinh', nhan: t('menu.mauDoiHinh'), icon: ClipboardList },
        { to: '/thu-vien-video', nhan: t('menu.thuVienVideo'), icon: Video },
        { to: '/thong-ke', nhan: t('menu.thongKe'), icon: BarChart3 },
        { to: '/tai-chinh', nhan: t('menu.taiChinh'), icon: Wallet },
      ],
    },
    {
      tieuDe: t('menu.quanTri'),
      muc: [
        { to: '/quan-tri/tai-khoan', nhan: t('menu.taiKhoan'), icon: Users },
        { to: '/quan-tri/cau-thu', nhan: t('menu.cauThu'), icon: UserCircle },
        { to: '/quan-tri/phan-quyen', nhan: t('menu.phanQuyen'), icon: ShieldCheck },
        { to: '/quan-tri/thiet-lap', nhan: t('menu.thietLap'), icon: Settings },
      ],
    },
  ]

  const tenTrang = nhomMenu
    .flatMap((n) => n.muc)
    .find((m) => (m.cuoi ? location.pathname === m.to : location.pathname.startsWith(m.to)))?.nhan

  const noiDungSidebar = (
    <>
      {/*
        Tên đội là dòng chính, mã đội là dòng phụ: người dùng nhận ra CLB qua tên, mã chỉ
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
          title={thuGon ? `${phien?.tenDoi ?? ''} · ${phien?.maDoi ?? ''}` : undefined}
        >
          {vietTat(phien?.tenDoi)}
        </div>
        {!thuGon && (
          <div className="min-w-0">
            <p className="truncate text-sm font-semibold leading-tight" title={phien?.tenDoi}>
              {/* Token cũ chưa có claim ten_doi: hiện mã đội thay vì để trống. */}
              {phien?.tenDoi || phien?.maDoi || '—'}
            </p>
            {phien?.tenDoi && (
              <p className="truncate font-mono text-[11px] leading-tight tracking-wide text-muted-foreground">
                {phien.maDoi}
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
      {/* Sidebar cố định — desktop */}
      <aside
        className={cn(
          'hidden shrink-0 flex-col border-r border-border bg-background transition-[width] duration-200 md:flex',
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
