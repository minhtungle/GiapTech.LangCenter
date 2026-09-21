import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { api } from '@/lib/api'
import { Button, Input, Label } from '@/components/ui'
import { LichBuoiHoc, type BuoiChoLich } from '@/components/ui/LichBuoiHoc'
import { SelectTimKiem } from '@/components/ui/SelectTimKiem'

/** `Date` → `yyyy-MM-dd` theo giờ ĐỊA PHƯƠNG (`toISOString` trả giờ UTC, lệch một ngày). */
const isoNgay = (d: Date) =>
  `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`

/**
 * Lịch học của **mọi lớp** trên một tấm lịch (21/09/2026).
 *
 * Yêu cầu: *"bổ sung chế độ xem dạng lịch như lịch học"*. Lịch trong từng lớp
 * (`LichVaDiemDanh`) chỉ vẽ buổi của **một** lớp; ở đây trả lời câu khác: *"tuần này trung tâm
 * dạy những gì, có lớp nào trùng giờ không"*.
 *
 * ## Vì sao gọi theo KHOẢNG NGÀY, không gộp từ danh sách lớp
 *
 * Bảng danh sách lớp có **phân trang** — gộp buổi từ đó thì lịch chỉ có buổi của 20 lớp đang
 * hiện, và bấm sang trang 2 lịch đổi nội dung. Endpoint `/buoi-hoc?tu=&den=` trả buổi theo
 * khoảng thời gian trên **mọi lớp người dùng được thấy** (`IPhamViLopHoc` đã lọc).
 *
 * ## Phạm vi dữ liệu là của BACKEND
 *
 * Giáo viên chỉ thấy buổi của lớp mình dạy, học viên chỉ thấy lớp mình học — do
 * `LayLichTheoKhoangHandler` lọc, không phải do màn này ẩn.
 */
export function LichTatCaLop() {
  const { t } = useTranslation()
  const navigate = useNavigate()

  /*
    Mốc tháng đang xem — lịch tự đổi khi người dùng bấm ‹ › nên phải tải theo tháng.

    Lấy rộng hơn tháng hiện tại **một tháng mỗi bên**: view "tuần" ở đầu/cuối tháng hiển thị
    vài ngày của tháng kề, thiếu dữ liệu thì những ngày đó trống một cách khó hiểu.
  */
  const [moc, setMoc] = useState(() => new Date())

  /** Khoảng ngày do NGƯỜI DÙNG chọn — rỗng = đi theo tháng đang xem. */
  const [tuNgay, setTuNgay] = useState('')
  const [denNgay, setDenNgay] = useState('')

  const khoangTuNgayThang = useMemo(() => {
    const dau = new Date(moc.getFullYear(), moc.getMonth() - 1, 1)
    const cuoi = new Date(moc.getFullYear(), moc.getMonth() + 2, 0)
    return { tu: isoNgay(dau), den: isoNgay(cuoi) }
  }, [moc])

  /*
    Người dùng chọn khoảng thì ưu tiên khoảng đó; chỉ chọn MỘT đầu cũng được
    (yêu cầu 21/09: *"lọc theo cả thời gian hoặc khoảng thời gian"*).

    Đầu còn trống thì lấy của tháng đang xem, KHÔNG để rỗng: endpoint bắt buộc cả `tu` lẫn
    `den`, thiếu một đầu là 400.
  */
  const tu = tuNgay || khoangTuNgayThang.tu
  const den = denNgay || khoangTuNgayThang.den
  const dangLocNgay = Boolean(tuNgay || denNgay)

  const { data: buoi = [], isLoading } = useQuery({
    queryKey: ['buoi-hoc', 'lich-tat-ca', tu, den],
    queryFn: async () =>
      (await api.get<BuoiChoLich[]>('/buoi-hoc', { params: { tu, den } })).data,
  })

  /** Lọc theo lớp — tấm lịch của cả trung tâm dễ rối, cần cách nhìn riêng một lớp. */
  const [locLop, setLocLop] = useState<string | null>(null)

  /*
    Danh sách lớp cho ô lọc lấy từ khoảng ĐANG XEM, nên nó đổi theo tháng. Chấp nhận được:
    lọc một lớp không có buổi trong tháng này là vô nghĩa. Nhưng nếu lớp đang lọc biến mất
    khỏi danh sách thì phải giữ nó lại, không thì ô lọc hiện rỗng mà lịch vẫn đang lọc.
  */
  const cacLop = useMemo(() => {
    const ten = new Set(buoi.map((b) => b.tenLopHoc).filter(Boolean) as string[])
    if (locLop) ten.add(locLop)
    return [...ten].sort((a, b) => a.localeCompare(b, 'vi'))
  }, [buoi, locLop])

  const buoiHienThi = useMemo(
    () => (locLop ? buoi.filter((b) => b.tenLopHoc === locLop) : buoi),
    [buoi, locLop],
  )

  const xoaLoc = () => {
    setTuNgay('')
    setDenNgay('')
    setLocLop(null)
  }

  return (
    <div className="grid gap-4">
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="lichTuNgay">{t('lopHoc.lichTuNgay')}</Label>
          <Input
            id="lichTuNgay"
            type="date"
            value={tuNgay}
            onChange={(e) => setTuNgay(e.target.value)}
            className="w-44"
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="lichDenNgay">{t('lopHoc.lichDenNgay')}</Label>
          <Input
            id="lichDenNgay"
            type="date"
            value={denNgay}
            onChange={(e) => setDenNgay(e.target.value)}
            className="w-44"
          />
        </div>

        {cacLop.length > 0 && (
          <div className="flex w-64 flex-col gap-1.5">
            <Label htmlFor="locLop">{t('lopHoc.locTheoLop')}</Label>
            <SelectTimKiem
              id="locLop"
              luaChon={cacLop.map((x) => ({ giaTri: x, nhan: x }))}
              giaTri={locLop}
              onDoi={setLocLop}
              placeholder={t('chung.tatCa')}
            />
          </div>
        )}

        {(dangLocNgay || locLop) && (
          <Button variant="outline" onClick={xoaLoc}>
            {t('lopHoc.lichXoaLoc')}
          </Button>
        )}
      </div>

      {/*
        Chỉ một dòng CHÚ THÍCH khi đang lọc ngày — không chặn lịch.

        Lúc lọc ngày, nút ‹ › của lịch vẫn đổi tháng nhưng dữ liệu đứng yên theo khoảng đã
        chọn; nói ra để người dùng không tưởng lịch hỏng.
      */}
      {dangLocNgay && (
        <p className="text-xs text-muted-foreground">{t('lopHoc.dangLocKhoangNgay')}</p>
      )}

      {isLoading && <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>}

      {/*
        LUÔN vẽ lịch, kể cả khi không có buổi nào.

        Trước 21/09 chỗ này thay cả tấm lịch bằng `TrangTrong` khi `buoi.length === 0` — người
        dùng bấm ‹ đi xa vài tháng là lịch **biến mất cùng với nút điều hướng**, kẹt luôn
        không quay lại được (chủ sản phẩm báo 21/09). FullCalendar đã có `noEventsText` cho
        tháng rỗng, không cần lớp chặn nào ở đây.
      */}
      <LichBuoiHoc
        buoi={buoiHienThi}
        // Tên lớp trên mỗi ô: đây là lịch của NHIỀU lớp, hiện "Buổi 3" thì không biết của ai.
        hienTenLop
        onChonBuoi={(id) => navigate(`/lms/buoi-hoc/${id}`)}
        onDoiThang={setMoc}
      />
    </div>
  )
}
