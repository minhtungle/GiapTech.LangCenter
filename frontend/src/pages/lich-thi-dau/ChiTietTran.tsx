import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useNavigate, useParams } from 'react-router-dom'
import {
  ArrowLeft, BookmarkPlus, ChevronDown, Copy, Heart, Pencil, Plus, Save, Users,
} from 'lucide-react'
import { api, layMaLoi, type KetQuaTrang } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Input, Label, Table, Td, Th, TrangTrong, Textarea,
} from '@/components/ui'
import { Modal, ModalChan } from '@/components/ui/Modal'
import { SelectTimKiem, SelectTimKiemNhieu } from '@/components/ui/SelectTimKiem'
import { cn } from '@/lib/utils'
import { SoDoSan, type CauThuTrenSan, type QuanTrenSan, type Ben } from '@/components/SoDoSan'
import {
  CAU_HINH_SAN, CAC_LOAI_SAN, BANG_MAU_AO, chuanHoaLoaiSan, timMauAo,
  MAU_MAC_DINH_TA, MAU_MAC_DINH_DOI_THU, type LoaiSan,
} from '@/components/soDo/loaiSan'
import { HopXacNhan } from '@/components/ui/HopXacNhan'
import { QuanLyVideoTran } from '@/components/VideoTran'
import {
  ChamChiSo, RadarChiSo, docChiSo, ghiChiSo, diemTrungBinh, type BoChiSo,
} from '@/components/ChamChiSo'

interface TranDauDto {
  id: string
  thoiGian: string
  doiThuId: string | null
  tenDoiThu: string | null
  tySoNha: number | null
  tySoKhach: number | null
  ketQua: 'ChuaCo' | 'Thang' | 'Hoa' | 'Thua'
  trangThai: 'DaLenLich' | 'DaDienRa' | 'DaHuy' | 'LuuTru'
  nhanXetChung: string | null
  ghiChu: string | null
}
interface DoiThuNgan {
  id: string
  tenDoi: string
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
  chiSoKyNang: string | null
  ghiChu: string | null
  soPhieuMvp: number
  toiDaVote: boolean
}
interface CauThuNgan {
  id: string
  hoTen: string
  soAo: number | null
  viTriSoTruong: string | null
}
interface MauDoiHinhDto {
  id: string
  ten: string
  loaiSan: number
  ghiChu: string | null
  noiDungJson: string
  soCauThu: number
}

export type Hiep = 'hiep1' | 'hiep2'

/** Nội dung `SODO_CHIENTHUAT.so_do_json` sau khi đã chuẩn hoá. */
export interface NoiDungSoDo {
  loaiSan: LoaiSan
  /** Mã màu áo trong BANG_MAU_AO — chung cho cả hai hiệp, đội không đổi áo giữa trận. */
  mauTa: string
  mauDoiThu: string
  hiep1: { ta: QuanTrenSan[]; doiThu: QuanTrenSan[] }
  hiep2: { ta: QuanTrenSan[]; doiThu: QuanTrenSan[] }
}

const SO_DO_RONG: NoiDungSoDo = {
  loaiSan: 11,
  mauTa: MAU_MAC_DINH_TA,
  mauDoiThu: MAU_MAC_DINH_DOI_THU,
  hiep1: { ta: [], doiThu: [] },
  hiep2: { ta: [], doiThu: [] },
}

/**
 * Đọc sơ đồ đã lưu, chấp nhận CẢ HAI dạng.
 *
 * Dạng cũ phẳng `{viTri:[{cauThuId,x,y}], doiThu:[...]}` được coi là hiệp 1 và đổi `cauThuId`
 * thành `id`. Không có bước này thì mọi sơ đồ lưu trước hôm nay biến mất khỏi màn hình —
 * đúng kiểu mất dữ liệu mà quy tắc #1 cấm.
 *
 * JSON hỏng trả về sơ đồ rỗng chứ không ném lỗi: một bản ghi lỗi không được làm sập cả tab.
 */
export function docSoDo(json: string | null | undefined): NoiDungSoDo {
  if (!json) return SO_DO_RONG

  try {
    const o = JSON.parse(json) as Record<string, unknown>
    const loaiSan = chuanHoaLoaiSan(o.loaiSan)
    // Bản ghi cũ không có màu → về mặc định trắng/đỏ, đúng như nó vẫn hiển thị trước đây.
    const mauTa = typeof o.mauTa === 'string' ? o.mauTa : MAU_MAC_DINH_TA
    const mauDoiThu = typeof o.mauDoiThu === 'string' ? o.mauDoiThu : MAU_MAC_DINH_DOI_THU

    const docQuan = (v: unknown): QuanTrenSan[] =>
      Array.isArray(v)
        ? v
            .map((x) => x as Record<string, unknown>)
            // `cauThuId` là tên trường của dạng cũ.
            .map((x) => ({
              id: String(x.id ?? x.cauThuId ?? ''),
              x: Number(x.x) || 0,
              y: Number(x.y) || 0,
              viTri: typeof x.viTri === 'string' ? x.viTri : undefined,
              so: typeof x.so === 'number' ? x.so : undefined,
              ten: typeof x.ten === 'string' ? x.ten : undefined,
            }))
            .filter((q) => q.id !== '')
        : []

    const docHiep = (v: unknown) => {
      const h = (v ?? {}) as Record<string, unknown>
      return { ta: docQuan(h.ta), doiThu: docQuan(h.doiThu) }
    }

    // Dạng cũ: không có hiep1 nhưng có viTri ở gốc.
    if (o.hiep1 === undefined && o.viTri !== undefined) {
      return {
        loaiSan,
        mauTa,
        mauDoiThu,
        hiep1: { ta: docQuan(o.viTri), doiThu: docQuan(o.doiThu) },
        hiep2: { ta: [], doiThu: [] },
      }
    }

    return { loaiSan, mauTa, mauDoiThu, hiep1: docHiep(o.hiep1), hiep2: docHiep(o.hiep2) }
  } catch {
    return SO_DO_RONG
  }
}

/**
 * Bốn tab của màn chi tiết trận (FR-10).
 *
 * Đội hình và sơ đồ gộp làm một vì chúng là **một việc**: chọn ai đá rồi xếp người đó lên sân.
 *
 * Video tách riêng khỏi Đánh giá: gắn link là việc làm ngay sau trận, thường do người khác
 * làm (ai quay thì người đó dán link), còn chấm điểm cầu thủ là việc của ban huấn luyện làm
 * sau. Nhét chung một tab thì mỗi lần dán link phải cuộn qua cả bảng chấm 6 chỉ số × 11 người.
 */
type Tab = 'thong-tin' | 'doi-hinh' | 'video' | 'danh-gia'

/** FR-10 — chi tiết trận đấu, wizard 3 tab theo đặc tả. */
export default function ChiTietTran() {
  const { t } = useTranslation()
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [tab, setTab] = useState<Tab>('thong-tin')
  const [maLoi, setMaLoi] = useState<string | null>(null)

  const { data: tran } = useQuery({
    queryKey: ['tran-dau', id],
    queryFn: async () => (await api.get<TranDauDto>(`/tran-dau/${id}`)).data,
  })

  const tabs: { khoa: Tab; nhan: string }[] = [
    // FR-10 tab (a) gồm cả thông tin chung — trước đây phải quay về bảng mới sửa được.
    { khoa: 'thong-tin', nhan: t('chiTiet.tabThongTin') },
    { khoa: 'doi-hinh', nhan: t('chiTiet.tabDoiHinh') },
    { khoa: 'video', nhan: t('chiTiet.tabVideo') },
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
            {/* Hiện khi có BẤT KỲ bên nào — hai bên nhập ở hai chỗ khác nhau nên thường lệch
                pha: ghi bàn thắng cầu thủ xong mà chưa nhập bàn thua thì vẫn phải thấy số. */}
            {(tran.tySoNha !== null || tran.tySoKhach !== null) && (
              <span className="font-mono font-semibold">
                {tran.tySoNha ?? 0} – {tran.tySoKhach ?? 0}
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

      {tab === 'thong-tin' && <TabThongTin tranDauId={id} onLoi={setMaLoi} />}
      {tab === 'doi-hinh' && <TabDoiHinh tranDauId={id} onLoi={setMaLoi} qc={qc} />}
      {tab === 'video' && <QuanLyVideoTran tranDauId={id} onLoi={setMaLoi} />}
      {tab === 'danh-gia' && <TabDanhGia tranDauId={id} onLoi={setMaLoi} />}
    </div>
  )
}

/**
 * Tab thông tin chung — sửa ngay tại màn chi tiết.
 *
 * Trước đây phải quay về bảng mới sửa được thời gian/tỷ số, trong khi đây đúng là chỗ người
 * dùng đang đứng khi cập nhật kết quả sau trận.
 */
function TabThongTin({
  tranDauId,
  onLoi,
}: {
  tranDauId: string
  onLoi: (m: string | null) => void
}) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [doiThuChon, setDoiThuChon] = useState<string | null | undefined>(undefined)
  const [trangThaiChon, setTrangThaiChon] = useState<TranDauDto['trangThai'] | null>(null)
  const [daLuu, setDaLuu] = useState(false)

  const { data, isLoading } = useQuery({
    queryKey: ['tran-dau', tranDauId],
    queryFn: async () => (await api.get<TranDauDto>(`/tran-dau/${tranDauId}`)).data,
  })
  const { data: doiThus } = useQuery({
    queryKey: ['doi-thu'],
    queryFn: async () =>
      (await api.get<KetQuaTrang<DoiThuNgan>>('/doi-thu', { params: { soDong: 200 } })).data.duLieu,
  })

  const luu = useMutation({
    mutationFn: async (form: Record<string, unknown>) =>
      api.put(`/tran-dau/${tranDauId}`, { ...form, id: tranDauId }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['tran-dau'] })
      onLoi(null)
      setDaLuu(true)
      setTimeout(() => setDaLuu(false), 2500)
    },
    onError: (e) => onLoi(layMaLoi(e)),
  })

  if (isLoading || !data) {
    return <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
  }

  // undefined = chưa chạm tới, lấy giá trị server; null = người dùng chủ động bỏ chọn.
  const doiThu = doiThuChon === undefined ? data.doiThuId : doiThuChon
  const trangThai = trangThaiChon ?? data.trangThai

  const onSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    const fd = new FormData(e.currentTarget)
    const khach = fd.get('tySoKhach') as string

    // Mọi trường lệnh cập nhật ghi đè đều đọc từ form (quy tắc #1).
    // `tySoNha` KHÔNG gửi: nó do tab đánh giá tính từ bàn thắng cầu thủ, backend cũng đã bỏ
    // trường này khỏi lệnh — gửi lên chỉ bị lờ đi.
    luu.mutate({
      thoiGian: new Date(String(fd.get('thoiGian'))).toISOString(),
      doiThuId: doiThu,
      tySoKhach: khach === '' ? null : Number(khach),
      trangThai,
      nhanXetChung: (fd.get('nhanXetChung') as string) || null,
      ghiChu: (fd.get('ghiChu') as string) || null,
    })
  }

  return (
    <form onSubmit={onSubmit} className="grid max-w-3xl gap-4 sm:grid-cols-2">
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="thoiGian">{t('tranDau.thoiGian')}</Label>
        <Input
          id="thoiGian"
          name="thoiGian"
          type="datetime-local"
          required
          defaultValue={sangInputLocal(data.thoiGian)}
        />
      </div>

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="doiThuId">{t('tranDau.doiThu')}</Label>
        <SelectTimKiem
          id="doiThuId"
          luaChon={(doiThus ?? []).map((d) => ({ giaTri: d.id, nhan: d.tenDoi }))}
          giaTri={doiThu}
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
          giaTri={trangThai}
          onDoi={(v) => setTrangThaiChon((v as TranDauDto['trangThai']) ?? 'DaLenLich')}
        />
      </div>

      {/*
        Bàn thắng đội nhà chỉ HIỆN, không sửa được ở đây — nó là tổng bàn cầu thủ ghi ở tab
        đánh giá. Để ô nhập ở cả hai chỗ thì con số nào cũng có thể đúng, không biết tin bên nào.
      */}
      <div className="flex gap-2">
        <div className="flex flex-1 flex-col gap-1.5">
          <Label>{t('tranDau.tySoNha')}</Label>
          <div className="flex h-9 items-center rounded-md border border-dashed border-border bg-muted/40 px-3 text-sm font-semibold tabular-nums">
            {data.tySoNha ?? '—'}
          </div>
        </div>
        <div className="flex flex-1 flex-col gap-1.5">
          <Label htmlFor="tySoKhach">{t('tranDau.tySoKhach')}</Label>
          <Input
            id="tySoKhach"
            name="tySoKhach"
            type="number"
            min={0}
            defaultValue={data.tySoKhach ?? ''}
          />
        </div>
      </div>

      <p className="text-xs text-muted-foreground sm:col-span-2">{t('tranDau.tySoNhaTuTinh')}</p>

      <div className="flex flex-col gap-1.5 sm:col-span-2">
        <Label htmlFor="nhanXetChung">{t('tranDau.nhanXetChung')}</Label>
        <Textarea id="nhanXetChung" name="nhanXetChung" defaultValue={data.nhanXetChung ?? ''} />
      </div>

      <div className="flex flex-col gap-1.5 sm:col-span-2">
        <Label htmlFor="ghiChu">{t('cauThu.ghiChu')}</Label>
        <Textarea id="ghiChu" name="ghiChu" defaultValue={data.ghiChu ?? ''} />
      </div>

      <div className="flex items-center gap-3 sm:col-span-2">
        <Button type="submit" disabled={luu.isPending}>
          <Save className="h-4 w-4" />
          {luu.isPending ? t('chung.dangTai') : t('chung.luu')}
        </Button>
        {daLuu && <span className="text-sm text-status-win">{t('chiTiet.daLuu')}</span>}
      </div>
    </form>
  )
}

/** Chuyển ISO sang giá trị cho input datetime-local (theo giờ địa phương). */
function sangInputLocal(iso: string) {
  const d = new Date(iso)
  const p = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`
}

/**
 * Tab (a+b) gộp — chọn thành viên VÀ xếp họ lên sân trong cùng một màn.
 *
 * Bố cục **side by side**: sân bên trái, bảng điều khiển bên phải (xuống một cột trên mobile).
 * Xếp dọc thì mỗi lần đổi sơ đồ phải cuộn lên xem kết quả rồi cuộn xuống bấm tiếp.
 *
 * **Hai hiệp** lưu trong cùng bản ghi `SODO_CHIENTHUAT`: đội bóng phong trào hay đổi người và
 * đổi sơ đồ giữa giờ nghỉ, một sơ đồ cho cả trận không tả được điều đó.
 *
 * **Hai nút Lưu riêng** cho `DOIHINH_TRANDAU` và `SODO_CHIENTHUAT`: một nút lưu tất thì bấm
 * lưu sơ đồ cũng ghi đè đội hình, dễ mất người vừa thêm (quy tắc #1).
 */
function TabDoiHinh({
  tranDauId, onLoi, qc,
}: {
  tranDauId: string
  onLoi: (m: string | null) => void
  qc: ReturnType<typeof useQueryClient>
}) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [chon, setChon] = useState<string[] | null>(null)
  const [moChonNguoi, setMoChonNguoi] = useState(false)
  const [nhap, setNhap] = useState<NoiDungSoDo | null>(null)
  const [hiep, setHiep] = useState<Hiep>('hiep1')
  const [ben, setBen] = useState<Ben>('ta')
  const [ghiChu, setGhiChu] = useState<string | null>(null)
  const [daLuuSoDo, setDaLuuSoDo] = useState(false)
  const [moLuuMau, setMoLuuMau] = useState(false)
  const [mauChon, setMauChon] = useState<string | null>(null)
  /**
   * Việc đang chờ xác nhận. Một state cho mọi thao tác phá huỷ thay vì mỗi thao tác một cờ:
   * mỗi lúc chỉ có một hộp thoại mở, và thêm thao tác mới không phải thêm state mới.
   */
  const [choXacNhan, setChoXacNhan] = useState<{
    tieuDe: string
    thongDiep: string
    lam: () => void
  } | null>(null)

  /** Chạy ngay nếu không mất gì; có dữ liệu sẽ bị ghi đè thì hỏi trước. */
  const hoiTruocKhiGhiDe = (
    soQuanMat: number,
    tieuDe: string,
    thongDiep: string,
    lam: () => void,
  ) => {
    if (soQuanMat === 0) { lam(); return }
    setChoXacNhan({ tieuDe, thongDiep, lam })
  }

  const { data: doiHinh, isLoading } = useQuery({
    queryKey: ['doi-hinh', tranDauId],
    queryFn: async () => (await api.get<DoiHinhDto[]>(`/tran-dau/${tranDauId}/doi-hinh`)).data,
  })
  const { data: cauThus } = useQuery({
    queryKey: ['cau-thu'],
    queryFn: async () =>
      (await api.get<KetQuaTrang<CauThuNgan>>('/cau-thu', { params: { soDong: 200 } })).data.duLieu,
  })
  const { data: soDo } = useQuery({
    queryKey: ['so-do', tranDauId],
    queryFn: async () =>
      (await api.get<{ soDoJson: string; ghiChuChienThuat: string | null }>(
        `/tran-dau/${tranDauId}/so-do`,
      )).data,
  })
  const { data: thietLap } = useQuery({
    queryKey: ['thiet-lap'],
    queryFn: async () => (await api.get<{ mauAo: string[] }>('/thiet-lap')).data,
  })
  const { data: mauDs } = useQuery({
    queryKey: ['mau-doi-hinh'],
    queryFn: async () =>
      (await api.get<KetQuaTrang<MauDoiHinhDto>>('/mau-doi-hinh', { params: { soDong: 100 } }))
        .data.duLieu,
  })

  // Khởi tạo từ dữ liệu server, sau đó để người dùng chỉnh tự do.
  const dangChon = chon ?? doiHinh?.map((d) => d.cauThuId) ?? []

  const luuDoiHinh = useMutation({
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
      setMoChonNguoi(false)
    },
    onError: (e) => onLoi(layMaLoi(e)),
  })

  const luuSoDo = useMutation({
    mutationFn: async (nd: NoiDungSoDo) =>
      api.put(`/tran-dau/${tranDauId}/so-do`, {
        tranDauId,
        soDoJson: JSON.stringify(nd),
        ghiChuChienThuat: ghiChu ?? soDo?.ghiChuChienThuat ?? null,
      }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['so-do', tranDauId] })
      onLoi(null)
      setDaLuuSoDo(true)
      setTimeout(() => setDaLuuSoDo(false), 2500)
    },
    onError: (e) => onLoi(layMaLoi(e)),
  })

  const luuMau = useMutation({
    mutationFn: async (ten: string) =>
      api.post(`/mau-doi-hinh/tu-tran/${tranDauId}`, { tranDauId, ten, ghiChu: null }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['mau-doi-hinh'] })
      onLoi(null)
      setMoLuuMau(false)
    },
    onError: (e) => onLoi(layMaLoi(e)),
  })

  if (isLoading) return <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>

  const trenSan: CauThuTrenSan[] = (doiHinh ?? []).map((d) => ({
    id: d.cauThuId,
    hoTen: d.hoTen,
    laDuBi: d.laDuBi,
    soAo: cauThus?.find((c) => c.id === d.cauThuId)?.soAo,
    viTriSoTruong: cauThus?.find((c) => c.id === d.cauThuId)?.viTriSoTruong,
  }))

  const dangDung = nhap ?? docSoDo(soDo?.soDoJson)
  const cuaHiep = dangDung[hiep]
  const gc = ghiChu ?? soDo?.ghiChuChienThuat ?? ''
  const loaiSan = dangDung.loaiSan

  const doiNoiDung = (thay: Partial<NoiDungSoDo>) => setNhap({ ...dangDung, ...thay })
  const mauCua = (b: Ben) =>
    timMauAo(b === 'ta' ? dangDung.mauTa : dangDung.mauDoiThu,
      b === 'ta' ? MAU_MAC_DINH_TA : MAU_MAC_DINH_DOI_THU)

  /**
   * Chỉ cho chọn trong **bộ áo CLB đã khai** ở Thiết lập chung.
   *
   * CLB chưa khai (mảng rỗng) thì mở toàn bộ bảng màu — khoá người dùng khỏi tính năng chỉ vì
   * họ chưa vào màn thiết lập là chặn nhầm chỗ.
   *
   * Màu đang dùng LUÔN có mặt kể cả khi CLB vừa bỏ nó khỏi bộ áo: nếu không, sơ đồ cũ hiện
   * một màu mà bảng chọn không có ô nào sáng, người dùng tưởng hỏng.
   */
  const boAoClb = thietLap?.mauAo ?? []
  const mauChonDuoc =
    boAoClb.length === 0
      ? BANG_MAU_AO
      : BANG_MAU_AO.filter(
          (m) =>
            boAoClb.includes(m.ma) ||
            m.ma === dangDung.mauTa ||
            m.ma === dangDung.mauDoiThu,
        )
  const doiHiep = (h: Hiep, q: Partial<{ ta: QuanTrenSan[]; doiThu: QuanTrenSan[] }>) =>
    doiNoiDung({ [h]: { ...dangDung[h], ...q } } as Partial<NoiDungSoDo>)

  /** Xếp nhanh theo sơ đồ dựng sẵn — áp cho BÊN đang chọn, hiệp đang xem. */
  const apSoDo = (ten: string) =>
    hoiTruocKhiGhiDe(
      cuaHiep[ben].length,
      t('soDo.xacNhanApSoDo'),
      t('soDo.xacNhanApSoDoMoTa', {
        soDo: ten,
        soNguoi: cuaHiep[ben].length,
        ben: t(ben === 'ta' ? 'soDo.doiNha' : 'soDo.doiKhach'),
        hiep: t(`soDo.${hiep}`),
      }),
      () => apSoDoNgay(ten),
    )

  const apSoDoNgay = (ten: string) => {
    const mau = CAU_HINH_SAN[loaiSan].soDo[ten]
    if (!mau) return

    if (ben === 'ta') {
      // Đá chính trước; số áo lấy từ hồ sơ, giữ số người dùng đã sửa tay cho trận này.
      const daChinh = trenSan.filter((c) => !c.laDuBi).slice(0, mau.length)
      doiHiep(hiep, {
        ta: daChinh.map((c, i) => {
          const cu = cuaHiep.ta.find((q) => q.id === c.id)
          return {
            id: c.id,
            x: mau[i].x,
            y: mau[i].y,
            viTri: mau[i].vt,
            so: cu?.so ?? c.soAo ?? undefined,
          }
        }),
      })
    } else {
      // Đối thủ không có hồ sơ nên đánh số 1..n theo thứ tự trong sơ đồ.
      doiHiep(hiep, {
        doiThu: mau.map((m, i) => ({
          id: `dt-${i + 1}`,
          x: m.x,
          y: m.y,
          viTri: m.vt,
          so: cuaHiep.doiThu[i]?.so ?? i + 1,
          ten: cuaHiep.doiThu[i]?.ten,
        })),
      })
    }
  }

  /** Chép sơ đồ hiệp 1 sang hiệp 2 — điểm khởi đầu tự nhiên khi chỉ đổi vài người. */
  const chepSangHiep2 = () =>
    hoiTruocKhiGhiDe(
      dangDung.hiep2.ta.length + dangDung.hiep2.doiThu.length,
      t('soDo.xacNhanChep'),
      t('soDo.xacNhanChepMoTa', {
        soNguoi: dangDung.hiep2.ta.length + dangDung.hiep2.doiThu.length,
      }),
      () =>
        doiNoiDung({ hiep2: { ta: [...dangDung.hiep1.ta], doiThu: [...dangDung.hiep1.doiThu] } }),
    )

  /** Áp mẫu đã lưu: thay toàn bộ sơ đồ, bỏ qua cầu thủ không còn trong đội hình trận này. */
  const apMau = (mauId: string) => {
    const m = mauDs?.find((x) => x.id === mauId)
    if (!m) return

    const dangCo =
      dangDung.hiep1.ta.length + dangDung.hiep1.doiThu.length +
      dangDung.hiep2.ta.length + dangDung.hiep2.doiThu.length

    hoiTruocKhiGhiDe(
      dangCo,
      t('soDo.xacNhanApMau'),
      t('soDo.xacNhanApMauMoTa', { ten: m.ten, soNguoi: dangCo }),
      () => apMauNgay(m),
    )
  }

  const apMauNgay = (m: MauDoiHinhDto) => {
    const nd = docSoDo(m.noiDungJson)
    const coTrongTran = new Set(trenSan.map((c) => c.id))
    const locTa = (q: QuanTrenSan[]) => q.filter((x) => coTrongTran.has(x.id))

    setNhap({
      loaiSan: m.loaiSan as LoaiSan,
      mauTa: nd.mauTa,
      mauDoiThu: nd.mauDoiThu,
      hiep1: { ta: locTa(nd.hiep1.ta), doiThu: nd.hiep1.doiThu },
      hiep2: { ta: locTa(nd.hiep2.ta), doiThu: nd.hiep2.doiThu },
    })
    setMauChon(m.id)
  }

  const themQuanDoiThu = () => {
    const so = cuaHiep.doiThu.length + 1
    if (so > CAU_HINH_SAN[loaiSan].soNguoi) return
    doiHiep(hiep, {
      doiThu: [...cuaHiep.doiThu, { id: `dt-${so}-${Date.now()}`, x: 20 + ((so - 1) % 5) * 15, y: 30, so }],
    })
  }

  return (
    <div className="flex flex-col gap-4">
      {/* Chọn thành viên: gập lại khi đã có đội hình, vì việc chính ở màn này là xếp sân. */}
      <div className="rounded-lg border border-border">
        <button
          type="button"
          onClick={() => setMoChonNguoi((v) => !v)}
          className="flex w-full items-center justify-between px-3 py-2 text-sm font-medium hover:bg-muted/40"
        >
          <span className="flex items-center gap-2">
            <Users className="h-4 w-4" />
            {t('chiTiet.thanhVien')}
            <span className="text-muted-foreground">({trenSan.length})</span>
          </span>
          <ChevronDown
            className={cn('h-4 w-4 transition-transform', (moChonNguoi || !trenSan.length) && 'rotate-180')}
          />
        </button>

        {(moChonNguoi || trenSan.length === 0) && (
          <div className="flex flex-col gap-3 border-t border-border p-3">
            <SelectTimKiemNhieu
              id="thanhVien"
              luaChon={(cauThus ?? []).map((c) => ({
                giaTri: c.id,
                nhan: c.soAo ? `${c.soAo} · ${c.hoTen}` : c.hoTen,
              }))}
              giaTri={dangChon}
              onDoi={setChon}
              placeholder={t('chiTiet.chonThanhVien')}
              placeholderTimKiem={t('taiKhoan.timCauThu')}
            />
            <div className="flex items-center gap-3">
              <Button size="sm" onClick={() => luuDoiHinh.mutate()} disabled={luuDoiHinh.isPending}>
                <Save className="h-4 w-4" />
                {luuDoiHinh.isPending ? t('chung.dangTai') : t('chiTiet.luuDoiHinh')}
              </Button>
              <p className="text-xs text-muted-foreground">{t('chiTiet.doiHinhGoiY')}</p>
            </div>
          </div>
        )}
      </div>

      {trenSan.length === 0 ? (
        <TrangTrong thongDiep={t('soDo.chuaCoDoiHinh')} />
      ) : (
        <>
          {/* Hàng điều khiển trên cùng: loại sân · hiệp · mẫu. Ba thứ này đổi cả bàn cờ nên
              tách khỏi nhóm nút sơ đồ vốn chỉ tác động lên một bên một hiệp. */}
          <div className="flex flex-wrap items-center gap-x-4 gap-y-2 rounded-lg border border-border bg-muted/20 px-3 py-2">
            <div className="flex items-center gap-1.5">
              <span className="text-xs text-muted-foreground">{t('soDo.loaiSan')}</span>
              <div className="flex rounded-md border border-border">
                {CAC_LOAI_SAN.map((ls) => (
                  <button
                    key={ls}
                    type="button"
                    onClick={() => doiNoiDung({ loaiSan: ls })}
                    className={cn(
                      'px-2 py-1 text-xs first:rounded-l-md last:rounded-r-md',
                      loaiSan === ls
                        ? 'bg-primary font-medium text-primary-foreground'
                        : 'hover:bg-muted',
                    )}
                  >
                    {ls}-{ls}
                  </button>
                ))}
              </div>
            </div>

            <div className="flex items-center gap-1.5">
              <span className="text-xs text-muted-foreground">{t('soDo.hiep')}</span>
              <div className="flex rounded-md border border-border">
                {(['hiep1', 'hiep2'] as Hiep[]).map((h) => (
                  <button
                    key={h}
                    type="button"
                    onClick={() => setHiep(h)}
                    className={cn(
                      'px-2.5 py-1 text-xs first:rounded-l-md last:rounded-r-md',
                      hiep === h ? 'bg-primary font-medium text-primary-foreground' : 'hover:bg-muted',
                    )}
                  >
                    {t(`soDo.${h}`)}
                  </button>
                ))}
              </div>
              {hiep === 'hiep2' && (
                <Button variant="ghost" size="sm" onClick={chepSangHiep2}>
                  <Copy className="h-3.5 w-3.5" />
                  {t('soDo.chepHiep1')}
                </Button>
              )}
            </div>

            <div className="ml-auto flex items-center gap-2">
              <div className="w-52">
                <SelectTimKiem
                  id="mauDoiHinh"
                  luaChon={(mauDs ?? []).map((m) => ({
                    giaTri: m.id,
                    nhan: `${m.ten} (${m.loaiSan}-${m.loaiSan}, ${m.soCauThu})`,
                  }))}
                  giaTri={mauChon}
                  onDoi={(v) => v && apMau(v)}
                  placeholder={t('soDo.chonMau')}
                  placeholderTimKiem={t('soDo.timMau')}
                />
              </div>
              <Button variant="outline" size="sm" onClick={() => setMoLuuMau(true)}>
                <BookmarkPlus className="h-4 w-4" />
                {t('soDo.luuThanhMau')}
              </Button>
            </div>
          </div>

          {/* Chọn bên + sơ đồ dựng sẵn. Nút sơ đồ áp cho bên đang chọn, nên hai thứ đi liền nhau. */}
          <div className="flex flex-wrap items-center gap-2">
            <div className="flex rounded-md border border-border">
              {(['ta', 'doiThu'] as Ben[]).map((b) => (
                <button
                  key={b}
                  type="button"
                  onClick={() => setBen(b)}
                  className={cn(
                    'flex items-center gap-1.5 px-2.5 py-1 text-xs first:rounded-l-md last:rounded-r-md',
                    ben === b ? 'bg-primary font-medium text-primary-foreground' : 'hover:bg-muted',
                  )}
                >
                  <span
                    style={{ background: mauCua(b).nen }}
                    className="h-2.5 w-2.5 rounded-full border border-white/70"
                  />
                  {t(b === 'ta' ? 'soDo.doiNha' : 'soDo.doiKhach')}
                  <span className="opacity-70">({cuaHiep[b].length})</span>
                </button>
              ))}
            </div>

            {/* Bảng màu áo của BÊN đang chọn — đổi màu là việc thuộc về bên đó, đặt cạnh nút
                chọn bên để khỏi phải giải thích nó áp cho ai. */}
            <div className="flex items-center gap-1">
              <span className="text-xs text-muted-foreground">{t('soDo.mauAo')}</span>
              {mauChonDuoc.map((m) => {
                const dangDungMau = mauCua(ben).ma === m.ma
                return (
                  <button
                    key={m.ma}
                    type="button"
                    title={t(`soDo.mau.${m.ma}`)}
                    aria-label={t(`soDo.mau.${m.ma}`)}
                    aria-pressed={dangDungMau}
                    onClick={() =>
                      doiNoiDung(ben === 'ta' ? { mauTa: m.ma } : { mauDoiThu: m.ma })
                    }
                    style={{ background: m.nen }}
                    className={cn(
                      'h-5 w-5 rounded-full border transition-transform',
                      dangDungMau
                        ? 'scale-110 border-[hsl(var(--accent))] ring-2 ring-[hsl(var(--accent))]'
                        : 'border-border hover:scale-110',
                    )}
                  />
                )
              })}
              {boAoClb.length === 0 && (
                <button
                  type="button"
                  onClick={() => navigate('/quan-tri/thiet-lap')}
                  className="ml-1 text-xs text-muted-foreground underline hover:text-foreground"
                >
                  {t('soDo.khaiBoAo')}
                </button>
              )}
            </div>

            <span className="text-sm text-muted-foreground">{t('soDo.dungSan')}</span>
            {Object.keys(CAU_HINH_SAN[loaiSan].soDo).map((ten) => (
              <Button key={ten} variant="outline" size="sm" onClick={() => apSoDo(ten)}>
                {ten}
              </Button>
            ))}
            {ben === 'doiThu' && (
              <Button
                variant="outline"
                size="sm"
                onClick={themQuanDoiThu}
                disabled={cuaHiep.doiThu.length >= CAU_HINH_SAN[loaiSan].soNguoi}
              >
                <Plus className="h-4 w-4" />
                {t('soDo.themQuan')}
              </Button>
            )}
            <Button
              variant="ghost"
              size="sm"
              onClick={() =>
                hoiTruocKhiGhiDe(
                  cuaHiep[ben].length,
                  t('soDo.xacNhanXoaHet'),
                  t('soDo.xacNhanXoaHetMoTa', {
                    soNguoi: cuaHiep[ben].length,
                    ben: t(ben === 'ta' ? 'soDo.doiNha' : 'soDo.doiKhach'),
                    hiep: t(`soDo.${hiep}`),
                  }),
                  () => doiHiep(hiep, ben === 'ta' ? { ta: [] } : { doiThu: [] }),
                )
              }
            >
              {t('soDo.xoaHet')}
            </Button>
          </div>

          <SoDoSan
            loaiSan={loaiSan}
            cauThus={trenSan}
            quanTa={cuaHiep.ta}
            quanDoiThu={cuaHiep.doiThu}
            onDoiTa={(q) => doiHiep(hiep, { ta: q })}
            onDoiDoiThu={(q) => doiHiep(hiep, { doiThu: q })}
            mauTa={dangDung.mauTa}
            mauDoiThu={dangDung.mauDoiThu}
            benDangChon={ben}
            onChonBen={setBen}
          />

          <div className="flex max-w-2xl flex-col gap-1.5">
            <Label htmlFor="ghiChuChienThuat">{t('chiTiet.ghiChuChienThuat')}</Label>
            <Input
              id="ghiChuChienThuat"
              value={gc}
              onChange={(e) => setGhiChu(e.target.value)}
              placeholder="VD: Phòng ngự phản công, ép biên phải"
            />
          </div>

          <div className="flex items-center gap-3">
            <Button onClick={() => luuSoDo.mutate(dangDung)} disabled={luuSoDo.isPending}>
              <Save className="h-4 w-4" />
              {luuSoDo.isPending ? t('chung.dangTai') : t('chiTiet.luuSoDo')}
            </Button>
            {daLuuSoDo && <span className="text-sm text-status-win">{t('chiTiet.daLuu')}</span>}
          </div>
        </>
      )}

      <HopXacNhan
        mo={choXacNhan !== null}
        tieuDe={choXacNhan?.tieuDe ?? ''}
        thongDiep={choXacNhan?.thongDiep ?? ''}
        onDongY={() => {
          choXacNhan?.lam()
          setChoXacNhan(null)
        }}
        onHuy={() => setChoXacNhan(null)}
      />

      <Modal mo={moLuuMau} onDong={() => setMoLuuMau(false)} tieuDe={t('soDo.luuThanhMau')}>
        <form
          onSubmit={(e) => {
            e.preventDefault()
            const fd = new FormData(e.currentTarget)
            luuMau.mutate(String(fd.get('tenMau')))
          }}
          className="flex flex-col gap-4"
        >
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="tenMau">{t('soDo.tenMau')}</Label>
            <Input id="tenMau" name="tenMau" required autoFocus placeholder="VD: Đội hình mạnh nhất" />
            <p className="text-xs text-muted-foreground">{t('soDo.luuMauGoiY')}</p>
          </div>
          <ModalChan>
            <Button type="button" variant="outline" onClick={() => setMoLuuMau(false)}>
              {t('chung.huy')}
            </Button>
            <Button type="submit" disabled={luuMau.isPending}>
              {luuMau.isPending ? t('chung.dangTai') : t('chung.luu')}
            </Button>
          </ModalChan>
        </form>
      </Modal>
    </div>
  )
}

/**
 * Tab (c) — đánh giá sau trận: bảng cầu thủ, sửa từng người trong modal.
 *
 * **Bàn thắng nằm ở tab này chứ không ở Thông tin chung**: tỷ số đội nhà bằng tổng bàn cầu
 * thủ ghi, nên chỗ nhập bàn thắng phải là chỗ ghi nhận ai ghi. Nhập hai nơi thì hai con số
 * lệch nhau và không biết tin bên nào — backend cũng đã bỏ `TySoNha` khỏi lệnh cập nhật trận.
 *
 * **Bảng + modal** thay cho các thẻ gập được: 11 thẻ gập cho một cái nhìn tổng quan tệ (phải
 * mở từng cái mới thấy điểm), còn mở nhiều thẻ cùng lúc thì trang dài lê thê. Bảng cho thấy
 * toàn đội trong một màn hình, modal lo phần nhập liệu chi tiết.
 *
 * Vote MVP **không** nằm trong modal: mỗi người một phiếu (quy tắc #8) và gửi ngay khi bấm,
 * không chờ Lưu — để trong modal thì người dùng tưởng phải bấm Lưu mới tính.
 */
function TabDanhGia({ tranDauId, onLoi }: { tranDauId: string; onLoi: (m: string | null) => void }) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [daLuu, setDaLuu] = useState(false)
  const [dangSua, setDangSua] = useState<DanhGiaDto | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['danh-gia', tranDauId],
    queryFn: async () => (await api.get<DanhGiaDto[]>(`/tran-dau/${tranDauId}/danh-gia`)).data,
  })

  /**
   * Lưu MỘT cầu thủ.
   *
   * Handler backend cố tình không xoá đánh giá của người vắng mặt trong payload, nên gửi một
   * người là an toàn — và tỷ số vẫn cộng đúng vì nó tính trên toàn bộ đánh giá của trận.
   */
  const luu = useMutation({
    mutationFn: async (dg: {
      cauThuId: string
      soBanGhiDuoc: number
      soBanCuuThua: number
      chiSoKyNang: string | null
      ghiChu: string | null
    }) => api.put(`/tran-dau/${tranDauId}/danh-gia`, { tranDauId, danhGias: [dg] }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['danh-gia', tranDauId] })
      // Tỷ số trận đổi theo bàn thắng vừa nhập — không nạp lại thì đầu trang hiện số cũ.
      void qc.invalidateQueries({ queryKey: ['tran-dau'] })
      onLoi(null)
      setDangSua(null)
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

  const tongBanThang = data.reduce((s, d) => s + d.soBanGhiDuoc, 0)
  const tongCuuThua = data.reduce((s, d) => s + d.soBanCuuThua, 0)

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center gap-4 rounded-lg border border-border bg-muted/30 px-4 py-3">
        <div className="flex items-baseline gap-2">
          <span className="text-sm text-muted-foreground">{t('chiTiet.tongBanThang')}</span>
          <span className="text-2xl font-bold tabular-nums text-primary">{tongBanThang}</span>
        </div>
        <div className="flex items-baseline gap-2">
          <span className="text-sm text-muted-foreground">{t('chiTiet.tongCuuThua')}</span>
          <span className="text-lg font-semibold tabular-nums">{tongCuuThua}</span>
        </div>
        <p className="ml-auto max-w-md text-xs text-muted-foreground">
          {t('chiTiet.tongBanThangGoiY')}
        </p>
        {daLuu && <span className="text-sm text-status-win">{t('chiTiet.daLuu')}</span>}
      </div>

      <Table>
        <thead>
          <tr>
            <Th>{t('cauThu.hoTen')}</Th>
            <Th className="w-24">{t('chiTiet.banThang')}</Th>
            <Th className="w-24">{t('chiTiet.cuuThua')}</Th>
            <Th className="w-28">{t('chiSo.trungBinh')}</Th>
            <Th>{t('cauThu.ghiChu')}</Th>
            <Th className="w-24 text-center">{t('chiTiet.mvp')}</Th>
            <Th className="w-16" />
          </tr>
        </thead>
        <tbody>
          {data.map((d) => {
            const chiSo = docChiSo(d.chiSoKyNang)
            const tb = diemTrungBinh(chiSo)
            return (
              <tr key={d.cauThuId} className="hover:bg-muted/40">
                <Td className="font-medium">{d.hoTen}</Td>
                <Td>
                  {d.soBanGhiDuoc > 0 ? (
                    <Badge variant="win">{d.soBanGhiDuoc}</Badge>
                  ) : (
                    <span className="text-muted-foreground">—</span>
                  )}
                </Td>
                <Td>
                  {d.soBanCuuThua > 0 ? (
                    <Badge variant="accent">{d.soBanCuuThua}</Badge>
                  ) : (
                    <span className="text-muted-foreground">—</span>
                  )}
                </Td>
                <Td>
                  {/* Điểm kèm thanh mức: đọc lướt cả cột thấy ngay ai chơi tốt, không phải
                      so từng con số. */}
                  {tb !== null ? (
                    <span className="flex items-center gap-1.5">
                      <span className="font-semibold tabular-nums">{tb}</span>
                      <span className="h-1.5 w-12 overflow-hidden rounded-full bg-muted">
                        <span
                          className="block h-full rounded-full bg-primary"
                          style={{ width: `${tb * 10}%` }}
                        />
                      </span>
                    </span>
                  ) : (
                    <span className="text-muted-foreground">{t('chiSo.chuaCham')}</span>
                  )}
                </Td>
                <Td className="max-w-56 truncate text-muted-foreground" title={d.ghiChu ?? ''}>
                  {d.ghiChu || '—'}
                </Td>
                <Td>
                  {/*
                    Nút tim là thao tác RIÊNG, không nằm trong modal: mỗi người chỉ một phiếu
                    (quy tắc #8) nên nó gửi ngay, không chờ bấm Lưu.
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
                          d.toiDaVote ? 'fill-status-lose text-status-lose' : 'text-muted-foreground',
                        )}
                      />
                    </button>
                    <span className="min-w-4 text-sm font-medium tabular-nums">{d.soPhieuMvp}</span>
                  </div>
                </Td>
                <Td>
                  <Button
                    variant="ghost"
                    size="sm"
                    title={t('chung.sua')}
                    onClick={() => setDangSua(d)}
                  >
                    <Pencil className="h-3.5 w-3.5" />
                  </Button>
                </Td>
              </tr>
            )
          })}
        </tbody>
      </Table>

      <p className="flex items-center gap-1.5 text-xs text-muted-foreground">
        <Users className="h-3.5 w-3.5" />
        {t('chiTiet.mvpGoiY')}
      </p>

      {dangSua && (
        <ModalDanhGia
          danhGia={dangSua}
          dangLuu={luu.isPending}
          onLuu={(v) => luu.mutate({ cauThuId: dangSua.cauThuId, ...v })}
          onDong={() => setDangSua(null)}
        />
      )}
    </div>
  )
}

/**
 * Modal chấm điểm một cầu thủ: bàn thắng, cứu thua, 6 chỉ số kỹ năng, ghi chú.
 *
 * Nhận `danhGia` làm giá trị KHỞI TẠO rồi giữ nháp cục bộ — không đọc thẳng từ props mỗi lần
 * render, vì query nền làm mới sẽ ghi đè thứ người dùng đang gõ dở.
 */
function ModalDanhGia({
  danhGia,
  dangLuu,
  onLuu,
  onDong,
}: {
  danhGia: DanhGiaDto
  dangLuu: boolean
  onLuu: (v: {
    soBanGhiDuoc: number
    soBanCuuThua: number
    chiSoKyNang: string | null
    ghiChu: string | null
  }) => void
  onDong: () => void
}) {
  const { t } = useTranslation()
  const [ghi, setGhi] = useState(danhGia.soBanGhiDuoc)
  const [cuu, setCuu] = useState(danhGia.soBanCuuThua)
  const [chiSo, setChiSo] = useState<BoChiSo>(() => docChiSo(danhGia.chiSoKyNang))
  const [ghiChu, setGhiChu] = useState(danhGia.ghiChu ?? '')

  return (
    <Modal mo onDong={onDong} chanDoiKhiXuLy={dangLuu} tieuDe={danhGia.hoTen} rong="lg">
      <form
        onSubmit={(e) => {
          e.preventDefault()
          // Mọi trường lệnh cập nhật ghi đè đều gửi lại (quy tắc #1) — thiếu một cái là mất
          // dữ liệu trường đó.
          onLuu({
            soBanGhiDuoc: ghi || 0,
            soBanCuuThua: cuu || 0,
            chiSoKyNang: ghiChiSo(chiSo),
            ghiChu: ghiChu || null,
          })
        }}
        className="flex flex-col gap-4"
      >
        <div className="flex flex-wrap gap-4">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="dgGhi">{t('chiTiet.banThang')}</Label>
            <Input
              id="dgGhi"
              type="number"
              min={0}
              autoFocus
              value={ghi}
              onChange={(e) => setGhi(Number(e.target.value) || 0)}
              className="w-24"
            />
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="dgCuu">{t('chiTiet.cuuThua')}</Label>
            <Input
              id="dgCuu"
              type="number"
              min={0}
              value={cuu}
              onChange={(e) => setCuu(Number(e.target.value) || 0)}
              className="w-24"
            />
          </div>
        </div>

        <div className="flex flex-col gap-4 sm:flex-row sm:items-start">
          <div className="min-w-0 flex-1">
            <Label className="mb-2 block">{t('chiSo.tieuDe')}</Label>
            <ChamChiSo giaTri={chiSo} onDoi={setChiSo} />
          </div>
          <div className="shrink-0 self-center sm:self-start">
            <RadarChiSo giaTri={chiSo} kichThuoc={140} />
          </div>
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="dgGhiChu">{t('cauThu.ghiChu')}</Label>
          <Textarea
            id="dgGhiChu"
            value={ghiChu}
            onChange={(e) => setGhiChu(e.target.value)}
            placeholder={t('chiTiet.ghiChuCauThuGoiY')}
          />
        </div>

        <ModalChan>
          <Button type="button" variant="outline" onClick={onDong} disabled={dangLuu}>
            {t('chung.huy')}
          </Button>
          <Button type="submit" disabled={dangLuu}>
            <Save className="h-4 w-4" />
            {dangLuu ? t('chung.dangTai') : t('chung.luu')}
          </Button>
        </ModalChan>
      </form>
    </Modal>
  )
}
