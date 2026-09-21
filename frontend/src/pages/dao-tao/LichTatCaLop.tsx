import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { api } from '@/lib/api'
import { Card, CardContent, TrangTrong } from '@/components/ui'
import { LichBuoiHoc, type BuoiChoLich } from '@/components/ui/LichBuoiHoc'
import { SelectTimKiem } from '@/components/ui/SelectTimKiem'
import { Label } from '@/components/ui'

/**
 * Lịch học của **mọi lớp** trên một tấm lịch (21/09/2026).
 *
 * Yêu cầu chủ sản phẩm: *"phần lớp học — bổ sung chế độ xem dạng lịch như lịch học"*. Lịch
 * trong từng lớp (`LichVaDiemDanh`) chỉ vẽ buổi của **một** lớp; ở đây trả lời câu hỏi khác:
 * *"tuần này trung tâm dạy những gì, có lớp nào trùng giờ không"*.
 *
 * ## Vì sao gọi theo KHOẢNG NGÀY, không gộp từ danh sách lớp
 *
 * Bảng danh sách lớp có **phân trang** — gộp buổi từ đó thì lịch chỉ có buổi của 20 lớp đang
 * hiện, và bấm sang trang 2 lịch đổi nội dung. Endpoint `/buoi-hoc?tu=&den=` trả buổi theo
 * khoảng thời gian trên **mọi lớp người dùng được thấy** (`IPhamViLopHoc` đã lọc), đúng thứ
 * một tấm lịch cần.
 *
 * ## Phạm vi dữ liệu là của BACKEND
 *
 * Giáo viên chỉ thấy buổi của lớp mình dạy, học viên chỉ thấy lớp mình học — do
 * `LayLichTheoKhoangHandler` lọc, không phải do màn này ẩn. Nên không cần (và không được)
 * kiểm quyền lại ở đây.
 */
export function LichTatCaLop() {
  const { t } = useTranslation()
  const navigate = useNavigate()

  /*
    Mốc tháng đang xem. Lịch tự đổi tháng khi người dùng bấm ‹ › nên phải tải theo tháng, không
    tải một lần rồi thôi.

    Lấy rộng hơn tháng hiện tại **một tháng mỗi bên**: view "tuần" ở đầu/cuối tháng hiển thị vài
    ngày của tháng kề, thiếu dữ liệu thì những ngày đó trống một cách khó hiểu.
  */
  const [moc, setMoc] = useState(() => new Date())

  const { tu, den } = useMemo(() => {
    const dau = new Date(moc.getFullYear(), moc.getMonth() - 1, 1)
    const cuoi = new Date(moc.getFullYear(), moc.getMonth() + 2, 0)
    const iso = (d: Date) =>
      `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
    return { tu: iso(dau), den: iso(cuoi) }
  }, [moc])

  const { data: buoi = [], isLoading } = useQuery({
    queryKey: ['buoi-hoc', 'lich-tat-ca', tu, den],
    queryFn: async () =>
      (await api.get<BuoiChoLich[]>('/buoi-hoc', { params: { tu, den } })).data,
  })

  /** Lọc theo lớp — tấm lịch của cả trung tâm dễ rối, cần cách nhìn riêng một lớp. */
  const [locLop, setLocLop] = useState<string | null>(null)

  const cacLop = useMemo(() => {
    const ten = [...new Set(buoi.map((b) => b.tenLopHoc).filter(Boolean))] as string[]
    return ten.sort((a, b) => a.localeCompare(b, 'vi'))
  }, [buoi])

  const buoiHienThi = useMemo(
    () => (locLop ? buoi.filter((b) => b.tenLopHoc === locLop) : buoi),
    [buoi, locLop],
  )

  return (
    <Card>
      <CardContent className="grid gap-4 pt-5">
        {cacLop.length > 1 && (
          <div className="flex w-72 flex-col gap-1.5">
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

        {isLoading ? (
          <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
        ) : buoi.length === 0 ? (
          <TrangTrong thongDiep={t('lopHoc.lichChuaCoBuoi')} />
        ) : (
          <LichBuoiHoc
            buoi={buoiHienThi}
            // Tên lớp trên mỗi ô: đây là lịch của NHIỀU lớp, hiện "Buổi 3" thì không biết của ai.
            hienTenLop
            onChonBuoi={(id) => navigate(`/lms/buoi-hoc/${id}`)}
            onDoiThang={setMoc}
          />
        )}
      </CardContent>
    </Card>
  )
}
