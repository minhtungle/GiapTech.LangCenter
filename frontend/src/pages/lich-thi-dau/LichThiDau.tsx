import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Plus, Pencil, Trash2, Archive, Filter, X, ListChecks } from 'lucide-react'
import { useNavigate } from 'react-router-dom'
import { api, layMaLoi } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Input, Label, Table, Td, Th, TrangTrong,
} from '@/components/ui'
import { Modal, ModalChan } from '@/components/ui/Modal'
import { SelectTimKiem } from '@/components/ui/SelectTimKiem'

type KetQua = 'ChuaCo' | 'Thang' | 'Hoa' | 'Thua'
type TrangThai = 'DaLenLich' | 'DaDienRa' | 'DaHuy' | 'LuuTru'

interface TranDauDto {
  id: string
  thoiGian: string
  doiThuId: string | null
  tenDoiThu: string | null
  tySoNha: number | null
  tySoKhach: number | null
  ketQua: KetQua
  trangThai: TrangThai
  linkVideo: string | null
  nhanXetChung: string | null
  ghiChu: string | null
}
interface DoiThuNgan {
  id: string
  tenDoi: string
  soTranDaDau: number
}

/** Màu trạng thái theo quy ước cố định — xem docs/frontend/ui-ux-nguyen-tac.md. */
const MAU_KET_QUA: Record<KetQua, 'win' | 'lose' | 'draw' | 'muted'> = {
  Thang: 'win',
  Thua: 'lose',
  Hoa: 'draw',
  ChuaCo: 'muted',
}

/** Chuyển ISO sang giá trị cho input datetime-local (theo giờ địa phương). */
function sangInputLocal(iso: string) {
  const d = new Date(iso)
  const p = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`
}

/** FR-07 lọc · FR-08 danh sách · FR-10 thêm/sửa · FR-11 xóa. */
export default function LichThiDau() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const navigate = useNavigate()

  const [moLoc, setMoLoc] = useState(false)
  const [loc, setLoc] = useState<{
    tuNgay: string
    denNgay: string
    ketQua: KetQua[]
    doiThuId: string | null
  }>({ tuNgay: '', denNgay: '', ketQua: [], doiThuId: null })

  const [moForm, setMoForm] = useState(false)
  const [dangSua, setDangSua] = useState<TranDauDto | null>(null)
  const [doiThuChon, setDoiThuChon] = useState<string | null>(null)
  const [trangThaiChon, setTrangThaiChon] = useState<TrangThai>('DaLenLich')
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [maLoiBang, setMaLoiBang] = useState<string | null>(null)

  const { data: doiThus } = useQuery({
    queryKey: ['doi-thu'],
    queryFn: async () => (await api.get<DoiThuNgan[]>('/doi-thu')).data,
  })

  const { data, isLoading } = useQuery({
    queryKey: ['tran-dau', loc],
    queryFn: async () =>
      (
        await api.post<TranDauDto[]>('/tran-dau/tim-kiem', {
          tuNgay: loc.tuNgay || null,
          denNgay: loc.denNgay || null,
          ketQua: loc.ketQua.length ? loc.ketQua : null,
          doiThuId: loc.doiThuId,
        })
      ).data,
  })

  const luu = useMutation({
    mutationFn: async (form: Record<string, unknown>) => {
      if (dangSua) await api.put(`/tran-dau/${dangSua.id}`, { ...form, id: dangSua.id })
      else await api.post('/tran-dau', form)
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['tran-dau'] })
      dongForm()
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const xoa = useMutation({
    mutationFn: async (id: string) => api.delete(`/tran-dau/${id}`),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['tran-dau'] }),
    onError: (e) => setMaLoiBang(layMaLoi(e)),
  })

  const luuTru = useMutation({
    mutationFn: async (id: string) => api.post(`/tran-dau/${id}/luu-tru`),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['tran-dau'] }),
    onError: (e) => setMaLoiBang(layMaLoi(e)),
  })

  const moThem = () => {
    setDangSua(null)
    setDoiThuChon(null)
    setTrangThaiChon('DaLenLich')
    setMaLoi(null)
    setMoForm(true)
  }

  const moSua = (tr: TranDauDto) => {
    setDangSua(tr)
    setDoiThuChon(tr.doiThuId)
    setTrangThaiChon(tr.trangThai)
    setMaLoi(null)
    setMoForm(true)
  }

  const dongForm = () => {
    setMoForm(false)
    setDangSua(null)
    setDoiThuChon(null)
    setMaLoi(null)
  }

  const onSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    const fd = new FormData(e.currentTarget)
    const nha = fd.get('tySoNha') as string
    const khach = fd.get('tySoKhach') as string

    // Mọi trường lệnh cập nhật ghi đè đều đọc TỪ FORM (quy tắc #1) — không gửi giá trị cứng.
    luu.mutate({
      thoiGian: new Date(String(fd.get('thoiGian'))).toISOString(),
      doiThuId: doiThuChon,
      tySoNha: nha === '' ? null : Number(nha),
      tySoKhach: khach === '' ? null : Number(khach),
      trangThai: trangThaiChon,
      linkVideo: (fd.get('linkVideo') as string) || null,
      nhanXetChung: (fd.get('nhanXetChung') as string) || null,
      ghiChu: (fd.get('ghiChu') as string) || null,
    })
  }

  const coLoc = loc.tuNgay || loc.denNgay || loc.ketQua.length > 0 || loc.doiThuId

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <Button variant={coLoc ? 'primary' : 'outline'} onClick={() => setMoLoc((v) => !v)}>
          <Filter className="h-4 w-4" />
          {t('tranDau.boLoc')}
          {coLoc && <Badge variant="accent">{t('tranDau.dangLoc')}</Badge>}
        </Button>
        <Button onClick={moThem}>
          <Plus className="h-4 w-4" />
          {t('tranDau.themMoi')}
        </Button>
      </div>

      {moLoc && (
        <div className="grid gap-3 rounded-lg border border-border bg-card p-4 sm:grid-cols-4">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="tuNgay">{t('tranDau.tuNgay')}</Label>
            <Input
              id="tuNgay"
              type="date"
              value={loc.tuNgay}
              onChange={(e) => setLoc({ ...loc, tuNgay: e.target.value })}
            />
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="denNgay">{t('tranDau.denNgay')}</Label>
            <Input
              id="denNgay"
              type="date"
              value={loc.denNgay}
              onChange={(e) => setLoc({ ...loc, denNgay: e.target.value })}
            />
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="locDoiThu">{t('tranDau.doiThu')}</Label>
            <SelectTimKiem
              id="locDoiThu"
              luaChon={(doiThus ?? []).map((d) => ({ giaTri: d.id, nhan: d.tenDoi }))}
              giaTri={loc.doiThuId}
              onDoi={(v) => setLoc({ ...loc, doiThuId: v })}
              placeholder={t('tranDau.moiDoiThu')}
            />
          </div>
          <div className="flex flex-col gap-1.5">
            <Label>{t('tranDau.ketQua')}</Label>
            <div className="flex flex-wrap gap-2 pt-1.5">
              {(['Thang', 'Hoa', 'Thua'] as const).map((k) => (
                <label key={k} className="flex items-center gap-1.5 text-sm">
                  <input
                    type="checkbox"
                    className="h-4 w-4 accent-[hsl(var(--primary))]"
                    checked={loc.ketQua.includes(k)}
                    onChange={(e) =>
                      setLoc({
                        ...loc,
                        ketQua: e.target.checked
                          ? [...loc.ketQua, k]
                          : loc.ketQua.filter((x) => x !== k),
                      })
                    }
                  />
                  {t(`tranDau.kq.${k}`)}
                </label>
              ))}
            </div>
          </div>
          {coLoc && (
            <div className="sm:col-span-4">
              <Button
                variant="ghost"
                size="sm"
                onClick={() => setLoc({ tuNgay: '', denNgay: '', ketQua: [], doiThuId: null })}
              >
                <X className="h-3.5 w-3.5" />
                {t('tranDau.xoaLoc')}
              </Button>
            </div>
          )}
        </div>
      )}

      {maLoiBang && <CanhBaoLoi>{t(`loi.${maLoiBang}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      {isLoading ? (
        <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
      ) : !data?.length ? (
        <TrangTrong
          thongDiep={coLoc ? t('tranDau.khongKhopLoc') : t('tranDau.chuaCo')}
          hanhDong={
            !coLoc && (
              <Button onClick={moThem}>
                <Plus className="h-4 w-4" />
                {t('tranDau.themMoi')}
              </Button>
            )
          }
        />
      ) : (
        <Table>
          <thead>
            <tr>
              <Th>{t('tranDau.thoiGian')}</Th>
              <Th>{t('tranDau.doiThu')}</Th>
              <Th className="text-center">{t('tranDau.tySo')}</Th>
              <Th>{t('tranDau.ketQua')}</Th>
              <Th>{t('tranDau.trangThai')}</Th>
              <Th className="w-36" />
            </tr>
          </thead>
          <tbody>
            {data.map((tr) => (
              <tr key={tr.id} className="hover:bg-muted/40">
                <Td className="whitespace-nowrap font-medium">
                  {new Date(tr.thoiGian).toLocaleString('vi-VN', {
                    day: '2-digit',
                    month: '2-digit',
                    year: 'numeric',
                    hour: '2-digit',
                    minute: '2-digit',
                  })}
                </Td>
                <Td>{tr.tenDoiThu ?? <span className="text-muted-foreground">—</span>}</Td>
                <Td className="text-center font-mono">
                  {tr.tySoNha !== null ? `${tr.tySoNha} – ${tr.tySoKhach}` : '—'}
                </Td>
                <Td>
                  <Badge variant={MAU_KET_QUA[tr.ketQua]}>{t(`tranDau.kq.${tr.ketQua}`)}</Badge>
                </Td>
                <Td className="text-muted-foreground">{t(`tranDau.tt.${tr.trangThai}`)}</Td>
                <Td>
                  <div className="flex gap-1">
                    <Button
                      variant="ghost"
                      size="sm"
                      title={t('tranDau.moChiTiet')}
                      onClick={() => navigate(`/lich-thi-dau/${tr.id}`)}
                    >
                      <ListChecks className="h-3.5 w-3.5" />
                    </Button>
                    <Button variant="ghost" size="sm" title={t('chung.sua')} onClick={() => moSua(tr)}>
                      <Pencil className="h-3.5 w-3.5" />
                    </Button>

                    {/*
                      Trận đã diễn ra không xóa cứng được (FR-11) — hiện nút Lưu trữ thay thế,
                      để người dùng không bấm Xóa rồi mới nhận thông báo từ chối.
                    */}
                    {tr.trangThai === 'DaLenLich' || tr.trangThai === 'DaHuy' ? (
                      <Button
                        variant="ghost"
                        size="sm"
                        title={t('chung.xoa')}
                        onClick={() => {
                          setMaLoiBang(null)
                          if (confirm(t('chung.xacNhanXoa'))) xoa.mutate(tr.id)
                        }}
                      >
                        <Trash2 className="h-3.5 w-3.5 text-destructive" />
                      </Button>
                    ) : tr.trangThai !== 'LuuTru' ? (
                      <Button
                        variant="ghost"
                        size="sm"
                        title={t('tranDau.luuTru')}
                        onClick={() => {
                          setMaLoiBang(null)
                          if (confirm(t('tranDau.xacNhanLuuTru'))) luuTru.mutate(tr.id)
                        }}
                      >
                        <Archive className="h-3.5 w-3.5" />
                      </Button>
                    ) : null}
                  </div>
                </Td>
              </tr>
            ))}
          </tbody>
        </Table>
      )}

      <Modal
        mo={moForm}
        onDong={dongForm}
        chanDoiKhiXuLy={luu.isPending}
        tieuDe={dangSua ? t('tranDau.suaTieuDe') : t('tranDau.themMoi')}
        rong="lg"
      >
        <form key={dangSua?.id ?? 'moi'} onSubmit={onSubmit} className="grid gap-4 sm:grid-cols-2">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="thoiGian">{t('tranDau.thoiGian')}</Label>
            <Input
              id="thoiGian"
              name="thoiGian"
              type="datetime-local"
              required
              autoFocus
              defaultValue={dangSua ? sangInputLocal(dangSua.thoiGian) : ''}
            />
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="doiThuId">{t('tranDau.doiThu')}</Label>
            <SelectTimKiem
              id="doiThuId"
              luaChon={(doiThus ?? []).map((d) => ({
                giaTri: d.id,
                nhan: d.tenDoi,
                phu: d.soTranDaDau > 0 ? `${d.soTranDaDau} trận đã đấu` : undefined,
              }))}
              giaTri={doiThuChon}
              onDoi={setDoiThuChon}
              placeholder={t('tranDau.chuaChonDoiThu')}
              placeholderTimKiem={t('tranDau.timDoiThu')}
            />
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="trangThai">{t('tranDau.trangThai')}</Label>
            <SelectTimKiem
              id="trangThai"
              choPhepXoa={false}
              luaChon={(['DaLenLich', 'DaDienRa', 'DaHuy', 'LuuTru'] as const).map((v) => ({
                giaTri: v,
                nhan: t(`tranDau.tt.${v}`),
              }))}
              giaTri={trangThaiChon}
              onDoi={(v) => setTrangThaiChon((v as TrangThai) ?? 'DaLenLich')}
            />
          </div>

          <div className="flex gap-2">
            <div className="flex flex-1 flex-col gap-1.5">
              <Label htmlFor="tySoNha">{t('tranDau.tySoNha')}</Label>
              <Input
                id="tySoNha"
                name="tySoNha"
                type="number"
                min={0}
                defaultValue={dangSua?.tySoNha ?? ''}
              />
            </div>
            <div className="flex flex-1 flex-col gap-1.5">
              <Label htmlFor="tySoKhach">{t('tranDau.tySoKhach')}</Label>
              <Input
                id="tySoKhach"
                name="tySoKhach"
                type="number"
                min={0}
                defaultValue={dangSua?.tySoKhach ?? ''}
              />
            </div>
          </div>

          <p className="text-xs text-muted-foreground sm:col-span-2">{t('tranDau.tySoGoiY')}</p>

          <div className="flex flex-col gap-1.5 sm:col-span-2">
            <Label htmlFor="linkVideo">{t('tranDau.linkVideo')}</Label>
            <Input
              id="linkVideo"
              name="linkVideo"
              type="url"
              placeholder="https://youtube.com/..."
              defaultValue={dangSua?.linkVideo ?? ''}
            />
          </div>

          <div className="flex flex-col gap-1.5 sm:col-span-2">
            <Label htmlFor="nhanXetChung">{t('tranDau.nhanXetChung')}</Label>
            <Input
              id="nhanXetChung"
              name="nhanXetChung"
              defaultValue={dangSua?.nhanXetChung ?? ''}
            />
          </div>

          <div className="flex flex-col gap-1.5 sm:col-span-2">
            <Label htmlFor="ghiChu">{t('cauThu.ghiChu')}</Label>
            <Input id="ghiChu" name="ghiChu" defaultValue={dangSua?.ghiChu ?? ''} />
          </div>

          {maLoi && (
            <div className="sm:col-span-2">
              <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>
            </div>
          )}

          <div className="sm:col-span-2">
            <ModalChan>
              <Button type="button" variant="outline" onClick={dongForm} disabled={luu.isPending}>
                {t('chung.huy')}
              </Button>
              <Button type="submit" disabled={luu.isPending}>
                {luu.isPending ? t('chung.dangTai') : t('chung.luu')}
              </Button>
            </ModalChan>
          </div>
        </form>
      </Modal>
    </div>
  )
}
