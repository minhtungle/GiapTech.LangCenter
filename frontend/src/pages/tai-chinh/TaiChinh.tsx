import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ArrowDownCircle, ArrowUpCircle, Wallet } from 'lucide-react'
import { api } from '@/lib/api'
import { cn } from '@/lib/utils'
import DanhSachQuy from './DanhSachQuy'
import KhoanChi from './KhoanChi'

interface TongQuanDto {
  tongDaThu: number
  tongConPhaiThu: number
  tongDaChi: number
  soDu: number
  soDotQuyDangMo: number
  soNguoiConNo: number
}

type Tab = 'quy' | 'chi'

/** Định dạng tiền VND — không lẻ đồng, CLB phong trào không thu tiền lẻ. */
export const tienVnd = (n: number) =>
  n.toLocaleString('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 })

/**
 * FR-15, FR-16 — quỹ đội.
 *
 * Hai tab: **Đợt quỹ** (thu) và **Khoản chi**. Thanh tổng quan ở trên dùng chung cho cả hai
 * vì số dư là kết quả của cả thu lẫn chi — đặt riêng mỗi tab một thanh thì hai nơi hiện hai
 * con số không liên quan nhau.
 */
export default function TaiChinh() {
  const { t } = useTranslation()
  const [tab, setTab] = useState<Tab>('quy')

  const { data: tq } = useQuery({
    queryKey: ['tai-chinh-tong-quan'],
    queryFn: async () => (await api.get<TongQuanDto>('/tai-chinh/tong-quan')).data,
  })

  const tabs: { khoa: Tab; nhan: string }[] = [
    { khoa: 'quy', nhan: t('taiChinh.tabQuy') },
    { khoa: 'chi', nhan: t('taiChinh.tabChi') },
  ]

  return (
    <div className="flex flex-col gap-4">
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <TheSo
          nhan={t('taiChinh.soDu')}
          giaTri={tienVnd(tq?.soDu ?? 0)}
          icon={<Wallet className="h-4 w-4" />}
          // Số dư âm nghĩa là đã chi quá số thu — phải nhìn ra ngay, không lẫn vào màu chung.
          mau={(tq?.soDu ?? 0) < 0 ? 'lose' : 'primary'}
          phu={t('taiChinh.soDuGoiY')}
        />
        <TheSo
          nhan={t('taiChinh.daThu')}
          giaTri={tienVnd(tq?.tongDaThu ?? 0)}
          icon={<ArrowDownCircle className="h-4 w-4" />}
          mau="win"
        />
        <TheSo
          nhan={t('taiChinh.daChi')}
          giaTri={tienVnd(tq?.tongDaChi ?? 0)}
          icon={<ArrowUpCircle className="h-4 w-4" />}
          mau="lose"
        />
        <TheSo
          nhan={t('taiChinh.conPhaiThu')}
          giaTri={tienVnd(tq?.tongConPhaiThu ?? 0)}
          mau={(tq?.tongConPhaiThu ?? 0) > 0 ? 'draw' : 'muted'}
          phu={
            (tq?.soNguoiConNo ?? 0) > 0
              ? t('taiChinh.soNguoiConNo', { so: tq?.soNguoiConNo })
              : t('taiChinh.khongAiNo')
          }
        />
      </div>

      <div className="flex gap-1 border-b border-border">
        {tabs.map((tb) => (
          <button
            key={tb.khoa}
            type="button"
            onClick={() => setTab(tb.khoa)}
            className={cn(
              '-mb-px border-b-2 px-4 py-2 text-sm transition-colors',
              tab === tb.khoa
                ? 'border-primary font-medium text-primary'
                : 'border-transparent text-muted-foreground hover:text-foreground',
            )}
          >
            {tb.nhan}
          </button>
        ))}
      </div>

      {tab === 'quy' ? <DanhSachQuy /> : <KhoanChi />}
    </div>
  )
}

function TheSo({
  nhan,
  giaTri,
  icon,
  mau,
  phu,
}: {
  nhan: string
  giaTri: string
  icon?: React.ReactNode
  mau: 'primary' | 'win' | 'lose' | 'draw' | 'muted'
  phu?: string
}) {
  return (
    <div className="rounded-lg border border-border p-3">
      <p className="flex items-center gap-1.5 text-xs text-muted-foreground">
        {icon}
        {nhan}
      </p>
      <p
        className={cn(
          'mt-1 text-xl font-bold tabular-nums',
          mau === 'primary' && 'text-primary',
          mau === 'win' && 'text-status-win',
          mau === 'lose' && 'text-status-lose',
          mau === 'draw' && 'text-status-draw',
          mau === 'muted' && 'text-muted-foreground',
        )}
      >
        {giaTri}
      </p>
      {phu && <p className="mt-0.5 text-xs text-muted-foreground">{phu}</p>}
    </div>
  )
}
