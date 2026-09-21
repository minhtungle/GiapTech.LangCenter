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
import {
  type TinhTrangBuoi,
  type TrangThaiBuoiHoc,
  tinhTrangHienTai,
} from '@/lib/tinhTrangBuoi'
import { useMuiGio } from '@/lib/quyen'
import './lich-buoi-hoc.css'

/** Buổi học tối giản — chỉ những gì lịch cần vẽ. */
export interface BuoiChoLich {
  id: string
  thuTu: number
  batDau: string
  ketThuc: string
  trangThai: TrangThaiBuoiHoc
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
/**
 * Đổi một mốc tuyệt đối sang **giờ treo tường** của múi giờ trung tâm (18/09/2026).
 *
 * ## Vì sao cần hàm này, khi đã truyền `timeZone` cho FullCalendar
 *
 * FullCalendar bản **không có plugin múi giờ** chỉ hiểu `'local'` và `'UTC'`. Đưa cho nó một
 * tên IANA như `Asia/Ho_Chi_Minh` thì nó **âm thầm rơi về UTC** — không cảnh báo, không lỗi.
 * Hậu quả thấy trên màn: buổi 18:00 giờ Việt Nam hiện **"11 giờ"** (= 11:00 UTC), trong khi
 * bảng danh sách ngay cạnh hiện đúng 18:00. Hai chỗ nói hai giờ khác nhau về cùng một buổi.
 *
 * Đã kiểm trên dữ liệu thật: `BUOI_HOC.bat_dau = 2026-09-16 11:00+00`, thiết lập trung tâm
 * `Asia/Ho_Chi_Minh`, lịch hiện "11 giờ". Lỗi có từ trước, không phải do thay đổi hôm nay —
 * chạy lại trên bản gốc cũng ra "11 giờ".
 *
 * ## Cách chữa: đổi dữ liệu, không đổi thư viện
 *
 * Thay vì thêm `@fullcalendar/luxon3` (một phụ thuộc nữa, chỉ để định dạng giờ), ta dịch mốc
 * sang giờ treo tường của trung tâm rồi đưa cho FullCalendar dưới dạng `'local'`. Lịch nhận
 * chuỗi **không có offset** nên hiểu đúng như giờ địa phương.
 *
 * Hệ quả cần biết: từ đây lịch luôn vẽ theo giờ TRUNG TÂM, kể cả khi máy người xem đặt múi
 * giờ khác — đó chính là điều mong muốn (xem `useMuiGio`).
 */
function gioTreoTuong(moc: string, muiGio: string | undefined): string {
  const d = new Date(moc)
  if (!muiGio) return moc   // chưa tải xong thiết lập: để nguyên, lệch một khoảnh khắc đầu

  // `en-CA` cho `YYYY-MM-DD`, `en-GB` cho `HH:mm:ss` 24 giờ — hai locale ổn định nhất cho
  // việc ghép chuỗi ISO, không phụ thuộc locale máy người xem.
  const ngay = d.toLocaleDateString('en-CA', { timeZone: muiGio })
  const gio = d.toLocaleTimeString('en-GB', { timeZone: muiGio, hour12: false })
  return `${ngay}T${gio}`
}

/** Tình trạng → class CSS ở `lich-buoi-hoc.css`. Một chỗ khai để bảng và lịch cùng màu. */
const CLASS_TINH_TRANG: Record<TinhTrangBuoi, string> = {
  ChuaBatDau: 'buoi-chua-bat-dau',
  DangDienRa: 'buoi-dang-dien-ra',
  ChuaChot: 'buoi-chua-chot',
  DaXong: 'buoi-xong',
  ChuyenLich: 'buoi-chuyen-lich',
  DaHuy: 'buoi-huy',
}

export function LichBuoiHoc({
  buoi,
  onChonBuoi,
  hienTenLop,
  onDoiThang,
}: {
  buoi: BuoiChoLich[]
  onChonBuoi?: (id: string) => void
  /** true = hiện tên lớp trong nhãn (lịch nhiều lớp), false = hiện số buổi. */
  hienTenLop?: boolean
  /**
   * Người dùng bấm ‹ › hoặc "Hôm nay" — báo ra mốc của khung nhìn mới (21/09/2026).
   *
   * Cần khi dữ liệu **tải theo tháng**: lịch của cả trung tâm không thể tải hết mọi buổi từ
   * xưa tới nay. Lịch trong MỘT lớp thì không cần (số buổi hữu hạn, tải một lần), nên tham số
   * này tuỳ chọn — không truyền thì lịch hoạt động y như trước.
   */
  onDoiThang?: (moc: Date) => void
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
        // Giờ treo tường của trung tâm — xem `gioTreoTuong`. Đưa mốc có offset thì
        // FullCalendar (không plugin múi giờ) vẽ theo UTC và lệch 7 tiếng.
        start: gioTreoTuong(b.batDau, muiGio),
        end: gioTreoTuong(b.ketThuc, muiGio),
        // Màu theo TÌNH TRẠNG suy từ giờ, khớp badge ở bảng để hai chỗ không nói khác nhau.
        // Dùng `trangThai` thô thì buổi đã qua chưa chốt tô như buổi sắp tới.
        classNames: [
          CLASS_TINH_TRANG[tinhTrangHienTai(b.trangThai, b.batDau, b.ketThuc)],
          ...(b.laHocBu ? ['buoi-bu'] : []),
        ],
        extendedProps: {
          giaoVien: b.tenGiaoVien,
          phongHoc: b.phongHoc,
          laHocBu: b.laHocBu,
        },
      })),
    [buoi, hienTenLop, muiGio, t],
  )

  const dieuHuong = (huong: 'truoc' | 'sau' | 'homNay') => {
    const api = lich.current?.getApi()
    if (!api) return
    if (huong === 'truoc') api.prev()
    else if (huong === 'sau') api.next()
    else api.today()
    setTieuDe(api.view.title)

    // Báo mốc GIỮA khung nhìn, không phải `activeStart`: ở view tháng, `activeStart` thường
    // rơi vào tháng TRƯỚC (lịch vẽ vài ngày đầu tuần của tháng kề), nên trang cha sẽ tải
    // nhầm tháng.
    const v = api.view
    const giua = new Date((v.activeStart.getTime() + v.activeEnd.getTime()) / 2)
    onDoiThang?.(giua)
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

      {/*
        Chú giải màu — bảng có chữ trong badge, còn lịch CHỈ có màu, nên không có chú giải thì
        người dùng phải bấm vào từng buổi mới biết màu nghĩa gì.
      */}
      <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-muted-foreground">
        {(Object.keys(CLASS_TINH_TRANG) as TinhTrangBuoi[]).map((tt) => (
          <span key={tt} className="inline-flex items-center gap-1.5">
            <span
              aria-hidden
              className={`chu-giai-mau ${CLASS_TINH_TRANG[tt]} inline-block h-2.5 w-2.5 rounded-sm`}
            />
            {t(`tinhTrangBuoi.${tt}`)}
          </span>
        ))}
      </div>

      <div className="lich-buoi-hoc rounded-lg border border-border p-2">
        <FullCalendar
          ref={lich}
          plugins={[dayGridPlugin, timeGridPlugin, listPlugin]}
          initialView="dayGridMonth"
          locale={viLocale}
          /*
            `'local'`, KHÔNG phải tên IANA: bản FullCalendar này không có plugin múi giờ nên
            tên IANA bị âm thầm hiểu thành UTC (buổi 18:00 hiện "11 giờ"). Việc quy đổi đã làm
            ở `gioTreoTuong` khi dựng sự kiện, nên ở đây chỉ cần lịch hiểu "giờ như đã ghi".
          */
          timeZone="local"
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
          /*
            Lịch NHIỀU LỚP ở chế độ THÁNG: ẩn giờ trên nhãn để tên lớp có đủ chỗ.

            Ô ngày trong view tháng rộng khoảng 130px; "18 giờ " chiếm gần một phần ba, nên
            tên lớp bị cắt thành "IELTS 6.5 cấ" — không phân biệt được K1 với K6. Giờ vẫn còn
            ở tooltip (bên dưới) và ở view Tuần / Danh sách, nơi có chỗ hiển thị đầy đủ.

            Chỉ ẩn khi `hienTenLop`: lịch trong MỘT lớp nhãn chỉ là "Buổi 3", còn thừa chỗ.
          */
          displayEventTime={!(hienTenLop && cheDo === 'dayGridMonth')}
          eventClick={(arg: EventClickArg) => onChonBuoi?.(arg.event.id)}
          datesSet={(arg) => setTieuDe(arg.view.title)}
          eventDidMount={(arg) => {
            // Tooltip gốc của trình duyệt: đủ dùng, không cần thư viện tooltip cho một dòng.
            const p = arg.event.extendedProps as {
              giaoVien?: string
              phongHoc?: string | null
              laHocBu?: boolean
            }
            // Giờ vào tooltip: ở view tháng của lịch nhiều lớp, nhãn đã ẩn giờ để nhường
            // chỗ cho tên lớp, nên đây là chỗ duy nhất còn đọc được giờ mà không đổi view.
            const gio = arg.event.start
              ? arg.event.start.toLocaleTimeString('vi-VN',
                  { hour: '2-digit', minute: '2-digit', timeZone: muiGio })
              : null

            const dong = [
              arg.event.title,
              gio,
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
