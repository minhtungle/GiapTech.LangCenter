import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { CalendarDays, CheckCircle2, ClipboardCheck, X } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Input, Label, Table, Td, Th, TrangTrong,
} from '@/components/ui'
import { Modal } from '@/components/ui/Modal'
import { SelectTimKiem } from '@/components/ui/SelectTimKiem'

type TrangThaiDiemDanh = 'CoMat' | 'Vang' | 'DiMuon' | 'VangCoPhep'
const CAC_TRANG_THAI: TrangThaiDiemDanh[] = ['CoMat', 'Vang', 'DiMuon', 'VangCoPhep']

const THU: { ma: number; khoa: string }[] = [
  { ma: 1, khoa: 'Monday' }, { ma: 2, khoa: 'Tuesday' }, { ma: 3, khoa: 'Wednesday' },
  { ma: 4, khoa: 'Thursday' }, { ma: 5, khoa: 'Friday' }, { ma: 6, khoa: 'Saturday' },
  { ma: 0, khoa: 'Sunday' },
]

interface BuoiHocDto {
  id: string
  thuTu: number
  batDau: string
  ketThuc: string
  tenGiaoVien: string
  giaoVienRieng: boolean
  trangThai: 'DaLenLich' | 'DaHoanThanh' | 'DaHuy'
  laHocBu: boolean
  soDaDiemDanh: number
  soHocVien: number
}

interface DiemDanhDto {
  hocVienId: string
  hoTen: string
  trangThaiTuKhai: TrangThaiDiemDanh | null
  trangThaiChinhThuc: TrangThaiDiemDanh
  nguonGhiNhan: string
  lyDoVang: string | null
  giaoVienSuaKhacTuKhai: boolean
  daGhiNhan: boolean
}

const gioVN = (s: string) =>
  new Date(s).toLocaleString('vi-VN', {
    weekday: 'short', day: '2-digit', month: '2-digit',
    hour: '2-digit', minute: '2-digit',
  })

/** Tab Lịch học + Điểm danh của một lớp (bước 2 wizard, và dùng lại khi vận hành lớp). */
export function LichVaDiemDanh({
  lopHocId,
  tenLop,
  onDong,
}: {
  lopHocId: string
  tenLop: string
  onDong: () => void
}) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [moSinhLich, setMoSinhLich] = useState(false)
  const [buoiDiemDanh, setBuoiDiemDanh] = useState<BuoiHocDto | null>(null)

  const { data: buoiHocs = [], isLoading } = useQuery({
    queryKey: ['lop-hoc', lopHocId, 'buoi-hoc'],
    queryFn: async () => (await api.get<BuoiHocDto[]>(`/lop-hoc/${lopHocId}/buoi-hoc`)).data,
  })

  const lamMoi = () => {
    void qc.invalidateQueries({ queryKey: ['lop-hoc', lopHocId, 'buoi-hoc'] })
    void qc.invalidateQueries({ queryKey: ['lop-hoc'] })
  }

  const huyBuoi = useMutation({
    mutationFn: (id: string) => api.post(`/buoi-hoc/${id}/huy`),
    onSuccess: lamMoi,
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  return (
    <Modal mo onDong={onDong} tieuDe={`${t('buoiHoc.lich')} — ${tenLop}`}>
      <div className="grid gap-4">
        <div className="flex items-center justify-between">
          <p className="text-sm text-muted-foreground">
            {buoiHocs.length > 0 && t('buoiHoc.daSinh', { soLuong: buoiHocs.length })}
          </p>
          <Button size="sm" variant="outline" onClick={() => setMoSinhLich(true)}>
            <CalendarDays className="mr-1.5 h-4 w-4" />
            {buoiHocs.length > 0 ? t('buoiHoc.sinhLaiLich') : t('buoiHoc.sinhLich')}
          </Button>
        </div>

        {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

        {isLoading ? (
          <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
        ) : buoiHocs.length === 0 ? (
          <TrangTrong thongDiep={t('buoiHoc.chuaCoLich')} />
        ) : (
          <div className="max-h-[26rem] overflow-y-auto">
            <Table>
              <thead>
                <tr>
                  <Th className="w-16">{t('buoiHoc.thuTu')}</Th>
                  <Th>{t('buoiHoc.thoiGian')}</Th>
                  <Th>{t('buoiHoc.giaoVien')}</Th>
                  <Th>{t('buoiHoc.diemDanh')}</Th>
                  <Th>{t('buoiHoc.trangThai')}</Th>
                  <Th className="w-24" />
                </tr>
              </thead>
              <tbody>
                {buoiHocs.map((b) => (
                  <tr key={b.id} className="hover:bg-muted/40">
                    <Td className="font-medium">{b.thuTu}</Td>
                    <Td className="text-muted-foreground">
                      {gioVN(b.batDau)}
                      {b.laHocBu && (
                        <Badge variant="accent" className="ml-2">
                          {t('buoiHoc.hocBu')}
                        </Badge>
                      )}
                    </Td>
                    <Td className="text-muted-foreground">
                      {b.tenGiaoVien}
                      {b.giaoVienRieng && (
                        <Badge variant="draw" className="ml-2">
                          {t('buoiHoc.giaoVienRieng')}
                        </Badge>
                      )}
                    </Td>
                    <Td className="text-muted-foreground">
                      {b.soDaDiemDanh}/{b.soHocVien}
                    </Td>
                    <Td>
                      <Badge
                        variant={
                          b.trangThai === 'DaHoanThanh'
                            ? 'win'
                            : b.trangThai === 'DaHuy'
                              ? 'lose'
                              : 'draw'
                        }
                      >
                        {t(`trangThaiBuoiHoc.${b.trangThai}`)}
                      </Badge>
                    </Td>
                    <Td>
                      <div className="flex justify-end gap-1">
                        <Button
                          variant="ghost"
                          size="sm"
                          title={t('buoiHoc.diemDanh')}
                          onClick={() => setBuoiDiemDanh(b)}
                        >
                          <ClipboardCheck className="h-4 w-4" />
                        </Button>
                        {b.trangThai !== 'DaHuy' && (
                          <Button
                            variant="ghost"
                            size="sm"
                            title={t('buoiHoc.huyBuoi')}
                            onClick={() => huyBuoi.mutate(b.id)}
                          >
                            <X className="h-4 w-4" />
                          </Button>
                        )}
                      </div>
                    </Td>
                  </tr>
                ))}
              </tbody>
            </Table>
          </div>
        )}
      </div>

      {moSinhLich && (
        <FormSinhLich
          lopHocId={lopHocId}
          daCoLich={buoiHocs.length > 0}
          onXong={() => {
            setMoSinhLich(false)
            lamMoi()
          }}
          onDong={() => setMoSinhLich(false)}
        />
      )}

      {buoiDiemDanh && (
        <BangDiemDanh
          buoi={buoiDiemDanh}
          onDong={() => setBuoiDiemDanh(null)}
          onXong={lamMoi}
        />
      )}
    </Modal>
  )
}

/** Bước 2 wizard: khai tần suất, hệ thống sinh danh sách buổi. */
function FormSinhLich({
  lopHocId,
  daCoLich,
  onXong,
  onDong,
}: {
  lopHocId: string
  daCoLich: boolean
  onXong: () => void
  onDong: () => void
}) {
  const { t } = useTranslation()
  const [thuChon, setThuChon] = useState<number[]>([2, 4, 6])
  const [ketThucTheo, setKetThucTheo] = useState<'soBuoi' | 'ngay'>('soBuoi')
  const [maLoi, setMaLoi] = useState<string | null>(null)

  const sinh = useMutation({
    mutationFn: (body: Record<string, unknown>) =>
      api.post(`/lop-hoc/${lopHocId}/sinh-lich`, body),
    onSuccess: onXong,
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const batThu = (ma: number) =>
    setThuChon((cu) => (cu.includes(ma) ? cu.filter((x) => x !== ma) : [...cu, ma]))

  const onSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    const fd = new FormData(e.currentTarget)

    if (thuChon.length === 0) {
      setMaLoi('TAN_SUAT_TRONG')
      return
    }

    const loaiTruTho = ((fd.get('ngayLoaiTru') as string) || '')
      .split('\n')
      .map((d) => d.trim())
      .filter(Boolean)

    sinh.mutate({
      ngayKhaiGiang: String(fd.get('ngayKhaiGiang')),
      // Backend dùng DayOfWeek: CN=0, T2=1… trùng với mã đang lưu.
      thuTrongTuan: thuChon,
      gioBatDau: String(fd.get('gioBatDau')) + ':00',
      gioKetThuc: String(fd.get('gioKetThuc')) + ':00',
      soBuoi: ketThucTheo === 'soBuoi' ? Number(fd.get('soBuoi')) : null,
      denNgay: ketThucTheo === 'ngay' ? String(fd.get('denNgay')) : null,
      ngayLoaiTru: loaiTruTho,
    })
  }

  return (
    <Modal mo onDong={onDong} tieuDe={t('buoiHoc.sinhLich')}>
      <form onSubmit={onSubmit} className="grid gap-4 sm:grid-cols-2">
        {daCoLich && (
          <div className="sm:col-span-2">
            <CanhBaoLoi>{t('loi.LICH_DA_CO_DIEM_DANH')}</CanhBaoLoi>
          </div>
        )}

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="ngayKhaiGiang">{t('lopHoc.ngayKhaiGiang')}</Label>
          <Input id="ngayKhaiGiang" name="ngayKhaiGiang" type="date" required />
        </div>

        <div className="flex flex-col gap-1.5">
          <Label>{t('buoiHoc.thuTrongTuan')}</Label>
          <div className="flex flex-wrap gap-1">
            {THU.map((th) => (
              <button
                key={th.ma}
                type="button"
                aria-pressed={thuChon.includes(th.ma)}
                onClick={() => batThu(th.ma)}
                className={
                  'h-9 w-11 rounded-md border text-xs transition-colors ' +
                  (thuChon.includes(th.ma)
                    ? 'border-[hsl(var(--primary))] bg-[hsl(var(--primary))]/10 font-medium'
                    : 'border-border hover:bg-muted')
                }
              >
                {t(`thu.${th.khoa}`)}
              </button>
            ))}
          </div>
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="gioBatDau">{t('buoiHoc.gioBatDau')}</Label>
          <Input id="gioBatDau" name="gioBatDau" type="time" defaultValue="18:00" required />
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="gioKetThuc">{t('buoiHoc.gioKetThuc')}</Label>
          <Input id="gioKetThuc" name="gioKetThuc" type="time" defaultValue="20:00" required />
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="ketThucTheo">{t('buoiHoc.ketThucTheo')}</Label>
          <SelectTimKiem
            id="ketThucTheo"
            choPhepXoa={false}
            luaChon={[
              { giaTri: 'soBuoi', nhan: t('buoiHoc.theoSoBuoi') },
              { giaTri: 'ngay', nhan: t('buoiHoc.theoNgay') },
            ]}
            giaTri={ketThucTheo}
            onDoi={(v) => setKetThucTheo((v as 'soBuoi' | 'ngay') ?? 'soBuoi')}
          />
        </div>

        {ketThucTheo === 'soBuoi' ? (
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="soBuoi">{t('buoiHoc.theoSoBuoi')}</Label>
            <Input id="soBuoi" name="soBuoi" type="number" min={1} max={500} defaultValue={24} />
          </div>
        ) : (
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="denNgay">{t('buoiHoc.theoNgay')}</Label>
            <Input id="denNgay" name="denNgay" type="date" />
          </div>
        )}

        <div className="flex flex-col gap-1.5 sm:col-span-2">
          <Label htmlFor="ngayLoaiTru">{t('buoiHoc.ngayLoaiTru')}</Label>
          <textarea
            id="ngayLoaiTru"
            name="ngayLoaiTru"
            rows={2}
            placeholder={t('buoiHoc.ngayLoaiTruGoiY')}
            className="rounded-md border border-input bg-background px-3 py-2 text-sm"
          />
        </div>

        {maLoi && (
          <div className="sm:col-span-2">
            <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>
          </div>
        )}

        <div className="flex justify-end gap-2 sm:col-span-2">
          <Button type="button" variant="outline" onClick={onDong}>
            {t('chung.huy')}
          </Button>
          <Button type="submit" disabled={sinh.isPending}>
            {t('buoiHoc.sinhLich')}
          </Button>
        </div>
      </form>
    </Modal>
  )
}

/** Bảng điểm danh một buổi — hai nguồn: học viên tự khai và giáo viên xác nhận. */
function BangDiemDanh({
  buoi,
  onDong,
  onXong,
}: {
  buoi: BuoiHocDto
  onDong: () => void
  onXong: () => void
}) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [sua, setSua] = useState<Record<string, { tt: TrangThaiDiemDanh; lyDo: string }>>({})
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [daLuu, setDaLuu] = useState(false)

  const { data: ds = [] } = useQuery({
    queryKey: ['buoi-hoc', buoi.id, 'diem-danh'],
    queryFn: async () => (await api.get<DiemDanhDto[]>(`/buoi-hoc/${buoi.id}/diem-danh`)).data,
  })

  const hienTai = useMemo(() => {
    const m: Record<string, { tt: TrangThaiDiemDanh; lyDo: string }> = {}
    for (const d of ds) {
      m[d.hocVienId] = sua[d.hocVienId] ?? {
        tt: d.trangThaiChinhThuc,
        lyDo: d.lyDoVang ?? '',
      }
    }
    return m
  }, [ds, sua])

  const lamMoi = () => {
    void qc.invalidateQueries({ queryKey: ['buoi-hoc', buoi.id, 'diem-danh'] })
    onXong()
  }

  const luu = useMutation({
    mutationFn: () =>
      api.post(`/buoi-hoc/${buoi.id}/diem-danh`, {
        danhSach: Object.entries(hienTai).map(([hocVienId, v]) => ({
          hocVienId,
          trangThai: v.tt,
          lyDoVang: v.lyDo || null,
        })),
      }),
    onSuccess: () => {
      setSua({})
      setMaLoi(null)
      setDaLuu(true)
      setTimeout(() => setDaLuu(false), 2500)
      lamMoi()
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const chot = useMutation({
    mutationFn: () => api.post(`/buoi-hoc/${buoi.id}/chot`),
    onSuccess: lamMoi,
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const doi = (id: string, phan: Partial<{ tt: TrangThaiDiemDanh; lyDo: string }>) =>
    setSua((cu) => ({
      ...cu,
      [id]: { ...(hienTai[id] ?? { tt: 'Vang' as const, lyDo: '' }), ...phan },
    }))

  return (
    <Modal
      mo
      onDong={onDong}
      tieuDe={`${t('diemDanh.tieuDe')} — ${t('buoiHoc.thuTu')} ${buoi.thuTu} · ${gioVN(buoi.batDau)}`}
    >
      <div className="grid gap-4">
        {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

        {ds.length === 0 ? (
          <TrangTrong thongDiep={t('chung.khongCoDuLieu')} />
        ) : (
          <div className="max-h-[24rem] overflow-y-auto">
            <Table>
              <thead>
                <tr>
                  <Th>{t('diemDanh.hocVien')}</Th>
                  <Th>{t('diemDanh.tuKhai')}</Th>
                  <Th className="w-44">{t('diemDanh.chinhThuc')}</Th>
                  <Th>{t('diemDanh.lyDoVang')}</Th>
                </tr>
              </thead>
              <tbody>
                {ds.map((d) => {
                  const v = hienTai[d.hocVienId]
                  const canLyDo = v?.tt === 'Vang' || v?.tt === 'VangCoPhep'
                  return (
                    <tr key={d.hocVienId} className="hover:bg-muted/40">
                      <Td className="font-medium">{d.hoTen}</Td>
                      <Td className="text-muted-foreground">
                        {d.trangThaiTuKhai ? (
                          <>
                            {t(`trangThaiDiemDanh.${d.trangThaiTuKhai}`)}
                            {d.giaoVienSuaKhacTuKhai && (
                              <Badge variant="lose" className="ml-2">
                                {t('diemDanh.khacTuKhai')}
                              </Badge>
                            )}
                          </>
                        ) : (
                          '—'
                        )}
                      </Td>
                      <Td>
                        <select
                          value={v?.tt ?? 'Vang'}
                          onChange={(e) =>
                            doi(d.hocVienId, { tt: e.target.value as TrangThaiDiemDanh })
                          }
                          className="h-8 w-full rounded-md border border-input bg-background px-2 text-sm"
                        >
                          {CAC_TRANG_THAI.map((tt) => (
                            <option key={tt} value={tt}>
                              {t(`trangThaiDiemDanh.${tt}`)}
                            </option>
                          ))}
                        </select>
                      </Td>
                      <Td>
                        <Input
                          value={v?.lyDo ?? ''}
                          onChange={(e) => doi(d.hocVienId, { lyDo: e.target.value })}
                          disabled={!canLyDo}
                          placeholder={canLyDo ? t('diemDanh.lyDoVang') : ''}
                          className="h-8"
                        />
                      </Td>
                    </tr>
                  )
                })}
              </tbody>
            </Table>
          </div>
        )}

        <p className="text-xs text-muted-foreground">{t('buoiHoc.chotBuoiGoiY')}</p>

        <div className="flex items-center justify-end gap-2">
          {daLuu && (
            <span className="mr-auto flex items-center gap-1 text-sm text-status-win">
              <CheckCircle2 className="h-4 w-4" />
              {t('diemDanh.daLuu')}
            </span>
          )}
          <Button variant="outline" disabled={chot.isPending} onClick={() => chot.mutate()}>
            {t('buoiHoc.chotBuoi')}
          </Button>
          <Button disabled={luu.isPending || ds.length === 0} onClick={() => luu.mutate()}>
            {t('diemDanh.luu')}
          </Button>
        </div>
      </div>
    </Modal>
  )
}
