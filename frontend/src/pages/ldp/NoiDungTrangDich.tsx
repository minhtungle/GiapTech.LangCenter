import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ChevronDown, ChevronUp, Eye, EyeOff, Plus, Trash2 } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import { useQuyen } from '@/lib/quyen'
import {
  Button, CanhBaoLoi, Card, CardContent, CardHeader, CardTitle, Input, Label, Textarea,
} from '@/components/ui'
import { HopXacNhan } from '@/components/ui/HopXacNhan'

/** Khớp `LoaiKhoiLdp` ở backend. Enum serialize thành CHUỖI, không phải số. */
type LoaiKhoi =
  | 'Hero' | 'GioiThieu' | 'KhoaHoc' | 'GiaoVien' | 'CamNhan' | 'TinTuc' | 'LienHe'
  | 'DoiTac' | 'QuyTrinh' | 'CoSo'

/**
 * Bảy loại có mục con; ba loại còn lại (Hero, GioiThieu, LienHe) chỉ có nội dung của chính
 * khối. Danh sách này phải khớp phép kiểm ở `ThemMucHandler` — lệch thì giao diện hiện nút
 * "Thêm mục" mà API từ chối.
 */
const CO_MUC_CON: LoaiKhoi[] = [
  'KhoaHoc', 'GiaoVien', 'CamNhan', 'TinTuc', 'DoiTac', 'QuyTrinh', 'CoSo',
]

/** Gợi ý nhập cho từng loại khối — mỗi loại dùng các ô theo cách khác nhau. */
const GOI_Y_MUC: Partial<Record<LoaiKhoi, string>> = {
  DoiTac: 'ldp.goiYDoiTac',
  QuyTrinh: 'ldp.goiYQuyTrinh',
  CoSo: 'ldp.goiYCoSo',
  GiaoVien: 'ldp.goiYGiaoVien',
}

interface Muc {
  id: string
  thuTu: number
  tieuDe: string
  phuDe: string | null
  moTa: string | null
  khoaAnh: string | null
  giaNiemYet: number | null
  duongDan: string | null
}

interface Khoi {
  id: string
  loai: LoaiKhoi
  hien: boolean
  thuTu: number
  tieuDe: string | null
  moTa: string | null
  khoaAnh: string | null
  nhanNut: string | null
  duongDanNut: string | null
  mucs: Muc[]
}

interface Trang {
  id: string
  daXuatBan: boolean
  tieuDeSeo: string | null
  moTaSeo: string | null
  khois: Khoi[]
}

/**
 * FR-30 — soạn nội dung trang đích.
 *
 * Bố cục cố định: người dùng sửa nội dung, bật/tắt và sắp thứ tự, **không** thêm được loại
 * khối mới. Xem FR-30 để biết vì sao không làm page builder tự do.
 */
export default function NoiDungTrangDich() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { coQuyen } = useQuyen()
  const [maLoi, setMaLoi] = useState<string | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['ldp', 'trang-dich'],
    queryFn: async () => (await api.get<Trang>('/ldp/trang-dich')).data,
  })

  const duocSua = coQuyen('TrangDich', 'Sua')
  const duocXuatBan = coQuyen('TrangDich', 'XuatBan')

  const lamMoi = () => qc.invalidateQueries({ queryKey: ['ldp', 'trang-dich'] })

  const xuatBan = useMutation({
    mutationFn: async (bat: boolean) =>
      api.put('/ldp/xuat-ban', {
        xuatBan: bat,
        // Gửi đủ hai trường SEO kể cả khi không đổi: lệnh ghi đè chúng, nên thiếu là xoá
        // mất nội dung người dùng đã nhập (quy tắc #1).
        tieuDeSeo: data?.tieuDeSeo ?? null,
        moTaSeo: data?.moTaSeo ?? null,
      }),
    onSuccess: lamMoi,
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  if (isLoading) return <p className="p-4 text-sm text-muted-foreground">{t('chung.dangTai')}</p>
  if (!data) return null

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h1 className="text-xl font-semibold">{t('menu.noiDungTrangDich')}</h1>
          <p className="text-sm text-muted-foreground">
            {data.daXuatBan ? t('ldp.dangHienCongKhai') : t('ldp.chuaXuatBan')}
          </p>
        </div>

        {duocXuatBan && (
          <Button
            variant={data.daXuatBan ? 'outline' : 'primary'}
            disabled={xuatBan.isPending}
            onClick={() => xuatBan.mutate(!data.daXuatBan)}
          >
            {data.daXuatBan ? t('ldp.goXuatBan') : t('ldp.xuatBan')}
          </Button>
        )}
      </div>

      {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      <SeoCard trang={data} duocSua={duocXuatBan} onLuu={lamMoi} onLoi={setMaLoi} />

      {data.khois.map((k, i) => (
        <KhoiCard
          key={k.id}
          khoi={k}
          duocSua={duocSua}
          dauTien={i === 0}
          cuoiCung={i === data.khois.length - 1}
          onLuu={lamMoi}
          onLoi={setMaLoi}
        />
      ))}
    </div>
  )
}

function SeoCard({
  trang, duocSua, onLuu, onLoi,
}: {
  trang: Trang
  duocSua: boolean
  onLuu: () => void
  onLoi: (m: string) => void
}) {
  const { t } = useTranslation()
  const [tieuDe, setTieuDe] = useState(trang.tieuDeSeo ?? '')
  const [moTa, setMoTa] = useState(trang.moTaSeo ?? '')

  const luu = useMutation({
    mutationFn: async () =>
      api.put('/ldp/xuat-ban', {
        // Giữ nguyên trạng thái xuất bản — màn này chỉ sửa SEO.
        xuatBan: trang.daXuatBan,
        tieuDeSeo: tieuDe.trim() || null,
        moTaSeo: moTa.trim() || null,
      }),
    onSuccess: onLuu,
    onError: (e) => onLoi(layMaLoi(e)),
  })

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('ldp.seo')}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="seo-tieu-de">{t('ldp.tieuDeSeo')}</Label>
          <Input
            id="seo-tieu-de"
            value={tieuDe}
            disabled={!duocSua}
            onChange={(e) => setTieuDe(e.target.value)}
            placeholder={t('ldp.tieuDeSeoGoiY')}
          />
        </div>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="seo-mo-ta">{t('ldp.moTaSeo')}</Label>
          <Textarea
            id="seo-mo-ta"
            rows={2}
            value={moTa}
            disabled={!duocSua}
            onChange={(e) => setMoTa(e.target.value)}
          />
        </div>
        {duocSua && (
          <div className="flex justify-end">
            <Button size="sm" disabled={luu.isPending} onClick={() => luu.mutate()}>
              {t('chung.luu')}
            </Button>
          </div>
        )}
      </CardContent>
    </Card>
  )
}

function KhoiCard({
  khoi, duocSua, dauTien, cuoiCung, onLuu, onLoi,
}: {
  khoi: Khoi
  duocSua: boolean
  dauTien: boolean
  cuoiCung: boolean
  onLuu: () => void
  onLoi: (m: string) => void
}) {
  const { t } = useTranslation()
  const [tieuDe, setTieuDe] = useState(khoi.tieuDe ?? '')
  const [moTa, setMoTa] = useState(khoi.moTa ?? '')
  const [nhanNut, setNhanNut] = useState(khoi.nhanNut ?? '')
  const [duongDanNut, setDuongDanNut] = useState(khoi.duongDanNut ?? '')

  /**
   * Mọi lệnh sửa khối gửi ĐỦ TÁM trường (quy tắc #1).
   *
   * Backend ghi đè tất cả, nên gửi thiếu một trường là xoá nội dung người dùng đã nhập ở
   * trường đó — đúng lỗi 16/08/2026 (form sửa tài khoản thiếu ô địa chỉ).
   */
  const guiSua = (doiGi: Partial<Pick<Khoi, 'hien' | 'thuTu'>> = {}) =>
    api.put(`/ldp/khoi/${khoi.id}`, {
      hien: doiGi.hien ?? khoi.hien,
      thuTu: doiGi.thuTu ?? khoi.thuTu,
      tieuDe: tieuDe.trim() || null,
      moTa: moTa.trim() || null,
      khoaAnh: khoi.khoaAnh,
      nhanNut: nhanNut.trim() || null,
      duongDanNut: duongDanNut.trim() || null,
    })

  const luu = useMutation({
    mutationFn: () => guiSua(),
    onSuccess: onLuu,
    onError: (e) => onLoi(layMaLoi(e)),
  })

  const doiTrangThai = useMutation({
    mutationFn: () => guiSua({ hien: !khoi.hien }),
    onSuccess: onLuu,
    onError: (e) => onLoi(layMaLoi(e)),
  })

  const doiThuTu = useMutation({
    mutationFn: (buoc: number) => guiSua({ thuTu: khoi.thuTu + buoc }),
    onSuccess: onLuu,
    onError: (e) => onLoi(layMaLoi(e)),
  })

  return (
    <Card className={khoi.hien ? undefined : 'opacity-60'}>
      <CardHeader className="flex flex-row items-center justify-between gap-2 space-y-0">
        <CardTitle className="flex items-center gap-2">
          {t(`ldp.khoi.${khoi.loai}`)}
          {!khoi.hien && (
            <span className="rounded bg-muted px-1.5 py-0.5 text-xs font-normal text-muted-foreground">
              {t('ldp.dangTat')}
            </span>
          )}
        </CardTitle>

        {duocSua && (
          <div className="flex gap-1">
            <Button
              variant="ghost" size="sm"
              title={t('ldp.leenTren')}
              disabled={dauTien || doiThuTu.isPending}
              onClick={() => doiThuTu.mutate(-1)}
            >
              <ChevronUp className="h-4 w-4" />
            </Button>
            <Button
              variant="ghost" size="sm"
              title={t('ldp.xuongDuoi')}
              disabled={cuoiCung || doiThuTu.isPending}
              onClick={() => doiThuTu.mutate(1)}
            >
              <ChevronDown className="h-4 w-4" />
            </Button>
            <Button
              variant="ghost" size="sm"
              title={khoi.hien ? t('ldp.tatKhoi') : t('ldp.batKhoi')}
              disabled={doiTrangThai.isPending}
              onClick={() => doiTrangThai.mutate()}
            >
              {khoi.hien ? <Eye className="h-4 w-4" /> : <EyeOff className="h-4 w-4" />}
            </Button>
          </div>
        )}
      </CardHeader>

      <CardContent className="flex flex-col gap-3">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor={`td-${khoi.id}`}>{t('ldp.tieuDeKhoi')}</Label>
          <Input
            id={`td-${khoi.id}`}
            value={tieuDe}
            disabled={!duocSua}
            onChange={(e) => setTieuDe(e.target.value)}
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor={`mt-${khoi.id}`}>{t('ldp.moTaKhoi')}</Label>
          <Textarea
            id={`mt-${khoi.id}`}
            rows={3}
            value={moTa}
            disabled={!duocSua}
            onChange={(e) => setMoTa(e.target.value)}
          />
        </div>

        {khoi.loai === 'Hero' && (
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor={`nn-${khoi.id}`}>{t('ldp.nhanNut')}</Label>
              <Input
                id={`nn-${khoi.id}`}
                value={nhanNut}
                disabled={!duocSua}
                onChange={(e) => setNhanNut(e.target.value)}
                placeholder={t('ldp.nhanNutGoiY')}
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <Label htmlFor={`dn-${khoi.id}`}>{t('ldp.duongDanNut')}</Label>
              <Input
                id={`dn-${khoi.id}`}
                value={duongDanNut}
                disabled={!duocSua}
                onChange={(e) => setDuongDanNut(e.target.value)}
                placeholder="#lien-he"
              />
            </div>
          </div>
        )}

        {duocSua && (
          <div className="flex justify-end">
            <Button size="sm" disabled={luu.isPending} onClick={() => luu.mutate()}>
              {t('chung.luu')}
            </Button>
          </div>
        )}

        {CO_MUC_CON.includes(khoi.loai) && (
          <DanhSachMuc khoi={khoi} duocSua={duocSua} onLuu={onLuu} onLoi={onLoi} />
        )}
      </CardContent>
    </Card>
  )
}

function DanhSachMuc({
  khoi, duocSua, onLuu, onLoi,
}: {
  khoi: Khoi
  duocSua: boolean
  onLuu: () => void
  onLoi: (m: string) => void
}) {
  const { t } = useTranslation()
  const [tenMoi, setTenMoi] = useState('')
  const [xoaId, setXoaId] = useState<string | null>(null)

  const them = useMutation({
    mutationFn: async () => api.post(`/ldp/khoi/${khoi.id}/muc`, { tieuDe: tenMoi.trim() }),
    onSuccess: () => { setTenMoi(''); onLuu() },
    onError: (e) => onLoi(layMaLoi(e)),
  })

  const xoa = useMutation({
    mutationFn: async (id: string) => api.delete(`/ldp/muc/${id}`),
    onSuccess: () => { setXoaId(null); onLuu() },
    onError: (e) => { setXoaId(null); onLoi(layMaLoi(e)) },
  })

  return (
    <div className="mt-2 border-t pt-3">
      <p className="mb-1 text-sm font-medium">
        {t('ldp.danhSachMuc')} ({khoi.mucs.length})
      </p>
      {GOI_Y_MUC[khoi.loai] && (
        <p className="mb-2 text-xs text-muted-foreground">{t(GOI_Y_MUC[khoi.loai]!)}</p>
      )}

      <div className="flex flex-col gap-2">
        {khoi.mucs.map((m) => (
          <MucHang
            key={m.id}
            muc={m}
            duocSua={duocSua}
            onLuu={onLuu}
            onLoi={onLoi}
            onXoa={() => setXoaId(m.id)}
          />
        ))}
        {khoi.mucs.length === 0 && (
          <p className="text-sm text-muted-foreground">{t('ldp.chuaCoMuc')}</p>
        )}
      </div>

      {duocSua && (
        <div className="mt-3 flex gap-2">
          <Input
            value={tenMoi}
            onChange={(e) => setTenMoi(e.target.value)}
            placeholder={t('ldp.tenMucMoi')}
          />
          <Button
            size="sm"
            disabled={!tenMoi.trim() || them.isPending}
            onClick={() => them.mutate()}
          >
            <Plus className="h-4 w-4" />
            {t('chung.them')}
          </Button>
        </div>
      )}

      <HopXacNhan
        mo={xoaId !== null}
        tieuDe={t('ldp.xacNhanXoaMuc')}
        thongDiep={t('ldp.xoaMucMatGi', {
          ten: khoi.mucs.find((m) => m.id === xoaId)?.tieuDe ?? '',
        })}
        nguyHiem
        onHuy={() => setXoaId(null)}
        onDongY={() => xoaId && xoa.mutate(xoaId)}
      />
    </div>
  )
}

function MucHang({
  muc, duocSua, onLuu, onLoi, onXoa,
}: {
  muc: Muc
  duocSua: boolean
  onLuu: () => void
  onLoi: (m: string) => void
  onXoa: () => void
}) {
  const { t } = useTranslation()
  const [mo, setMo] = useState(false)
  const [tieuDe, setTieuDe] = useState(muc.tieuDe)
  const [phuDe, setPhuDe] = useState(muc.phuDe ?? '')
  const [moTa, setMoTa] = useState(muc.moTa ?? '')
  const [gia, setGia] = useState(muc.giaNiemYet?.toString() ?? '')

  const luu = useMutation({
    // Gửi đủ mọi trường — xem chú thích ở `KhoiCard.guiSua`.
    mutationFn: async () =>
      api.put(`/ldp/muc/${muc.id}`, {
        thuTu: muc.thuTu,
        tieuDe: tieuDe.trim(),
        phuDe: phuDe.trim() || null,
        moTa: moTa.trim() || null,
        khoaAnh: muc.khoaAnh,
        giaNiemYet: gia.trim() ? Number(gia) : null,
        duongDan: muc.duongDan,
      }),
    onSuccess: () => { setMo(false); onLuu() },
    onError: (e) => onLoi(layMaLoi(e)),
  })

  return (
    <div className="rounded-md border px-3 py-2">
      <div className="flex items-center justify-between gap-2">
        <button
          type="button"
          className="flex-1 truncate text-left text-sm hover:underline"
          onClick={() => setMo((x) => !x)}
        >
          {muc.tieuDe}
        </button>
        {duocSua && (
          <Button variant="ghost" size="sm" title={t('chung.xoa')} onClick={onXoa}>
            <Trash2 className="h-4 w-4 text-destructive" />
          </Button>
        )}
      </div>

      {mo && (
        <div className="mt-2 flex flex-col gap-2">
          <Input
            value={tieuDe}
            disabled={!duocSua}
            onChange={(e) => setTieuDe(e.target.value)}
            placeholder={t('ldp.tieuDeMuc')}
          />
          <Input
            value={phuDe}
            disabled={!duocSua}
            onChange={(e) => setPhuDe(e.target.value)}
            placeholder={t('ldp.phuDeMuc')}
          />
          <Textarea
            rows={2}
            value={moTa}
            disabled={!duocSua}
            onChange={(e) => setMoTa(e.target.value)}
            placeholder={t('ldp.moTaMuc')}
          />
          <Input
            type="number"
            value={gia}
            disabled={!duocSua}
            onChange={(e) => setGia(e.target.value)}
            placeholder={t('ldp.giaNiemYet')}
          />
          {duocSua && (
            <div className="flex justify-end">
              <Button size="sm" disabled={luu.isPending} onClick={() => luu.mutate()}>
                {t('chung.luu')}
              </Button>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
