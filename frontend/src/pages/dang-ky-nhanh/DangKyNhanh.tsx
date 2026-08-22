import { useMemo, useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import axios from 'axios'
import { Check, CircleAlert, Swords } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import {
  Badge, Button, Card, CardContent, CardDescription, CardHeader, CardTitle, Label, Textarea,
} from '@/components/ui'

/**
 * FR-19 — trang đăng ký đá trận cho người **không có tài khoản**.
 *
 * Người dùng vào từ link/QR trong nhóm chat. Không đăng nhập, chọn tên mình từ danh sách, bấm
 * Tham gia / Chưa chắc / Không.
 *
 * Token đọc từ query string (QR buộc phải nhúng vào URL) nhưng **gửi lên trong body** — URL vào
 * access log, vào history trình duyệt, và vào header Referer.
 *
 * Chọn xong vẫn **sửa lại được**: khoá cứng thì người mở link đầu tiên có thể chọn hộ người khác
 * rồi khoá luôn họ, mà không ai biết (quyết định 21/08).
 */

interface TenDeChon {
  id: string
  hoTen: string
  soAo: number | null
  daTraLoi: string
  quaLink: boolean
}

interface TrangDto {
  tenDoiNha: string
  tenDoiThu: string
  thoiGian: string | null
  loiNhan: string | null
  hanTraLoi: string | null
  lichSu: { soTran: number; thang: number; hoa: number; thua: number }
  danhSachTen: TenDeChon[]
}

/**
 * Khớp `TraLoiThamGia` ở backend — API serialize enum thành **CHUỖI** tên, không phải số.
 * Xem ghi chú dài hơn ở `TabDangKy.tsx`: dùng số làm mọi phép so sai im lặng.
 */
const CHUA_TRA_LOI = 'ChuaTraLoi'
const THAM_GIA = 'ThamGia'
const KHONG_THAM_GIA = 'KhongThamGia'
const CHUA_CHAC = 'ChuaChac'

export default function DangKyNhanh() {
  const { t } = useTranslation()
  const [params] = useSearchParams()
  const token = params.get('token') ?? ''

  const [cauThuId, setCauThuId] = useState('')
  const [ghiChu, setGhiChu] = useState('')
  const [daGui, setDaGui] = useState<string | null>(null)
  const [maLoi, setMaLoi] = useState<string | null>(null)

  const { data, error, isLoading, refetch } = useQuery({
    queryKey: ['dang-ky-nhanh', token],
    enabled: token.length > 0,
    retry: false,
    queryFn: async () => (await api.post<TrangDto>('/dang-ky-nhanh/xem', { token })).data,
  })

  const traLoi = useMutation({
    mutationFn: async (tl: string) =>
      api.post('/dang-ky-nhanh/tra-loi', { token, cauThuId, traLoi: tl, ghiChu: ghiChu.trim() || null }),
    onSuccess: (_r, tl) => {
      setDaGui(tl)
      setMaLoi(null)
      void refetch()
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const nguoiDangChon = useMemo(
    () => data?.danhSachTen.find((x) => x.id === cauThuId),
    [data, cauThuId],
  )

  if (!token) return <Loi ma="LINK_DANG_KY_HET_HAN" />

  if (isLoading)
    return (
      <Khung>
        <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
      </Khung>
    )

  // Link hết hạn / bị thu hồi / đã đóng / trận không còn — mỗi lý do một câu riêng, và KHÔNG
  // phải trang 404: 404 làm người dùng tưởng link sai rồi bỏ luôn thay vì liên hệ trưởng nhóm.
  if (error || !data)
    return <Loi ma={axios.isAxiosError(error) ? layMaLoi(error) : 'LINK_DANG_KY_HET_HAN'} />

  return (
    <Khung>
      <Card className="w-full max-w-lg">
        <CardHeader>
          <CardTitle className="text-lg">{t('dangKyNhanh.trangTieuDe')}</CardTitle>
          <CardDescription>{data.tenDoiNha}</CardDescription>
        </CardHeader>

        <CardContent className="flex flex-col gap-4">
          {/* Thông tin trận: không có giờ thì không ai trả lời được là có đá được hay không. */}
          <div className="flex flex-col gap-1.5 rounded-md bg-muted/50 p-3 text-sm">
            <div className="flex items-center gap-2 font-semibold">
              <Swords className="h-4 w-4 shrink-0 text-primary" />
              {data.tenDoiThu}
            </div>
            {data.thoiGian && (
              <div className="font-medium">
                {new Date(data.thoiGian).toLocaleString('vi-VN', {
                  weekday: 'long', day: '2-digit', month: '2-digit',
                  hour: '2-digit', minute: '2-digit',
                })}
              </div>
            )}
            {data.loiNhan && <p className="pt-1 text-muted-foreground">{data.loiNhan}</p>}
          </div>

          {/* Lịch sử đối đầu — chủ sản phẩm yêu cầu. Chưa đá lần nào thì nói rõ thay vì hiện 0-0-0. */}
          <div className="text-sm">
            <span className="text-muted-foreground">{t('dangKyNhanh.lichSuDoiDau')}: </span>
            {data.lichSu.soTran === 0 ? (
              <span className="text-muted-foreground">{t('dangKyNhanh.chuaDaLanNao')}</span>
            ) : (
              <span>
                {t('dangKyNhanh.soTran', { so: data.lichSu.soTran })}{' '}
                <span className="text-[hsl(var(--status-win))]">{data.lichSu.thang}</span>
                {' · '}
                <span className="text-[hsl(var(--status-draw))]">{data.lichSu.hoa}</span>
                {' · '}
                <span className="text-destructive">{data.lichSu.thua}</span>
              </span>
            )}
          </div>

          <div>
            <Label htmlFor="dknTen">{t('dangKyNhanh.chonTen')}</Label>
            <select
              id="dknTen"
              value={cauThuId}
              onChange={(e) => {
                setCauThuId(e.target.value)
                setDaGui(null)
              }}
              className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm"
            >
              <option value="">{t('dangKyNhanh.chonTenGoiY')}</option>
              {data.danhSachTen.map((x) => (
                <option key={x.id} value={x.id}>
                  {x.soAo !== null ? `${x.soAo}. ` : ''}
                  {x.hoTen}
                  {/* Ai đã trả lời vẫn CHỌN ĐƯỢC — bấm nhầm hoặc bị người khác chọn hộ thì
                      sửa lại. Chỉ đánh dấu để họ biết mình đang đổi gì. */}
                  {x.daTraLoi !== CHUA_TRA_LOI ? ` — ${t('dangKyNhanh.daTraLoi')}` : ''}
                </option>
              ))}
            </select>
          </div>

          {nguoiDangChon && nguoiDangChon.daTraLoi !== CHUA_TRA_LOI && (
            <p className="flex items-start gap-1.5 text-xs text-[hsl(var(--status-draw))]">
              <CircleAlert className="mt-0.5 h-3.5 w-3.5 shrink-0" />
              {t('dangKyNhanh.canhBaoDaTraLoi', {
                traLoi:
                  nguoiDangChon.daTraLoi === THAM_GIA
                    ? t('homThu.tl.ThamGia')
                    : nguoiDangChon.daTraLoi === KHONG_THAM_GIA
                      ? t('homThu.tl.KhongThamGia')
                      : t('homThu.tl.ChuaChac'),
              })}
            </p>
          )}

          <div>
            <Label htmlFor="dknGhiChu">{t('dangKyNhanh.ghiChu')}</Label>
            <Textarea
              id="dknGhiChu"
              rows={2}
              value={ghiChu}
              onChange={(e) => setGhiChu(e.target.value)}
              placeholder={t('dangKyNhanh.ghiChuGoiY')}
            />
          </div>

          {daGui !== null ? (
            <div className="flex items-center gap-2 rounded-md bg-[hsl(var(--status-win))]/10 p-3 text-sm text-[hsl(var(--status-win))]">
              <Check className="h-4 w-4 shrink-0" />
              {t('dangKyNhanh.daGhiNhan', {
                ten: nguoiDangChon?.hoTen ?? '',
                traLoi:
                  daGui === THAM_GIA
                    ? t('homThu.tl.ThamGia')
                    : daGui === KHONG_THAM_GIA
                      ? t('homThu.tl.KhongThamGia')
                      : t('homThu.tl.ChuaChac'),
              })}
            </div>
          ) : null}

          {maLoi && (
            <p className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}
            </p>
          )}

          <div className="grid grid-cols-3 gap-2">
            <Button
              disabled={!cauThuId || traLoi.isPending}
              onClick={() => traLoi.mutate(THAM_GIA)}
            >
              {t('homThu.tl.ThamGia')}
            </Button>
            <Button
              variant="outline"
              disabled={!cauThuId || traLoi.isPending}
              onClick={() => traLoi.mutate(CHUA_CHAC)}
            >
              {t('homThu.tl.ChuaChac')}
            </Button>
            <Button
              variant="outline"
              disabled={!cauThuId || traLoi.isPending}
              onClick={() => traLoi.mutate(KHONG_THAM_GIA)}
            >
              {t('homThu.tl.KhongThamGia')}
            </Button>
          </div>

          {/* Nói rõ là sửa được, nếu không họ sợ bấm sai rồi không dám bấm. */}
          <p className="text-center text-xs text-muted-foreground">
            {t('dangKyNhanh.suaDuoc')}
          </p>
        </CardContent>
      </Card>
    </Khung>
  )
}

function Khung({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-screen items-center justify-center bg-muted/30 px-4 py-8">
      {children}
    </div>
  )
}

function Loi({ ma }: { ma: string }) {
  const { t } = useTranslation()

  return (
    <Khung>
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <CircleAlert className="h-5 w-5 shrink-0 text-destructive" />
            {t('dangKyNhanh.khongDungDuoc')}
          </CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          <p className="text-sm text-muted-foreground">
            {t(`loi.${ma}`, t('loi.LOI_HE_THONG'))}
          </p>
          {/* Nói phải làm gì tiếp: không có câu này thì họ bỏ luôn thay vì hỏi trưởng nhóm. */}
          <Badge variant="muted">{t('dangKyNhanh.lienHeTruongNhom')}</Badge>
        </CardContent>
      </Card>
    </Khung>
  )
}
