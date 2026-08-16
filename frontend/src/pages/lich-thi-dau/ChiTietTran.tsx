import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useNavigate, useParams } from 'react-router-dom'
import { ArrowLeft, Heart, Save, Users } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Input, Label, Table, Td, Th, TrangTrong,
} from '@/components/ui'
import { SelectTimKiemNhieu } from '@/components/ui/SelectTimKiem'
import { cn } from '@/lib/utils'

interface TranDauDto {
  id: string
  thoiGian: string
  tenDoiThu: string | null
  tySoNha: number | null
  tySoKhach: number | null
  ketQua: 'ChuaCo' | 'Thang' | 'Hoa' | 'Thua'
  linkVideo: string | null
  nhanXetChung: string | null
}
interface DoiHinhDto {
  id: string
  cauThuId: string
  hoTen: string
  viTri: string | null
  laDuBi: boolean
}
interface DanhGiaDto {
  cauThuId: string
  hoTen: string
  soBanGhiDuoc: number
  soBanCuuThua: number
  ghiChu: string | null
  soPhieuMvp: number
  toiDaVote: boolean
}
interface CauThuNgan {
  id: string
  hoTen: string
}

type Tab = 'doi-hinh' | 'so-do' | 'danh-gia'

/** FR-10 — chi tiết trận đấu, wizard 3 tab theo đặc tả. */
export default function ChiTietTran() {
  const { t } = useTranslation()
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [tab, setTab] = useState<Tab>('doi-hinh')
  const [maLoi, setMaLoi] = useState<string | null>(null)

  const { data: tran } = useQuery({
    queryKey: ['tran-dau', id],
    queryFn: async () => (await api.get<TranDauDto>(`/tran-dau/${id}`)).data,
  })

  const tabs: { khoa: Tab; nhan: string }[] = [
    { khoa: 'doi-hinh', nhan: t('chiTiet.tabDoiHinh') },
    { khoa: 'so-do', nhan: t('chiTiet.tabSoDo') },
    { khoa: 'danh-gia', nhan: t('chiTiet.tabDanhGia') },
  ]

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-3">
        <Button variant="ghost" size="sm" onClick={() => navigate('/lich-thi-dau')}>
          <ArrowLeft className="h-4 w-4" />
          {t('chung.quayLai')}
        </Button>
        {tran && (
          <div className="flex flex-wrap items-center gap-2 text-sm">
            <span className="font-semibold">
              {new Date(tran.thoiGian).toLocaleString('vi-VN', {
                day: '2-digit', month: '2-digit', year: 'numeric',
                hour: '2-digit', minute: '2-digit',
              })}
            </span>
            {tran.tenDoiThu && <span className="text-muted-foreground">· {tran.tenDoiThu}</span>}
            {tran.tySoNha !== null && (
              <span className="font-mono font-semibold">
                {tran.tySoNha} – {tran.tySoKhach}
              </span>
            )}
          </div>
        )}
      </div>

      <div className="flex gap-1 border-b border-border">
        {tabs.map((tb) => (
          <button
            key={tb.khoa}
            type="button"
            onClick={() => { setTab(tb.khoa); setMaLoi(null) }}
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

      {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      {tab === 'doi-hinh' && <TabDoiHinh tranDauId={id} onLoi={setMaLoi} qc={qc} />}
      {tab === 'so-do' && <TabSoDo tranDauId={id} onLoi={setMaLoi} />}
      {tab === 'danh-gia' && <TabDanhGia tranDauId={id} onLoi={setMaLoi} />}
    </div>
  )
}

/** Tab (a) — chọn thành viên tham gia. */
function TabDoiHinh({
  tranDauId, onLoi, qc,
}: {
  tranDauId: string
  onLoi: (m: string | null) => void
  qc: ReturnType<typeof useQueryClient>
}) {
  const { t } = useTranslation()
  const [chon, setChon] = useState<string[] | null>(null)

  const { data: doiHinh, isLoading } = useQuery({
    queryKey: ['doi-hinh', tranDauId],
    queryFn: async () => (await api.get<DoiHinhDto[]>(`/tran-dau/${tranDauId}/doi-hinh`)).data,
  })
  const { data: cauThus } = useQuery({
    queryKey: ['cau-thu'],
    queryFn: async () => (await api.get<CauThuNgan[]>('/cau-thu')).data,
  })

  // Khởi tạo từ dữ liệu server, sau đó để người dùng chỉnh tự do.
  const dangChon = chon ?? doiHinh?.map((d) => d.cauThuId) ?? []

  const luu = useMutation({
    mutationFn: async () =>
      api.put(`/tran-dau/${tranDauId}/doi-hinh`, {
        tranDauId,
        thanhVien: dangChon.map((cauThuId) => {
          const cu = doiHinh?.find((d) => d.cauThuId === cauThuId)
          // Giữ nguyên vị trí và cờ dự bị của người đã có (quy tắc #1).
          return { cauThuId, viTri: cu?.viTri ?? null, laDuBi: cu?.laDuBi ?? false }
        }),
      }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['doi-hinh', tranDauId] })
      void qc.invalidateQueries({ queryKey: ['danh-gia', tranDauId] })
      onLoi(null)
      setChon(null)
    },
    onError: (e) => onLoi(layMaLoi(e)),
  })

  if (isLoading) return <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>

  return (
    <div className="flex max-w-2xl flex-col gap-4">
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="thanhVien">{t('chiTiet.thanhVien')}</Label>
        <SelectTimKiemNhieu
          id="thanhVien"
          luaChon={(cauThus ?? []).map((c) => ({ giaTri: c.id, nhan: c.hoTen }))}
          giaTri={dangChon}
          onDoi={setChon}
          placeholder={t('chiTiet.chonThanhVien')}
          placeholderTimKiem={t('taiKhoan.timCauThu')}
        />
        <p className="text-xs text-muted-foreground">{t('chiTiet.doiHinhGoiY')}</p>
      </div>

      <div>
        <Button onClick={() => luu.mutate()} disabled={luu.isPending}>
          <Save className="h-4 w-4" />
          {luu.isPending ? t('chung.dangTai') : t('chung.luu')}
        </Button>
      </div>

      {doiHinh?.length ? (
        <Table>
          <thead>
            <tr>
              <Th>{t('cauThu.hoTen')}</Th>
              <Th>{t('chiTiet.viTri')}</Th>
              <Th>{t('chiTiet.vaiTro')}</Th>
            </tr>
          </thead>
          <tbody>
            {doiHinh.map((d) => (
              <tr key={d.id} className="hover:bg-muted/40">
                <Td className="font-medium">{d.hoTen}</Td>
                <Td className="text-muted-foreground">{d.viTri ?? '—'}</Td>
                <Td>
                  {d.laDuBi ? (
                    <Badge>{t('chiTiet.duBi')}</Badge>
                  ) : (
                    <Badge variant="win">{t('chiTiet.daChinh')}</Badge>
                  )}
                </Td>
              </tr>
            ))}
          </tbody>
        </Table>
      ) : (
        <TrangTrong thongDiep={t('chiTiet.chuaCoDoiHinh')} />
      )}
    </div>
  )
}

/** Tab (b) — sơ đồ chiến thuật. Bản kéo-thả sẽ làm ở đợt sau; hiện lưu JSON + ghi chú. */
function TabSoDo({ tranDauId, onLoi }: { tranDauId: string; onLoi: (m: string | null) => void }) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [daLuu, setDaLuu] = useState(false)

  const { data, isLoading } = useQuery({
    queryKey: ['so-do', tranDauId],
    queryFn: async () =>
      (await api.get<{ soDoJson: string; ghiChuChienThuat: string | null }>(
        `/tran-dau/${tranDauId}/so-do`,
      )).data,
  })

  const luu = useMutation({
    mutationFn: async (form: { soDoJson: string; ghiChuChienThuat: string | null }) =>
      api.put(`/tran-dau/${tranDauId}/so-do`, { tranDauId, ...form }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['so-do', tranDauId] })
      onLoi(null)
      setDaLuu(true)
      setTimeout(() => setDaLuu(false), 2500)
    },
    onError: (e) => onLoi(layMaLoi(e)),
  })

  if (isLoading) return <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>

  return (
    <form
      key={tranDauId}
      onSubmit={(e) => {
        e.preventDefault()
        const fd = new FormData(e.currentTarget)
        luu.mutate({
          soDoJson: String(fd.get('soDoJson')) || '{}',
          ghiChuChienThuat: (fd.get('ghiChuChienThuat') as string) || null,
        })
      }}
      className="flex max-w-2xl flex-col gap-4"
    >
      <div className="rounded-md border border-dashed border-border bg-muted/30 p-3 text-sm text-muted-foreground">
        {t('chiTiet.soDoTamThoi')}
      </div>

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="ghiChuChienThuat">{t('chiTiet.ghiChuChienThuat')}</Label>
        <Input
          id="ghiChuChienThuat"
          name="ghiChuChienThuat"
          defaultValue={data?.ghiChuChienThuat ?? ''}
          placeholder="VD: Phòng ngự phản công, ép biên phải"
        />
      </div>

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="soDoJson">{t('chiTiet.soDoJson')}</Label>
        <textarea
          id="soDoJson"
          name="soDoJson"
          rows={6}
          defaultValue={data?.soDoJson ?? '{}'}
          className="rounded-md border border-input bg-background px-3 py-2 font-mono text-xs focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        />
      </div>

      <div className="flex items-center gap-3">
        <Button type="submit" disabled={luu.isPending}>
          <Save className="h-4 w-4" />
          {luu.isPending ? t('chung.dangTai') : t('chung.luu')}
        </Button>
        {daLuu && <span className="text-sm text-status-win">{t('chiTiet.daLuu')}</span>}
      </div>
    </form>
  )
}

/** Tab (c) — đánh giá sau trận + vote MVP. */
function TabDanhGia({ tranDauId, onLoi }: { tranDauId: string; onLoi: (m: string | null) => void }) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [daLuu, setDaLuu] = useState(false)

  const { data, isLoading } = useQuery({
    queryKey: ['danh-gia', tranDauId],
    queryFn: async () => (await api.get<DanhGiaDto[]>(`/tran-dau/${tranDauId}/danh-gia`)).data,
  })

  const luu = useMutation({
    mutationFn: async (danhGias: unknown[]) =>
      api.put(`/tran-dau/${tranDauId}/danh-gia`, { tranDauId, danhGias }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['danh-gia', tranDauId] })
      onLoi(null)
      setDaLuu(true)
      setTimeout(() => setDaLuu(false), 2500)
    },
    onError: (e) => onLoi(layMaLoi(e)),
  })

  const vote = useMutation({
    mutationFn: async (cauThuId: string) =>
      api.post(`/tran-dau/${tranDauId}/vote-mvp/${cauThuId}`),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['danh-gia', tranDauId] })
      onLoi(null)
    },
    onError: (e) => onLoi(layMaLoi(e)),
  })

  if (isLoading) return <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
  if (!data?.length) return <TrangTrong thongDiep={t('chiTiet.chuaCoDoiHinhDeDanhGia')} />

  const onSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    const fd = new FormData(e.currentTarget)
    luu.mutate(
      data.map((d) => ({
        cauThuId: d.cauThuId,
        soBanGhiDuoc: Number(fd.get(`ghi_${d.cauThuId}`) ?? 0),
        soBanCuuThua: Number(fd.get(`cuu_${d.cauThuId}`) ?? 0),
        chiSoKyNang: null,
        ghiChu: (fd.get(`gc_${d.cauThuId}`) as string) || null,
      })),
    )
  }

  return (
    <form key={tranDauId} onSubmit={onSubmit} className="flex flex-col gap-4">
      <Table>
        <thead>
          <tr>
            <Th>{t('cauThu.hoTen')}</Th>
            <Th className="w-24">{t('chiTiet.banThang')}</Th>
            <Th className="w-24">{t('chiTiet.cuuThua')}</Th>
            <Th>{t('cauThu.ghiChu')}</Th>
            <Th className="w-28 text-center">{t('chiTiet.mvp')}</Th>
          </tr>
        </thead>
        <tbody>
          {data.map((d) => (
            <tr key={d.cauThuId} className="hover:bg-muted/40">
              <Td className="font-medium">{d.hoTen}</Td>
              <Td>
                <Input
                  name={`ghi_${d.cauThuId}`}
                  type="number"
                  min={0}
                  defaultValue={d.soBanGhiDuoc}
                  className="h-8"
                />
              </Td>
              <Td>
                <Input
                  name={`cuu_${d.cauThuId}`}
                  type="number"
                  min={0}
                  defaultValue={d.soBanCuuThua}
                  className="h-8"
                />
              </Td>
              <Td>
                <Input name={`gc_${d.cauThuId}`} defaultValue={d.ghiChu ?? ''} className="h-8" />
              </Td>
              <Td>
                {/*
                  Nút tim là thao tác RIÊNG, không nằm trong form lưu đánh giá: mỗi người chỉ
                  một phiếu (quy tắc #8) nên nó phải gửi ngay, không chờ bấm Lưu.
                */}
                <div className="flex items-center justify-center gap-1.5">
                  <button
                    type="button"
                    onClick={() => vote.mutate(d.cauThuId)}
                    disabled={vote.isPending}
                    aria-label={t('chiTiet.voteMvp')}
                    title={t('chiTiet.voteMvp')}
                    className="rounded p-1 hover:bg-muted disabled:opacity-50"
                  >
                    <Heart
                      className={cn(
                        'h-4 w-4',
                        d.toiDaVote
                          ? 'fill-status-lose text-status-lose'
                          : 'text-muted-foreground',
                      )}
                    />
                  </button>
                  <span className="min-w-4 text-sm font-medium">{d.soPhieuMvp}</span>
                </div>
              </Td>
            </tr>
          ))}
        </tbody>
      </Table>

      <div className="flex items-center gap-3">
        <Button type="submit" disabled={luu.isPending}>
          <Save className="h-4 w-4" />
          {luu.isPending ? t('chung.dangTai') : t('chung.luu')}
        </Button>
        {daLuu && <span className="text-sm text-status-win">{t('chiTiet.daLuu')}</span>}
        <span className="ml-auto flex items-center gap-1.5 text-xs text-muted-foreground">
          <Users className="h-3.5 w-3.5" />
          {t('chiTiet.mvpGoiY')}
        </span>
      </div>
    </form>
  )
}
