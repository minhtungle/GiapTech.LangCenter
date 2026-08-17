import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  Plus, Pencil, Trash2, Archive, ListChecks,
  Table2, CalendarDays, ChevronLeft, ChevronRight,
} from 'lucide-react'
import { useNavigate } from 'react-router-dom'
import { api, layMaLoi, trangRong, type KetQuaTrang } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Input, Label, Table, Td, Th, TrangTrong, Textarea,
} from '@/components/ui'
import { Modal, ModalChan } from '@/components/ui/Modal'
import { SelectTimKiem } from '@/components/ui/SelectTimKiem'
import { ChonDoiThu } from '@/components/ChonDoiThu'
import { cn } from '@/lib/utils'
import { LichThang, type SuKienLich } from '@/components/LichThang'
import {
  BoLocTranDau, NutBoLoc, BO_LOC_RONG, coLocNao, sangThamSoApi, type GiaTriBoLoc,
} from '@/components/BoLocTranDau'
import { PhanTrang } from '@/components/ui/PhanTrang'
import { ThSapXep } from '@/components/ui/ThSapXep'

const KHOA_CHE_DO = 'sr_lich_thi_dau_che_do'

type KetQua = 'ChuaCo' | 'Thang' | 'Hoa' | 'Thua'
/** Khớp enum CotSapXep ở backend — tên cột tự do không được nhận (chống SQL injection). */
type CotSapXep = 'ThoiGian' | 'DoiThu' | 'TySo' | 'KetQua' | 'TrangThai'
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
  nhanXetChung: string | null
  ghiChu: string | null
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

  // Nhớ chế độ giữa các phiên: người thích xem lịch không phải bấm lại mỗi lần vào.
  const [cheDo, setCheDo] = useState<'bang' | 'lich'>(
    () => (localStorage.getItem(KHOA_CHE_DO) as 'bang' | 'lich') ?? 'bang',
  )
  const [thangXem, setThangXem] = useState(() => {
    const n = new Date()
    return { nam: n.getFullYear(), thang: n.getMonth() + 1 }
  })

  useEffect(() => localStorage.setItem(KHOA_CHE_DO, cheDo), [cheDo])

  const [trang, setTrang] = useState(1)
  const [soDong, setSoDong] = useState(20)
  const [sapXep, setSapXep] = useState<{ cot: CotSapXep; tangDan: boolean }>({
    cot: 'ThoiGian',
    tangDan: false,
  })
  const [moLoc, setMoLoc] = useState(false)
  const [loc, setLoc] = useState<GiaTriBoLoc>(BO_LOC_RONG)

  const [moForm, setMoForm] = useState(false)
  const [dangSua, setDangSua] = useState<TranDauDto | null>(null)
  // Ngày điền sẵn khi mở form từ một ô trên lịch.
  const [ngayMacDinh, setNgayMacDinh] = useState<string | null>(null)
  const [doiThuChon, setDoiThuChon] = useState<string | null>(null)
  const [trangThaiChon, setTrangThaiChon] = useState<TrangThai>('DaLenLich')
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [maLoiBang, setMaLoiBang] = useState<string | null>(null)


  const { data: ketQua, isLoading } = useQuery({
    queryKey: ['tran-dau', loc, trang, soDong, sapXep],
    queryFn: async () =>
      (
        await api.post<KetQuaTrang<TranDauDto>>(
          `/tran-dau/tim-kiem?trang=${trang}&soDong=${soDong}` +
            `&cot=${sapXep.cot}&tangDan=${sapXep.tangDan}`,
          sangThamSoApi(loc),
        )
      ).data,
  })

  /** Đổi cột sắp xếp → về trang 1: trang 3 của thứ tự cũ không có nghĩa gì ở thứ tự mới. */
  const doiSapXep = (cot: CotSapXep, tangDan: boolean) => {
    setSapXep({ cot, tangDan })
    setTrang(1)
  }

  // Calendar dùng endpoint riêng: lịch tháng phải hiện ĐỦ trận, cắt trang sẽ làm mất trận
  // khỏi ô ngày mà người dùng không biết.
  const { data: tranThang } = useQuery({
    queryKey: ['tran-dau-thang', thangXem, loc],
    enabled: cheDo === 'lich',
    queryFn: async () =>
      (
        await api.post<TranDauDto[]>(
          `/tran-dau/theo-thang?nam=${thangXem.nam}&thang=${thangXem.thang}`,
          { ketQua: loc.ketQua.length ? loc.ketQua : null, doiThuId: loc.doiThuId },
        )
      ).data,
  })

  const kq = ketQua ?? trangRong<TranDauDto>()
  const data = kq.duLieu

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

  const moThemTuNgay = (ngay: string) => {
    setDangSua(null)
    // 15:00 là giờ đá phổ biến của CLB phong trào — đỡ cho người dùng một bước chỉnh.
    setNgayMacDinh(`${ngay}T15:00`)
    setDoiThuChon(null)
    setTrangThaiChon('DaLenLich')
    setMaLoi(null)
    setMoForm(true)
  }

  const moThem = () => {
    setDangSua(null)
    setNgayMacDinh(null)
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
    setNgayMacDinh(null)
    setDoiThuChon(null)
    setMaLoi(null)
  }

  const onSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    const fd = new FormData(e.currentTarget)
    const khach = fd.get('tySoKhach') as string

    // Mọi trường lệnh cập nhật ghi đè đều đọc TỪ FORM (quy tắc #1) — không gửi giá trị cứng.
    // `tySoNha` không nằm trong form vì nó là tổng bàn thắng cầu thủ, nhập ở tab đánh giá.
    luu.mutate({
      thoiGian: new Date(String(fd.get('thoiGian'))).toISOString(),
      doiThuId: doiThuChon,
      tySoKhach: khach === '' ? null : Number(khach),
      trangThai: trangThaiChon,
      nhanXetChung: (fd.get('nhanXetChung') as string) || null,
      ghiChu: (fd.get('ghiChu') as string) || null,
    })
  }

  const doiThang = (buoc: number) => {
    const d = new Date(thangXem.nam, thangXem.thang - 1 + buoc, 1)
    setThangXem({ nam: d.getFullYear(), thang: d.getMonth() + 1 })
  }

  const veHomNay = () => {
    const n = new Date()
    setThangXem({ nam: n.getFullYear(), thang: n.getMonth() + 1 })
  }

  /** Màu chấm theo kết quả — dùng chung token với badge ở bảng để hai màn khớp nhau. */
  const MAU_CHAM: Record<KetQua, string> = {
    Thang: 'hsl(var(--status-win))',
    Thua: 'hsl(var(--status-lose))',
    Hoa: 'hsl(var(--status-draw))',
    ChuaCo: 'hsl(var(--muted-foreground))',
  }

  const suKienLich: SuKienLich[] = (tranThang ?? []).map((tr) => {
    const d = new Date(tr.thoiGian)
    const p = (n: number) => String(n).padStart(2, '0')
    const tySo =
      tr.tySoNha !== null || tr.tySoKhach !== null
        ? ` ${tr.tySoNha ?? 0}-${tr.tySoKhach ?? 0}`
        : ''
    return {
      ngay: `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`,
      tranDauId: tr.id,
      // Tooltip đầy đủ: giờ + đối thủ + tỷ số.
      nhan: `${p(d.getHours())}:${p(d.getMinutes())} ${tr.tenDoiThu ?? ''}${tySo}`.trim(),
      // Nhãn trong ô hẹp: ưu tiên tên đối thủ; chưa có thì dùng tỷ số, cuối cùng là giờ.
      tenNgan: tr.tenDoiThu ?? (tySo.trim() || `${p(d.getHours())}:${p(d.getMinutes())}`),
      mau: MAU_CHAM[tr.ketQua],
    }
  })

  const coLoc = coLocNao(loc)

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <NutBoLoc mo={moLoc} onDoi={setMoLoc} loc={loc} />
        <div className="flex items-center gap-2">
          {/* Chuyển Bảng ↔ Lịch, giữ nguyên bộ lọc đang áp (FR-08). */}
          <div className="flex overflow-hidden rounded-md border border-input">
            <button
              type="button"
              onClick={() => setCheDo('bang')}
              aria-pressed={cheDo === 'bang'}
              className={cn(
                'flex items-center gap-1.5 px-3 py-1.5 text-sm transition-colors',
                cheDo === 'bang' ? 'bg-primary text-primary-foreground' : 'hover:bg-muted',
              )}
            >
              <Table2 className="h-4 w-4" />
              {t('tranDau.cheDoBang')}
            </button>
            <button
              type="button"
              onClick={() => setCheDo('lich')}
              aria-pressed={cheDo === 'lich'}
              className={cn(
                'flex items-center gap-1.5 px-3 py-1.5 text-sm transition-colors',
                cheDo === 'lich' ? 'bg-primary text-primary-foreground' : 'hover:bg-muted',
              )}
            >
              <CalendarDays className="h-4 w-4" />
              {t('tranDau.cheDoLich')}
            </button>
          </div>

          <Button onClick={moThem}>
            <Plus className="h-4 w-4" />
            {t('tranDau.themMoi')}
          </Button>
        </div>
      </div>

      {moLoc && <BoLocTranDau loc={loc} onDoi={setLoc} />}

      {maLoiBang && <CanhBaoLoi>{t(`loi.${maLoiBang}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      {cheDo === 'lich' ? (
        <div className="flex flex-col gap-3">
          <div className="flex items-center justify-center gap-2">
            <Button variant="ghost" size="sm" onClick={() => doiThang(-1)} aria-label={t('tranDau.thangTruoc')}>
              <ChevronLeft className="h-4 w-4" />
            </Button>
            <span className="min-w-40 text-center text-sm font-semibold">
              {t('tranDau.thang')} {thangXem.thang} / {thangXem.nam}
            </span>
            <Button variant="ghost" size="sm" onClick={() => doiThang(1)} aria-label={t('tranDau.thangSau')}>
              <ChevronRight className="h-4 w-4" />
            </Button>
            <Button variant="outline" size="sm" onClick={veHomNay}>
              {t('tranDau.homNay')}
            </Button>
          </div>

          <LichThang
            nam={thangXem.nam}
            thang={thangXem.thang}
            suKien={suKienLich}
            onChonTran={(id) => navigate(`/lich-thi-dau/${id}`)}
            onChonNgay={moThemTuNgay}
          />

          <p className="text-xs text-muted-foreground">{t('tranDau.lichGoiY')}</p>

          {/* Chú giải màu — người dùng không phải đoán chấm nào nghĩa gì. */}
          <div className="flex flex-wrap items-center gap-4 text-xs text-muted-foreground">
            {([
              ['Thang', 'hsl(var(--status-win))'],
              ['Thua', 'hsl(var(--status-lose))'],
              ['Hoa', 'hsl(var(--status-draw))'],
              ['ChuaCo', 'hsl(var(--muted-foreground))'],
            ] as const).map(([k, mau]) => (
              <span key={k} className="flex items-center gap-1.5">
                <span
                  className="inline-block h-2 w-2 rounded-full"
                  style={{ background: mau }}
                />
                {t(`tranDau.kq.${k}`)}
              </span>
            ))}
          </div>
        </div>
      ) : isLoading ? (
        <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
      ) : !data.length ? (
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
              {(
                [
                  ['ThoiGian', t('tranDau.thoiGian')],
                  ['DoiThu', t('tranDau.doiThu')],
                  ['TySo', t('tranDau.tySo')],
                  ['KetQua', t('tranDau.ketQua')],
                  ['TrangThai', t('tranDau.trangThai')],
                ] as [CotSapXep, string][]
              ).map(([cot, nhan]) => (
                <ThSapXep
                  key={cot}
                  cot={cot}
                  cotHienTai={sapXep.cot}
                  tangDan={sapXep.tangDan}
                  onDoi={doiSapXep}
                >
                  {nhan}
                </ThSapXep>
              ))}
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
                  {tr.tySoNha !== null || tr.tySoKhach !== null
                    ? `${tr.tySoNha ?? 0} – ${tr.tySoKhach ?? 0}`
                    : '—'}
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

      {cheDo === 'bang' && data.length > 0 && (
        <PhanTrang
          trang={kq.trang}
          soDong={kq.soDong}
          tongSoDong={kq.tongSoDong}
          tongSoTrang={kq.tongSoTrang}
          onDoiTrang={setTrang}
          onDoiSoDong={(n) => {
            setSoDong(n)
            setTrang(1)
          }}
        />
      )}

      <Modal
        mo={moForm}
        onDong={dongForm}
        chanDoiKhiXuLy={luu.isPending}
        tieuDe={dangSua ? t('tranDau.suaTieuDe') : t('tranDau.themMoi')}
        rong="lg"
      >
        <form
          key={dangSua?.id ?? ngayMacDinh ?? 'moi'}
          onSubmit={onSubmit}
          className="grid gap-4 sm:grid-cols-2"
        >
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="thoiGian">{t('tranDau.thoiGian')}</Label>
            <Input
              id="thoiGian"
              name="thoiGian"
              type="datetime-local"
              required
              autoFocus
              defaultValue={dangSua ? sangInputLocal(dangSua.thoiGian) : (ngayMacDinh ?? '')}
            />
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="doiThuId">{t('tranDau.doiThu')}</Label>
            <ChonDoiThu giaTri={doiThuChon} onDoi={setDoiThuChon} />
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

          {/* Bàn thắng chỉ HIỆN ở đây — nó là tổng bàn cầu thủ ghi, nhập ở tab đánh giá của
              màn chi tiết. Để ô nhập ở cả hai chỗ thì hai con số sẽ đá nhau. */}
          <div className="flex gap-2">
            <div className="flex flex-1 flex-col gap-1.5">
              <Label>{t('tranDau.tySoNha')}</Label>
              <div className="flex h-9 items-center rounded-md border border-dashed border-border bg-muted/40 px-3 text-sm font-semibold tabular-nums">
                {dangSua?.tySoNha ?? '—'}
              </div>
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

          <p className="text-xs text-muted-foreground sm:col-span-2">
            {t('tranDau.tySoNhaTuTinh')}
          </p>

          <div className="flex flex-col gap-1.5 sm:col-span-2">
            <Label htmlFor="nhanXetChung">{t('tranDau.nhanXetChung')}</Label>
            <Textarea
              id="nhanXetChung"
              name="nhanXetChung"
              defaultValue={dangSua?.nhanXetChung ?? ''}
            />
          </div>

          <div className="flex flex-col gap-1.5 sm:col-span-2">
            <Label htmlFor="ghiChu">{t('cauThu.ghiChu')}</Label>
            <Textarea id="ghiChu" name="ghiChu" defaultValue={dangSua?.ghiChu ?? ''} />
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
