import { useTranslation } from 'react-i18next'
import { NavLink, Outlet, useLocation } from 'react-router-dom'
import {
  CalendarDays, BarChart3, Wallet, Users, UserCircle, ShieldCheck, Settings, LogOut, Home,
} from 'lucide-react'
import { useAuth } from '@/lib/auth'
import { Button } from '@/components/ui'
import { cn } from '@/lib/utils'

/** Sidebar cố định theo 5 module + breadcrumb (docs/frontend/ui-ux-nguyen-tac.md). */
export default function Layout() {
  const { t } = useTranslation()
  const { phien, dangXuat } = useAuth()
  const location = useLocation()

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

  return (
    <div className="flex min-h-screen bg-muted/20">
      <aside className="hidden w-56 shrink-0 flex-col border-r border-border bg-background md:flex">
        <div className="flex h-14 items-center gap-2 border-b border-border px-4">
          <div className="flex h-7 w-7 items-center justify-center rounded bg-primary text-xs font-bold text-primary-foreground">
            SR
          </div>
          <span className="truncate text-sm font-semibold">{phien?.maDoi}</span>
        </div>

        <nav className="flex-1 overflow-y-auto p-2">
          {nhomMenu.map((nhom, i) => (
            <div key={i} className="mb-3">
              {nhom.tieuDe && (
                <p className="px-3 py-1.5 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                  {nhom.tieuDe}
                </p>
              )}
              {nhom.muc.map((m) => (
                <NavLink
                  key={m.to}
                  to={m.to}
                  end={m.cuoi}
                  className={({ isActive }) =>
                    cn(
                      'flex items-center gap-2.5 rounded-md px-3 py-2 text-sm transition-colors',
                      isActive
                        ? 'bg-primary/10 font-medium text-primary'
                        : 'text-foreground/80 hover:bg-muted',
                    )
                  }
                >
                  <m.icon className="h-4 w-4 shrink-0" />
                  <span className="truncate">{m.nhan}</span>
                </NavLink>
              ))}
            </div>
          ))}
        </nav>

        <div className="border-t border-border p-2">
          <div className="px-3 py-1.5 text-xs text-muted-foreground">{phien?.username}</div>
          <Button variant="ghost" size="sm" className="w-full justify-start" onClick={dangXuat}>
            <LogOut className="h-4 w-4" />
            {t('chung.dangXuat')}
          </Button>
        </div>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex h-14 shrink-0 items-center border-b border-border bg-background px-5">
          <h1 className="text-sm font-semibold">{tenTrang}</h1>
        </header>
        <main className="flex-1 overflow-x-hidden p-5">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
