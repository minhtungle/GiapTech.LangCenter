import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import {
  CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis,
} from 'recharts'
import { Heart, Shield, Star, Target } from 'lucide-react'
import { api } from '@/lib/api'
import { Badge, Table, Td, Th, TrangTrong } from '@/components/ui'
import {
  BoLocTranDau, NutBoLoc, BO_LOC_RONG, sangThamSoApi, type GiaTriBoLoc,
} from '@/components/BoLocTranDau'
import { cn } from '@/lib/utils'

type KetQua = 'ChuaCo' | 'Thang' | 'Hoa' | 'Thua'

interface KpiDto {
  tongTran: number
  soTran: number
  thang: number
  hoa: number
  thua: number
  tongBanThang: number
  tongBanThua: number
  tyLeThang: number
}
interface DiemBieuDoDto {
  tranDauId: string
  thoiGian: string
  tenDoiThu: string | null
  banThang: number
  banThua: number
  ketQua: KetQua
}
interface XepHangDto {
  cauThuId: string
  hoTen: string
  soAo: number | null
  soPhieuMvp: number
  tongBanThang: number
  tongBanCuuThua: number
  diemKyNang: number | null
  soTranThamGia: number
}
interface ThongKeDto {
  kpi: KpiDto
  dienBien: DiemBieuDoDto[]
  xepHang: XepHangDto[]
}

/** Bốn tiêu chí xếp hạng của FR-14. */
type TieuChi = 'mvp' | 'kyNang' | 'banThang' | 'cuuThua'

/**
 * FR-12 → FR-14 — thống kê.
 *
 * Bố cục theo nguyên tắc UI/UX: **KPI card gọn ở đầu**, biểu đồ chi tiết bên dưới.
 *
 * Bộ lọc dùng chung component với Lịch thi đấu (FR-12 yêu cầu rõ) và **ảnh hưởng cả ba phần**:
 * KPI, biểu đồ và bảng xếp hạng đều chỉ tính các trận trong khoảng lọc.
 */
export default function ThongKe() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [moLoc, setMoLoc] = useState(false)
  const [loc, setLoc] = useState<GiaTriBoLoc>(BO_LOC_RONG)
  const [tieuChi, setTieuChi] = useState<TieuChi>('mvp')

  const { data, isLoading } = useQuery({
    queryKey: ['thong-ke', loc],
    queryFn: async () => (await api.post<ThongKeDto>('/thong-ke', sangThamSoApi(loc))).data,
  })

  const kpi = data?.kpi

  /**
   * Sắp bảng xếp hạng theo tiêu chí đang chọn.
   *
   * Sắp ở client vì DTO đã mang đủ bốn tiêu chí — đổi cột không cần gọi lại API, và số cầu
   * thủ của một CLB phong trào chỉ vài chục dòng.
   */
  const xepHang = [...(data?.xepHang ?? [])].sort((a, b) => {
    const lay = (x: XepHangDto) =>
      tieuChi === 'mvp' ? x.soPhieuMvp
        : tieuChi === 'banThang' ? x.tongBanThang
        : tieuChi === 'cuuThua' ? x.tongBanCuuThua
        // Chưa chấm điểm xếp cuối thay vì lẫn lên đầu với giá trị 0.
        : (x.diemKyNang ?? -1)
    return lay(b) - lay(a) || a.hoTen.localeCompare(b.hoTen, 'vi')
  })

  const cacTieuChi: { khoa: TieuChi; nhan: string; icon: React.ReactNode }[] = [
    { khoa: 'mvp', nhan: t('thongKe.tc.mvp'), icon: <Heart className="h-3.5 w-3.5" /> },
    { khoa: 'kyNang', nhan: t('thongKe.tc.kyNang'), icon: <Star className="h-3.5 w-3.5" /> },
    { khoa: 'banThang', nhan: t('thongKe.tc.banThang'), icon: <Target className="h-3.5 w-3.5" /> },
    { khoa: 'cuuThua', nhan: t('thongKe.tc.cuuThua'), icon: <Shield className="h-3.5 w-3.5" /> },
  ]

  /** Dữ liệu cho Recharts — nhãn trục X là ngày, đủ ngắn để không chồng nhau. */
  const duLieuBieuDo = (data?.dienBien ?? []).map((d) => ({
    ...d,
    nhan: new Date(d.thoiGian).toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit' }),
  }))

  return (
    <div className="flex flex-col gap-4">
      <div className="flex justify-end">
        <NutBoLoc mo={moLoc} onDoi={setMoLoc} loc={loc} />
      </div>

      {moLoc && <BoLocTranDau loc={loc} onDoi={setLoc} />}

      {isLoading ? (
        <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
      ) : (
        <>
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            <TheKpi
              nhan={t('thongKe.tyLeThang')}
              giaTri={`${kpi?.tyLeThang ?? 0}%`}
              phu={t('thongKe.tren', { so: kpi?.soTran ?? 0 })}
              mau="primary"
            />
            <TheKpi
              nhan={t('thongKe.thangHoaThua')}
              giaTri={`${kpi?.thang ?? 0} · ${kpi?.hoa ?? 0} · ${kpi?.thua ?? 0}`}
              phu={t('thongKe.thangHoaThuaGoiY')}
              mau="win"
            />
            <TheKpi
              nhan={t('thongKe.banThang')}
              giaTri={String(kpi?.tongBanThang ?? 0)}
              phu={t('thongKe.hieuSo', {
                so: (kpi?.tongBanThang ?? 0) - (kpi?.tongBanThua ?? 0),
              })}
              mau="win"
            />
            <TheKpi
              nhan={t('thongKe.banThua')}
              giaTri={String(kpi?.tongBanThua ?? 0)}
              mau="lose"
            />
          </div>

          {/* FR-13 — biểu đồ diễn biến */}
          <section className="rounded-lg border border-border p-4">
            <h2 className="mb-1 text-base font-semibold">{t('thongKe.dienBien')}</h2>
            <p className="mb-3 text-xs text-muted-foreground">{t('thongKe.dienBienGoiY')}</p>

            {duLieuBieuDo.length === 0 ? (
              <TrangTrong thongDiep={t('thongKe.chuaCoTranDaDau')} />
            ) : (
              <ResponsiveContainer width="100%" height={220}>
                <LineChart
                  data={duLieuBieuDo}
                  margin={{ top: 8, right: 8, bottom: 0, left: -20 }}
                  /*
                    FR-13: bấm vào một điểm → mở chi tiết trận đó.

                    Recharts v3 bỏ `activePayload` khỏi tham số onClick, chỉ còn `activeIndex`
                    — tra ngược vào mảng dữ liệu thay vì đọc payload như v2.
                  */
                  onClick={(e) => {
                    const i = Number(e?.activeIndex)
                    const diem = Number.isInteger(i) ? duLieuBieuDo[i] : undefined
                    if (diem) navigate(`/lich-thi-dau/${diem.tranDauId}`)
                  }}
                  style={{ cursor: 'pointer' }}
                >
                  <CartesianGrid strokeDasharray="3 3" stroke="hsl(var(--border))" />
                  <XAxis dataKey="nhan" fontSize={12} stroke="hsl(var(--muted-foreground))" />
                  {/* allowDecimals=false: bàn thắng là số nguyên, trục 0.5 bàn là vô nghĩa. */}
                  <YAxis fontSize={12} allowDecimals={false} stroke="hsl(var(--muted-foreground))" />
                  <Tooltip content={<GoiY />} />
                  <Legend />
                  <Line
                    type="monotone"
                    dataKey="banThang"
                    name={t('thongKe.banThang')}
                    stroke="hsl(var(--status-win))"
                    strokeWidth={2}
                    dot={{ r: 4 }}
                    activeDot={{ r: 6 }}
                  />
                  <Line
                    type="monotone"
                    dataKey="banThua"
                    name={t('thongKe.banThua')}
                    stroke="hsl(var(--status-lose))"
                    strokeWidth={2}
                    dot={{ r: 4 }}
                    activeDot={{ r: 6 }}
                  />
                </LineChart>
              </ResponsiveContainer>
            )}
          </section>

          {/* FR-14 — bảng xếp hạng MVP */}
          <section className="flex flex-col gap-3 rounded-lg border border-border p-4">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <h2 className="text-base font-semibold">{t('thongKe.xepHang')}</h2>
              <div className="flex flex-wrap rounded-md border border-border">
                {cacTieuChi.map((tc) => (
                  <button
                    key={tc.khoa}
                    type="button"
                    onClick={() => setTieuChi(tc.khoa)}
                    className={cn(
                      'flex items-center gap-1.5 px-2.5 py-1 text-xs first:rounded-l-md last:rounded-r-md',
                      tieuChi === tc.khoa
                        ? 'bg-primary font-medium text-primary-foreground'
                        : 'hover:bg-muted',
                    )}
                  >
                    {tc.icon}
                    {tc.nhan}
                  </button>
                ))}
              </div>
            </div>

            {xepHang.length === 0 ? (
              <TrangTrong thongDiep={t('thongKe.chuaCoDuLieuXepHang')} />
            ) : (
              <Table caoToiDa="max-h-[24rem]">
                <thead>
                  <tr>
                    <Th className="w-12">#</Th>
                    <Th>{t('cauThu.hoTen')}</Th>
                    <Th className="w-20">{t('thongKe.soTran')}</Th>
                    <Th className={cn('w-24', tieuChi === 'mvp' && 'text-primary')}>
                      {t('thongKe.tc.mvp')}
                    </Th>
                    <Th className={cn('w-28', tieuChi === 'kyNang' && 'text-primary')}>
                      {t('thongKe.tc.kyNang')}
                    </Th>
                    <Th className={cn('w-24', tieuChi === 'banThang' && 'text-primary')}>
                      {t('thongKe.tc.banThang')}
                    </Th>
                    <Th className={cn('w-24', tieuChi === 'cuuThua' && 'text-primary')}>
                      {t('thongKe.tc.cuuThua')}
                    </Th>
                  </tr>
                </thead>
                <tbody>
                  {xepHang.map((x, i) => (
                    <tr key={x.cauThuId} className="hover:bg-muted/40">
                      <Td>
                        {/* Ba hạng đầu nổi bật — bảng xếp hạng mà không thấy ngay ai nhất
                            thì chỉ là một bảng số. */}
                        <span
                          className={cn(
                            'flex h-6 w-6 items-center justify-center rounded-full text-xs font-bold',
                            i === 0 && 'bg-[hsl(var(--status-draw))] text-white',
                            i === 1 && 'bg-muted-foreground/30',
                            i === 2 && 'bg-[hsl(var(--accent))]/30',
                            i > 2 && 'text-muted-foreground',
                          )}
                        >
                          {i + 1}
                        </span>
                      </Td>
                      <Td className="font-medium">
                        {x.soAo !== null && (
                          <span className="mr-1.5 inline-flex h-5 w-5 items-center justify-center rounded-full bg-primary/10 text-[10px] font-bold text-primary">
                            {x.soAo}
                          </span>
                        )}
                        {x.hoTen}
                      </Td>
                      <Td className="text-muted-foreground">{x.soTranThamGia}</Td>
                      <O noiBat={tieuChi === 'mvp'}>{x.soPhieuMvp || '—'}</O>
                      <O noiBat={tieuChi === 'kyNang'}>
                        {x.diemKyNang !== null ? (
                          <Badge variant={x.diemKyNang >= 7 ? 'win' : 'muted'}>
                            {x.diemKyNang}
                          </Badge>
                        ) : (
                          '—'
                        )}
                      </O>
                      <O noiBat={tieuChi === 'banThang'}>{x.tongBanThang || '—'}</O>
                      <O noiBat={tieuChi === 'cuuThua'}>{x.tongBanCuuThua || '—'}</O>
                    </tr>
                  ))}
                </tbody>
              </Table>
            )}
          </section>
        </>
      )}
    </div>
  )
}

/** Ô số của bảng xếp hạng — cột đang xếp được in đậm để mắt bám đúng cột. */
function O({ noiBat, children }: { noiBat: boolean; children: React.ReactNode }) {
  return (
    <Td className={cn('tabular-nums', noiBat ? 'font-bold text-primary' : 'text-muted-foreground')}>
      {children}
    </Td>
  )
}

function TheKpi({
  nhan,
  giaTri,
  phu,
  mau,
}: {
  nhan: string
  giaTri: string
  phu?: string
  mau: 'primary' | 'win' | 'lose'
}) {
  return (
    <div className="rounded-lg border border-border p-3">
      <p className="text-xs text-muted-foreground">{nhan}</p>
      <p
        className={cn(
          'mt-1 text-2xl font-bold tabular-nums',
          mau === 'primary' && 'text-primary',
          mau === 'win' && 'text-status-win',
          mau === 'lose' && 'text-status-lose',
        )}
      >
        {giaTri}
      </p>
      {phu && <p className="mt-0.5 text-xs text-muted-foreground">{phu}</p>}
    </div>
  )
}

/** Tooltip riêng: hiện tên đối thủ và tỷ số thay vì hai dòng số rời rạc. */
function GoiY({ active, payload }: { active?: boolean; payload?: { payload: DiemBieuDoDto }[] }) {
  const { t } = useTranslation()
  if (!active || !payload?.length) return null

  const d = payload[0].payload
  return (
    <div className="rounded-md border border-border bg-card p-2 text-xs shadow-lg">
      <p className="font-medium">
        {new Date(d.thoiGian).toLocaleDateString('vi-VN')}
        {d.tenDoiThu && <span className="text-muted-foreground"> · {d.tenDoiThu}</span>}
      </p>
      <p className="mt-1 font-mono font-semibold">
        {d.banThang} – {d.banThua}
      </p>
      <p className="mt-1 text-muted-foreground">{t('thongKe.bamDeXem')}</p>
    </div>
  )
}
