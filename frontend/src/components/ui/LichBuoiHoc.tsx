import { useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import FullCalendar from '@fullcalendar/react'
import dayGridPlugin from '@fullcalendar/daygrid'
import timeGridPlugin from '@fullcalendar/timegrid'
import listPlugin from '@fullcalendar/list'
import viLocale from '@fullcalendar/core/locales/vi'
import type { EventClickArg, EventInput } from '@fullcalendar/core'
import { ChevronLeft, ChevronRight } from 'lucide-react'
import { Button } from '@/components/ui'
import { useMuiGio } from '@/lib/quyen'
import './lich-buoi-hoc.css'

/** Buổi học tối giản — chỉ những gì lịch cần vẽ. */
export interface BuoiChoLich {
  id: string
  thuTu: number
  batDau: string
  ketThuc: string
  trangThai: 'DaLenLich' | 'DaHoanThanh' | 'DaHuy'
  laHocBu: boolean
  tenGiaoVien?: string
  phongHoc?: string | null
  tenLopHoc?: string
}

const CHE_DO = [
  { ma: 'dayGridMonth', khoa: 'lich.thang' },
  { ma: 'timeGridWeek', khoa: 'lich.tuan' },
  { ma: 'listMonth', khoa: 'lich.danhSach' },
] as const

type CheDo = (typeof CHE_DO)[number]['ma']

/**
 * Lịch buổi học dạng calendar, dựng trên FullCalendar 6 (MIT).
 *
 * Vì sao dùng thư viện thay vì tự viết: lưới tháng, trục giờ, kéo-thả, điều hướng, in ấn và
 * hàng chục ca biên về múi giờ là công việc nhiều tháng. Chọn FullCalendar vì nó hỗ trợ
 * `timeZone` sẵn (điểm bắt buộc ở đây), mọi gói view cùng một phiên bản ổn định, và chỉ kéo
 * theo `preact` — trong khi `react-big-calendar` mang cả moment, luxon, lodash và globalize.
 *
 * **Giờ vẽ theo múi giờ TRUNG TÂM**, không phải máy người xem: lệch giờ trên lịch làm buổi
 * nhảy sang ô ngày khác, sai rõ hơn nhiều so với bảng.
 */
export function LichBuoiHoc({
  buoi,
  onChonBuoi,
  hienTenLop,
}: {
  buoi: BuoiChoLich[]
  onChonBuoi?: (id: string) => void
  /** true = hiện tên lớp trong nhãn (lịch nhiều lớp), false = hiện số buổi. */
  hienTenLop?: boolean
}) {
  const { t } = useTranslation()
  const muiGio = useMuiGio()
  const lich = useRef<FullCalendar>(null)
  const [cheDo, setCheDo] = useState<CheDo>('dayGridMonth')
  const [tieuDe, setTieuDe] = useState('')

  const sukien = useMemo<EventInput[]>(
    () =>
      buoi.map((b) => ({
        id: b.id,
        title: hienTenLop && b.tenLopHoc
          ? b.tenLopHoc
          : `${t('buoiHoc.thuTuNgan')}${b.thuTu}`,
        start: b.batDau,
        end: b.ketThuc,
        // Màu theo trạng thái, khớp badge ở bảng để hai chỗ không nói khác nhau.
        classNames: [
          b.trangThai === 'DaHoanThanh'
            ? 'buoi-xong'
            : b.trangThai === 'DaHuy'
              ? 'buoi-huy'
              : 'buoi-lich',
          ...(b.laHocBu ? ['buoi-bu'] : []),
        ],
        extendedProps: {
          giaoVien: b.tenGiaoVien,
          phongHoc: b.phongHoc,
          laHocBu: b.laHocBu,
        },
      })),
    [buoi, hienTenLop, t],
  )

  const dieuHuong = (huong: 'truoc' | 'sau' | 'homNay') => {
    const api = lich.current?.getApi()
    if (!api) return
    if (huong === 'truoc') api.prev()
    else if (huong === 'sau') api.next()
    else api.today()
    setTieuDe(api.view.title)
  }

  const doiCheDo = (ma: CheDo) => {
    setCheDo(ma)
    const api = lich.current?.getApi()
    if (api) {
      api.changeView(ma)
      setTieuDe(api.view.title)
    }
  }

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex items-center gap-1">
          <Button
            variant="outline"
            size="sm"
            aria-label={t('lich.truoc')}
            onClick={() => dieuHuong('truoc')}
          >
            <ChevronLeft className="h-4 w-4" />
          </Button>
          <Button variant="outline" size="sm" onClick={() => dieuHuong('homNay')}>
            {t('lich.homNay')}
          </Button>
          <Button
            variant="outline"
            size="sm"
            aria-label={t('lich.sau')}
            onClick={() => dieuHuong('sau')}
          >
            <ChevronRight className="h-4 w-4" />
          </Button>
          <span className="ml-2 text-sm font-medium capitalize">{tieuDe}</span>
        </div>

        <div className="flex gap-1 rounded-lg border border-border p-1">
          {CHE_DO.map((x) => (
            <button
              key={x.ma}
              type="button"
              onClick={() => doiCheDo(x.ma)}
              className={
                'rounded-md px-2.5 py-1 text-xs font-medium transition-colors ' +
                (cheDo === x.ma
                  ? 'bg-primary text-primary-foreground'
                  : 'text-muted-foreground hover:bg-muted')
              }
            >
              {t(x.khoa)}
            </button>
          ))}
        </div>
      </div>

      <div className="lich-buoi-hoc rounded-lg border border-border p-2">
        <FullCalendar
          ref={lich}
          plugins={[dayGridPlugin, timeGridPlugin, listPlugin]}
          initialView="dayGridMonth"
          locale={viLocale}
          // Múi giờ TRUNG TÂM. `undefined` lúc chưa tải xong → FullCalendar tạm dùng múi giờ
          // máy; chỉ lệch trong khoảnh khắc đầu, và hook cache theo phiên nên chỉ xảy ra một lần.
          timeZone={muiGio}
          headerToolbar={false}
          height="auto"
          // Ẩn hẳn thanh cuộn giờ ngoài khung 6h–22h: trung tâm ngoại ngữ không dạy đêm, để
          // trục 24 tiếng thì buổi tối bị nén thành một dải mỏng.
          slotMinTime="06:00:00"
          slotMaxTime="22:00:00"
          allDaySlot={false}
          nowIndicator
          firstDay={1}
          dayMaxEvents={3}
          events={sukien}
          eventClick={(arg: EventClickArg) => onChonBuoi?.(arg.event.id)}
          datesSet={(arg) => setTieuDe(arg.view.title)}
          eventDidMount={(arg) => {
            // Tooltip gốc của trình duyệt: đủ dùng, không cần thư viện tooltip cho một dòng.
            const p = arg.event.extendedProps as {
              giaoVien?: string
              phongHoc?: string | null
              laHocBu?: boolean
            }
            const dong = [
              arg.event.title,
              p.giaoVien,
              p.phongHoc,
              p.laHocBu ? t('buoiHoc.hocBu') : null,
            ].filter(Boolean)
            arg.el.title = dong.join(' · ')
          }}
          noEventsText={t('buoiHoc.chuaCoLich')}
        />
      </div>
    </div>
  )
}
