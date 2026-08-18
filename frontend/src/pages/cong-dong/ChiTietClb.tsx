import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ArrowLeft, MapPin, Send, Swords, Info } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import { Badge, Button, CanhBaoLoi, Card, CardContent, Table, Td, Th, TrangTrong } from '@/components/ui'
import { Anh } from '@/components/ui/Anh'
import { BANG_MAU_AO } from '@/components/soDo/loaiSan'
import { ModalThachDau } from './ModalThachDau'

/**
 * Chi tiết công khai một CLB trong Cộng đồng.
 *
 * ⚠️ Mọi thứ trên trang này là dữ liệu của CLB KHÁC. Backend quyết định lộ gì — xem
 * `ChiTietClbDto` ở `CongDongDtos.cs`; frontend chỉ hiển thị, không suy diễn thêm.
 */

interface TranCongKhai {
  thoiGian: string
  tenDoiThu: string | null
  tySoNha: number | null
  tySoKhach: number | null
  ketQua: 'ChuaCo' | 'Thang' | 'Hoa' | 'Thua'
}

interface ChiTietClb {
  maDoi: string
  tenDoi: string
  tenVietTat: string | null
  ngayThanhLap: string | null
  logoUrl: string | null
  anhBiaUrl: string | null
  khuVuc: string | null
  sanNha: string | null
  moTa: string | null
  mauAo: string[]
  soTranDaDa: number
  soThang: number
  soHoa: number
  soThua: number
  soBanThang: number
  soBanThua: number
  lichSuGanDay: TranCongKhai[]
  soTranDoiDauVoiTa: number
  dangChoPhanHoi: boolean
  dangMoiTa: boolean
}

export default function ChiTietClb() {
  const { maDoi = '' } = useParams()
  const { t, i18n } = useTranslation()
  const qc = useQueryClient()
  const navigate = useNavigate()
  const [moThachDau, setMoThachDau] = useState(false)
  const [maLoi, setMaLoi] = useState<string | null>(null)

  const { data: clb, isLoading, isError } = useQuery({
    queryKey: ['cong-dong', 'chi-tiet', maDoi],
    queryFn: async () => (await api.get<ChiTietClb>(`/cong-dong/${maDoi}`)).data,
    retry: false,
  })

  const gui = useMutation({
    mutationFn: async (form: {
      thoiGianDeXuat: string | null
      diaDiem: string | null
      loiNhan: string | null
    }) => api.post('/cong-dong/loi-moi', { maDoiNhan: maDoi, ...form }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['cong-dong'] })
      void qc.invalidateQueries({ queryKey: ['loi-moi-thach-dau'] })
      setMoThachDau(false)
      setMaLoi(null)
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  if (isLoading) return <TrangTrong thongDiep={t('chung.dangTai')} />

  // 404 là ca thường gặp (mã sai, CLB của chính mình), không phải sự cố — nói rõ và cho đường về.
  if (isError || !clb) {
    return (
      <TrangTrong
        thongDiep={t('congDong.khongTimThayClb')}
        hanhDong={
          <Button type="button" variant="outline" onClick={() => navigate('/cong-dong')}>
            <ArrowLeft className="h-4 w-4" />
            {t('congDong.veCongDong')}
          </Button>
        }
      />
    )
  }

  const ngay = (iso: string) =>
    new Date(iso).toLocaleDateString(i18n.language, {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
    })

  const hieuSo = clb.soBanThang - clb.soBanThua

  return (
    <div className="flex flex-col gap-4">
      <Link
        to="/cong-dong"
        className="flex w-fit items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground"
      >
        <ArrowLeft className="h-4 w-4" />
        {t('congDong.veCongDong')}
      </Link>

      {/* Đầu trang: nhận diện CLB */}
      <Card>
        {clb.anhBiaUrl && (
          <Anh khoa={clb.anhBiaUrl} className="h-36 w-full rounded-t-lg object-cover" />
        )}
        <CardContent className="flex flex-col gap-3 pt-5">
          <div className="flex flex-wrap items-start gap-4">
            {clb.logoUrl ? (
              <Anh khoa={clb.logoUrl} className="h-16 w-16 shrink-0 rounded-lg object-cover" />
            ) : (
              <span className="flex h-16 w-16 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-lg font-semibold text-primary">
                {(clb.tenVietTat || clb.tenDoi).slice(0, 3).toUpperCase()}
              </span>
            )}

            <div className="min-w-0 flex-1">
              <h2 className="text-xl font-semibold" style={{ overflowWrap: 'anywhere' }}>
                {clb.tenDoi}
              </h2>
              <p className="font-mono text-sm text-muted-foreground">{clb.maDoi}</p>

              <div className="mt-1.5 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-muted-foreground">
                {clb.khuVuc && (
                  <span className="flex items-center gap-1.5">
                    <MapPin className="h-3.5 w-3.5" />
                    {clb.khuVuc}
                  </span>
                )}
                {clb.sanNha && <span>{t('congDong.sanNhaLa', { san: clb.sanNha })}</span>}
                {clb.ngayThanhLap && (
                  <span>{t('congDong.thanhLap', { ngay: ngay(clb.ngayThanhLap) })}</span>
                )}
              </div>

              {clb.mauAo.length > 0 && (
                <div className="mt-2 flex flex-wrap items-center gap-1.5">
                  {clb.mauAo.map((ma) => {
                    const mau = BANG_MAU_AO.find((m) => m.ma === ma)
                    if (!mau) return null
                    return (
                      // Viền đậm hơn `border-border`: áo trắng trên nền thẻ trắng thì viền
                      // nhạt làm chấm biến mất hoàn toàn — nhìn như CLB chưa khai màu áo.
                      <span
                        key={ma}
                        title={t(`mauAo.${ma}`, ma)}
                        className="h-4 w-4 rounded-full ring-1 ring-inset ring-foreground/25"
                        style={{ background: mau.nen }}
                      />
                    )
                  })}
                </div>
              )}
            </div>

            <div className="flex flex-col items-end gap-2">
              {clb.dangMoiTa ? (
                <Link to="/hom-thu">
                  <Badge variant="accent">{t('congDong.hoDaMoiTa')}</Badge>
                </Link>
              ) : clb.dangChoPhanHoi ? (
                <Badge variant="muted">{t('congDong.dangChoPhanHoi')}</Badge>
              ) : (
                <Button type="button" onClick={() => setMoThachDau(true)}>
                  <Send className="h-4 w-4" />
                  {t('congDong.thachDau')}
                </Button>
              )}
            </div>
          </div>

          {clb.moTa && (
            <p className="text-sm text-muted-foreground" style={{ overflowWrap: 'anywhere' }}>
              {clb.moTa}
            </p>
          )}

          {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}
        </CardContent>
      </Card>

      {/* Thành tích */}
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <TheSo nhan={t('congDong.soTranDaDa')} giaTri={clb.soTranDaDa} />
        <TheSo
          nhan={t('congDong.thangHoaThua')}
          giaTri={
            <span className="tabular-nums">
              <span className="text-status-win">{clb.soThang}</span>
              <span className="text-muted-foreground"> · </span>
              <span>{clb.soHoa}</span>
              <span className="text-muted-foreground"> · </span>
              <span className="text-status-lose">{clb.soThua}</span>
            </span>
          }
        />
        <TheSo
          nhan={t('congDong.banThangThua')}
          giaTri={`${clb.soBanThang} / ${clb.soBanThua}`}
          phu={t('congDong.hieuSo', { so: hieuSo > 0 ? `+${hieuSo}` : hieuSo })}
        />
        <TheSo
          nhan={t('congDong.doiDauVoiTa')}
          giaTri={clb.soTranDoiDauVoiTa}
          phu={
            clb.soTranDoiDauVoiTa === 0 ? t('congDong.chuaTungGap') : t('congDong.theoSoCuaTa')
          }
        />
      </div>

      {/* Lịch sử đấu */}
      <Card>
        <CardContent className="flex flex-col gap-3 pt-5">
          <h3 className="flex items-center gap-2 text-base font-semibold">
            <Swords className="h-4 w-4" />
            {t('congDong.lichSuDau')}
          </h3>

          {clb.lichSuGanDay.length === 0 ? (
            <TrangTrong thongDiep={t('congDong.chuaCoTranNaoCoKetQua')} />
          ) : (
            <>
              <Table>
                <thead>
                  <tr>
                    <Th>{t('tranDau.thoiGian')}</Th>
                    <Th>{t('tranDau.doiThu')}</Th>
                    <Th className="text-center">{t('tranDau.tySo')}</Th>
                    <Th className="text-center">{t('tranDau.ketQua')}</Th>
                  </tr>
                </thead>
                <tbody>
                  {clb.lichSuGanDay.map((tr, i) => (
                    <tr key={`${tr.thoiGian}-${i}`}>
                      <Td className="whitespace-nowrap">{ngay(tr.thoiGian)}</Td>
                      <Td style={{ overflowWrap: 'anywhere' }}>
                        {tr.tenDoiThu ?? t('congDong.doiThuAn')}
                      </Td>
                      <Td className="text-center tabular-nums">
                        {tr.tySoNha ?? '–'} - {tr.tySoKhach ?? '–'}
                      </Td>
                      <Td className="text-center">
                        <Badge
                          variant={
                            tr.ketQua === 'Thang' ? 'win' : tr.ketQua === 'Thua' ? 'lose' : 'draw'
                          }
                        >
                          {t(`tranDau.kq.${tr.ketQua}`)}
                        </Badge>
                      </Td>
                    </tr>
                  ))}
                </tbody>
              </Table>

              {/* Nói rõ đây là bản rút gọn: người dùng thấy 10 dòng sẽ tưởng đội chỉ đá 10 trận. */}
              <p className="flex items-start gap-1.5 text-xs text-muted-foreground">
                <Info className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                {t('congDong.chiHien10Tran')}
              </p>
            </>
          )}
        </CardContent>
      </Card>

      {moThachDau && (
        <ModalThachDau
          tenDoi={clb.tenDoi}
          sanNhaGoiY={clb.sanNha}
          maLoi={maLoi}
          dangGui={gui.isPending}
          onDong={() => {
            setMoThachDau(false)
            setMaLoi(null)
          }}
          onGui={(form) => gui.mutate(form)}
        />
      )}
    </div>
  )
}

function TheSo({
  nhan,
  giaTri,
  phu,
}: {
  nhan: string
  giaTri: React.ReactNode
  phu?: string
}) {
  return (
    <Card>
      <CardContent className="pt-5">
        <p className="text-sm text-muted-foreground">{nhan}</p>
        <p className="mt-1 text-2xl font-semibold tabular-nums">{giaTri}</p>
        {phu && <p className="mt-0.5 text-xs text-muted-foreground">{phu}</p>}
      </CardContent>
    </Card>
  )
}
