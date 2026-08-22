import { useEffect, useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { CalendarDays, MapPin, MessageSquare, ShieldAlert } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import { useAuth } from '@/lib/auth'
import {
  Button, CanhBaoLoi, Card, CardContent, CardDescription, CardHeader, CardTitle, Textarea,
} from '@/components/ui'
import { Anh } from '@/components/ui/Anh'

/**
 * Trang xem lời mời qua link — FR-18.
 *
 * KHÔNG cần đăng nhập để XEM: người nhận có thể chưa có tài khoản, bắt đăng nhập trước khi xem
 * là yêu cầu họ tạo đội cho một lời mời họ chưa biết nội dung.
 *
 * Chấp nhận thì cần đăng nhập — nó ghi vào lịch của cả hai CLB.
 */

type TinhTrang =
  | 'ConHieuLuc' | 'HetHan' | 'DaThuHoi' | 'DaChapNhan' | 'DaTuChoi' | 'TranKhongCon'

interface XemLoiMoi {
  tinhTrang: TinhTrang
  tenClbMoi: string
  maDoiClbMoi: string
  logoClbMoi: string | null
  khuVucClbMoi: string | null
  tenDoiDuocMoi: string
  thoiGianDeXuat: string | null
  diaDiem: string | null
  loiNhan: string | null
  hetHan: string
}

export default function XemLoiMoiLink() {
  const [sp] = useSearchParams()
  const token = sp.get('token') ?? ''
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const { phien } = useAuth()
  const [phanHoi, setPhanHoi] = useState('')
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [xong, setXong] = useState<{ chapNhan: boolean; tranId: string | null } | null>(null)

  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['xem-loi-moi-link', token],
    // POST dù là đọc: token trong body không đi vào log truy cập, history trình duyệt, hay
    // header Referer như khi đặt trên URL.
    queryFn: async () =>
      (await api.post<XemLoiMoi>('/moi-qua-link/xem', { token })).data,
    enabled: token.length > 0,
    retry: false,
  })

  const traLoi = useMutation({
    mutationFn: async (chapNhan: boolean) =>
      (
        await api.post<{ daChapNhan: boolean; tranDauCuaToi: string | null }>(
          '/moi-qua-link/tra-loi',
          { token, chapNhan, phanHoi: phanHoi.trim() || null },
        )
      ).data,
    onSuccess: (kq) => {
      setXong({ chapNhan: kq.daChapNhan, tranId: kq.tranDauCuaToi })
      setMaLoi(null)
      void refetch()
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  // Sau khi đăng nhập, quay lại đúng link này — không thì người dùng mất lời mời và phải mở
  // lại từ Zalo.
  useEffect(() => {
    if (token) sessionStorage.setItem('quayLaiSauDangNhap', `/loi-moi?token=${token}`)
  }, [token])

  if (!token) return <ThongBao tieuDe={t('moiLink.thieuToken')} />
  if (isLoading) return <ThongBao tieuDe={t('chung.dangTai')} />
  if (isError || !data) return <ThongBao tieuDe={t('moiLink.khongTimThay')} moTa={t('moiLink.khongTimThayMoTa')} />

  const gioDep = (iso: string | null) =>
    iso
      ? new Date(iso).toLocaleString(i18n.language, {
          weekday: 'long', day: '2-digit', month: '2-digit', year: 'numeric',
          hour: '2-digit', minute: '2-digit',
        })
      : t('moiLink.chuaHenGio')

  // Trạng thái không còn dùng được — nói RÕ lý do, không phải một trang lỗi chung.
  if (data.tinhTrang !== 'ConHieuLuc' && !xong) {
    const theo: Record<string, { tieuDe: string; moTa: string }> = {
      HetHan: { tieuDe: t('moiLink.hetHan'), moTa: t('moiLink.hetHanMoTa', { ten: data.tenClbMoi }) },
      DaThuHoi: { tieuDe: t('moiLink.daThuHoi'), moTa: t('moiLink.daThuHoiMoTa', { ten: data.tenClbMoi }) },
      DaChapNhan: { tieuDe: t('moiLink.daChapNhan'), moTa: t('moiLink.daChapNhanMoTa') },
      DaTuChoi: { tieuDe: t('moiLink.daTuChoi'), moTa: t('moiLink.daTuChoiMoTa') },
    }
    const n = theo[data.tinhTrang] ?? { tieuDe: t('moiLink.khongTimThay'), moTa: '' }
    return <ThongBao tieuDe={n.tieuDe} moTa={n.moTa} />
  }

  if (xong) {
    return (
      <ThongBao
        tieuDe={xong.chapNhan ? t('moiLink.daNhanLoiMoi') : t('moiLink.daTuChoiXong')}
        moTa={
          xong.chapNhan
            ? t('moiLink.daNhanMoTa', { ten: data.tenClbMoi })
            : t('moiLink.daTuChoiXongMoTa')
        }
        hanhDong={
          xong.chapNhan && xong.tranId ? (
            <Button onClick={() => navigate(`/lich-thi-dau/${xong.tranId}`)}>
              {t('moiLink.xemTran')}
            </Button>
          ) : (
            <Button variant="outline" onClick={() => navigate('/')}>
              {t('moiLink.vaoHeThong')}
            </Button>
          )
        }
      />
    )
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-muted/30 px-4 py-8">
      <Card className="w-full max-w-lg">
        <CardHeader>
          <div className="flex items-start gap-3">
            {data.logoClbMoi ? (
              <Anh khoa={data.logoClbMoi} className="h-12 w-12 shrink-0 rounded-md object-cover" />
            ) : (
              <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-md bg-primary/10 text-sm font-semibold text-primary">
                {data.tenClbMoi.slice(0, 3).toUpperCase()}
              </span>
            )}
            <div className="min-w-0">
              <CardTitle className="text-lg" style={{ overflowWrap: 'anywhere' }}>
                {t('moiLink.tieuDe', { ten: data.tenClbMoi })}
              </CardTitle>
              <CardDescription>
                <span className="font-mono">{data.maDoiClbMoi}</span>
                {data.khuVucClbMoi && ` · ${data.khuVucClbMoi}`}
              </CardDescription>
            </div>
          </div>
        </CardHeader>

        <CardContent className="flex flex-col gap-4">
          {/* Tên đối thủ như bên mời đã gõ — giúp người nhận nhận ra đây là mình. */}
          <p className="rounded-md bg-muted/50 p-3 text-sm">
            {t('moiLink.hoGoiBanLa')} <strong>{data.tenDoiDuocMoi}</strong>
          </p>

          <div className="flex flex-col gap-2 text-sm">
            <p className="flex items-start gap-2">
              <CalendarDays className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" />
              {gioDep(data.thoiGianDeXuat)}
            </p>
            {data.diaDiem && (
              <p className="flex items-start gap-2">
                <MapPin className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" />
                {data.diaDiem}
              </p>
            )}
            {data.loiNhan && (
              <p className="flex items-start gap-2" style={{ overflowWrap: 'anywhere' }}>
                <MessageSquare className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" />
                {data.loiNhan}
              </p>
            )}
          </div>

          {/* Chưa đăng nhập: nói rõ hai đường đi, đừng để họ đoán. */}
          {!phien ? (
            <div className="flex flex-col gap-3 border-t border-border pt-4">
              <p className="text-sm text-muted-foreground">{t('moiLink.canDangNhap')}</p>
              <div className="flex flex-wrap gap-2">
                <Button onClick={() => navigate('/dang-nhap')}>{t('moiLink.dangNhapDeNhan')}</Button>
                <Button variant="outline" onClick={() => navigate('/dang-ky')}>
                  {t('moiLink.taoDoiMoi')}
                </Button>
              </div>
            </div>
          ) : (
            <div className="flex flex-col gap-3 border-t border-border pt-4">
              {/* Ca 5: xác nhận danh tính. Link chia sẻ được nên người bấm có thể không phải
                  người được mời — bắt họ đọc tên CLB mình trước khi chấp nhận. */}
              <div className="flex items-start gap-2 rounded-md border border-border bg-muted/40 p-3 text-sm">
                <ShieldAlert className="mt-0.5 h-4 w-4 shrink-0 text-[hsl(var(--status-draw))]" />
                <span>
                  {t('moiLink.xacNhanDanhTinh')}{' '}
                  <strong>{phien.tenDoi ?? phien.maDoi}</strong>{' '}
                  <span className="font-mono text-xs">({phien.maDoi})</span>
                </span>
              </div>

              <Textarea
                rows={2}
                value={phanHoi}
                onChange={(e) => setPhanHoi(e.target.value)}
                placeholder={t('moiLink.phanHoiGoiY')}
              />

              {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

              <div className="flex flex-wrap gap-2">
                <Button disabled={traLoi.isPending} onClick={() => traLoi.mutate(true)}>
                  {traLoi.isPending ? t('chung.dangTai') : t('moiLink.dongY')}
                </Button>
                <Button
                  variant="outline"
                  disabled={traLoi.isPending}
                  onClick={() => traLoi.mutate(false)}
                >
                  {t('moiLink.tuChoi')}
                </Button>
              </div>

              {/* Nói trước việc đồng ý sẽ tạo trận: bấm rồi thấy trận tự mọc trong lịch sẽ
                  tưởng hệ thống làm sai. */}
              <p className="text-xs text-muted-foreground">{t('moiLink.dongYGiaiThich')}</p>
            </div>
          )}

          <p className="text-center text-xs text-muted-foreground">
            {t('moiLink.hanDen', { ngay: new Date(data.hetHan).toLocaleDateString(i18n.language) })}
          </p>
        </CardContent>
      </Card>
    </div>
  )
}

function ThongBao({
  tieuDe,
  moTa,
  hanhDong,
}: {
  tieuDe: string
  moTa?: string
  hanhDong?: React.ReactNode
}) {
  const { t } = useTranslation()
  return (
    <div className="flex min-h-screen items-center justify-center bg-muted/30 px-4">
      <Card className="w-full max-w-md">
        <CardHeader>
          <CardTitle className="text-lg">{tieuDe}</CardTitle>
          {moTa && <CardDescription>{moTa}</CardDescription>}
        </CardHeader>
        <CardContent className="flex flex-wrap items-center gap-3">
          {hanhDong}
          <Link to="/dang-nhap" className="text-sm text-primary hover:underline">
            {t('moiLink.veDangNhap')}
          </Link>
        </CardContent>
      </Card>
    </div>
  )
}
