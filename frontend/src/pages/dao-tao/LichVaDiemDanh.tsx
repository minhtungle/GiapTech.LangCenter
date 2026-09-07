import { lazy, Suspense, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  CalendarDays, CalendarPlus, CalendarRange, CheckCircle2, ClipboardCheck, List, Plus,
  RotateCcw, Trash2, X,
} from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Input, Label, Table, Td, Th, TrangTrong,
} from '@/components/ui'
import { Modal } from '@/components/ui/Modal'
import { KhungNoiDung } from '@/components/ui/KhungNoiDung'
import { HopXacNhan } from '@/components/ui/HopXacNhan'
import { MenuThaoTac } from '@/components/ui/MenuThaoTac'
import { useXacNhan } from '@/lib/xacNhan'
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

/**
 * Lịch tải theo yêu cầu: FullCalendar nặng ~200 kB và chỉ dùng khi người dùng chủ động bật
 * chế độ Lịch. Import thẳng thì mọi người mở bất kỳ màn nào cũng phải tải nó.
 */
const LichBuoiHoc = lazy(() =>
  import('@/components/ui/LichBuoiHoc').then((m) => ({ default: m.LichBuoiHoc })),
)

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
  nhung,
}: {
  lopHocId: string
  tenLop: string
  onDong: () => void
  /** true = đang là tab trong view chi tiết lớp, không bọc Modal. */
  nhung?: boolean
}) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [maLoi, setMaLoi] = useState<string | null>(null)
  // Mặc định BẢNG: nó là chỗ điểm danh và xem số liệu từng buổi. Lịch để nhìn tổng quát.
  const [kieuXem, setKieuXem] = useState<'bang' | 'lich'>('bang')
  const [moSinhLich, setMoSinhLich] = useState(false)
  const [moThemBuoi, setMoThemBuoi] = useState(false)
  const [moSinhThem, setMoSinhThem] = useState(false)
  const [xacNhanSinhLai, setXacNhanSinhLai] = useState(false)
  const [huyCho, setHuyCho] = useState<BuoiHocDto | null>(null)
  const [xoaCho, setXoaCho] = useState<BuoiHocDto | null>(null)
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
    onSuccess: () => {
      lamMoi()
      setHuyCho(null)
    },
    onError: (e) => {
      setMaLoi(layMaLoi(e))
      setHuyCho(null)
    },
  })

  const xoaBuoi = useMutation({
    mutationFn: (id: string) => api.delete(`/buoi-hoc/${id}`),
    onSuccess: () => {
      lamMoi()
      setXoaCho(null)
    },
    onError: (e) => {
      setMaLoi(layMaLoi(e))
      setXoaCho(null)
    },
  })

  const soDaChot = buoiHocs.filter((b) => b.trangThai === 'DaHoanThanh').length

  return (
    <KhungNoiDung nhung={nhung} onDong={onDong} tieuDe={`${t('buoiHoc.lich')} — ${tenLop}`}>
      <div className="grid gap-4">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div className="flex flex-wrap items-center gap-3">
            {buoiHocs.length > 0 && (
              <div className="flex gap-1 rounded-lg border border-border p-1">
                {(['bang', 'lich'] as const).map((x) => (
                  <button
                    key={x}
                    type="button"
                    onClick={() => setKieuXem(x)}
                    className={
                      'flex items-center gap-1.5 rounded-md px-2.5 py-1 text-xs font-medium ' +
                      'transition-colors ' +
                      (kieuXem === x
                        ? 'bg-primary text-primary-foreground'
                        : 'text-muted-foreground hover:bg-muted')
                    }
                  >
                    {x === 'bang' ? (
                      <List className="h-3.5 w-3.5" />
                    ) : (
                      <CalendarRange className="h-3.5 w-3.5" />
                    )}
                    {t(x === 'bang' ? 'lich.kieuBang' : 'lich.kieuLich')}
                  </button>
                ))}
              </div>
            )}

            <p className="text-sm text-muted-foreground">
              {buoiHocs.length > 0 && t('buoiHoc.daSinh', { soLuong: buoiHocs.length })}
              {soDaChot > 0 && ` · ${t('buoiHoc.daChot', { soLuong: soDaChot })}`}
            </p>
          </div>

          {/* Ba nút, ba việc khác nhau — trước đây chỉ có một nút vừa sinh vừa xoá.
              "Sinh lại" để cuối và viền đỏ vì nó là nút duy nhất xoá dữ liệu. */}
          <div className="flex flex-wrap gap-2">
            {buoiHocs.length === 0 ? (
              <Button size="sm" onClick={() => setMoSinhLich(true)}>
                <CalendarDays className="mr-1.5 h-4 w-4" />
                {t('buoiHoc.sinhLich')}
              </Button>
            ) : (
              <>
                <Button size="sm" onClick={() => setMoThemBuoi(true)}>
                  <Plus className="mr-1.5 h-4 w-4" />
                  {t('buoiHoc.themBuoi')}
                </Button>
                <Button size="sm" variant="outline" onClick={() => setMoSinhThem(true)}>
                  <CalendarPlus className="mr-1.5 h-4 w-4" />
                  {t('buoiHoc.sinhThemBuoi')}
                </Button>
                <Button
                  size="sm"
                  variant="outline"
                  className="border-destructive/40 text-destructive hover:bg-destructive/10"
                  onClick={() => setXacNhanSinhLai(true)}
                >
                  <RotateCcw className="mr-1.5 h-4 w-4" />
                  {t('buoiHoc.sinhLaiLich')}
                </Button>
              </>
            )}
          </div>
        </div>

        {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

        {isLoading ? (
          <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
        ) : buoiHocs.length === 0 ? (
          <TrangTrong thongDiep={t('buoiHoc.chuaCoLich')} />
        ) : kieuXem === 'lich' ? (
          <Suspense
            fallback={
              <p className="py-8 text-center text-sm text-muted-foreground">
                {t('chung.dangTai')}
              </p>
            }
          >
            <LichBuoiHoc
              buoi={buoiHocs}
              // Bấm buổi trên lịch mở ngay bảng điểm danh — việc hay làm nhất với một buổi.
              onChonBuoi={(id) => {
                const b = buoiHocs.find((x) => x.id === id)
                if (b) setBuoiDiemDanh(b)
              }}
            />
          </Suspense>
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
                      <div className="flex justify-end">
                        <MenuThaoTac
                          nhanMo={t('chung.thaoTac')}
                          muc={[
                            {
                              nhan: t('buoiHoc.diemDanh'),
                              icon: ClipboardCheck,
                              onChon: () => setBuoiDiemDanh(b),
                            },
                            {
                              nhan: t('buoiHoc.huyBuoi'),
                              icon: X,
                              nguyHiem: true,
                              ngatNhom: true,
                              // Buổi đã chốt là bằng chứng chuyên cần; buổi đã huỷ thì huỷ nữa
                              // cũng vô nghĩa. Backend chặn cả hai, đây chỉ là ẩn cho gọn.
                              an: b.trangThai !== 'DaLenLich',
                              onChon: () => setHuyCho(b),
                            },
                            {
                              nhan: t('chung.xoa'),
                              icon: Trash2,
                              nguyHiem: true,
                              an: b.trangThai === 'DaHoanThanh',
                              onChon: () => setXoaCho(b),
                            },
                          ]}
                        />
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

      {moSinhThem && (
        <FormSinhLich
          noiTiep
          lopHocId={lopHocId}
          daCoLich={buoiHocs.length > 0}
          onXong={() => {
            setMoSinhThem(false)
            lamMoi()
          }}
          onDong={() => setMoSinhThem(false)}
        />
      )}

      {moThemBuoi && (
        <FormThemBuoi
          lopHocId={lopHocId}
          onXong={() => {
            setMoThemBuoi(false)
            lamMoi()
          }}
          onDong={() => setMoThemBuoi(false)}
        />
      )}

      {/* Ba hộp xác nhận RIÊNG, mỗi cái nói đúng hậu quả của nó — hộp chung chung
          "Bạn chắc chắn?" không giúp người dùng phân biệt xoá 1 buổi với xoá cả lịch. */}
      <HopXacNhan
        mo={xacNhanSinhLai}
        tieuDe={t('buoiHoc.sinhLaiLich')}
        thongDiep={t('buoiHoc.xacNhanSinhLai', {
          soLuong: buoiHocs.length - soDaChot,
          daChot: soDaChot,
        })}
        nhanDongY={t('buoiHoc.sinhLaiLich')}
        onHuy={() => setXacNhanSinhLai(false)}
        onDongY={() => {
          setXacNhanSinhLai(false)
          setMoSinhLich(true)
        }}
      />

      <HopXacNhan
        mo={huyCho !== null}
        tieuDe={t('buoiHoc.huyBuoi')}
        thongDiep={
          huyCho
            ? t('buoiHoc.xacNhanHuyBuoi', { thuTu: huyCho.thuTu, gio: gioVN(huyCho.batDau) })
            : ''
        }
        nhanDongY={t('buoiHoc.huyBuoi')}
        onHuy={() => setHuyCho(null)}
        onDongY={() => huyCho && huyBuoi.mutate(huyCho.id)}
      />

      <HopXacNhan
        mo={xoaCho !== null}
        tieuDe={t('buoiHoc.xoaBuoi')}
        thongDiep={
          xoaCho
            ? t('buoiHoc.xacNhanXoaBuoi', { thuTu: xoaCho.thuTu, gio: gioVN(xoaCho.batDau) })
            : ''
        }
        nhanDongY={t('chung.xoa')}
        onHuy={() => setXoaCho(null)}
        onDongY={() => xoaCho && xoaBuoi.mutate(xoaCho.id)}
      />

      {buoiDiemDanh && (
        <BangDiemDanh
          buoi={buoiDiemDanh}
          onDong={() => setBuoiDiemDanh(null)}
          onXong={lamMoi}
        />
      )}
    </KhungNoiDung>
  )
}

/** Bước 2 wizard: khai tần suất, hệ thống sinh danh sách buổi. */
/**
 * Form sinh lịch — dùng cho CẢ hai việc:
 * - `theTheLich` (mặc định): thay cả lịch, xoá buổi chưa học.
 * - `noiTiep`: sinh thêm nối tiếp, **không xoá gì**.
 *
 * Cùng một form vì đầu vào giống hệt (tần suất, giờ, điều kiện dừng); chỉ khác endpoint và
 * lời cảnh báo. Viết hai form gần giống nhau là mời gọi chúng lệch nhau về sau.
 */
function FormSinhLich({
  lopHocId,
  daCoLich,
  noiTiep,
  onXong,
  onDong,
}: {
  lopHocId: string
  daCoLich: boolean
  /** true = gọi `sinh-them-buoi` thay vì `sinh-lich`. */
  noiTiep?: boolean
  onXong: () => void
  onDong: () => void
}) {
  const { t } = useTranslation()
  const { hoi, hop } = useXacNhan()
  const [thuChon, setThuChon] = useState<number[]>([2, 4, 6])
  const [ketThucTheo, setKetThucTheo] = useState<'soBuoi' | 'ngay'>('soBuoi')
  const [maLoi, setMaLoi] = useState<string | null>(null)

  const sinh = useMutation({
    mutationFn: (body: Record<string, unknown>) =>
      api.post(
        noiTiep ? `/lop-hoc/${lopHocId}/sinh-them-buoi` : `/lop-hoc/${lopHocId}/sinh-lich`,
        body,
      ),
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

    // Endpoint nối tiếp nhận `tuNgay`, endpoint thay lịch nhận `ngayKhaiGiang`.
    const ngay = String(fd.get('ngayKhaiGiang'))

    hoi({
      tieuDe: noiTiep ? t('buoiHoc.sinhThemBuoi') : t('buoiHoc.sinhLich'),
      thongDiep: noiTiep
        ? t('buoiHoc.hoiSinhThem')
        : daCoLich
          ? t('buoiHoc.hoiSinhLai')
          : t('buoiHoc.hoiSinhLich'),
      nguyHiem: !noiTiep && daCoLich,
      onDongY: () => {
        sinh.mutate({
          ...(noiTiep ? { tuNgay: ngay } : { ngayKhaiGiang: ngay }),
          // Backend dùng DayOfWeek: CN=0, T2=1… trùng với mã đang lưu.
          thuTrongTuan: thuChon,
          gioBatDau: String(fd.get('gioBatDau')) + ':00',
          gioKetThuc: String(fd.get('gioKetThuc')) + ':00',
          soBuoi: ketThucTheo === 'soBuoi' ? Number(fd.get('soBuoi')) : null,
          denNgay: ketThucTheo === 'ngay' ? String(fd.get('denNgay')) : null,
          ngayLoaiTru: loaiTruTho,
        })
      },
    })
  }

  return (
    <Modal
      mo
      onDong={onDong}
      tieuDe={noiTiep ? t('buoiHoc.sinhThemBuoi') : t('buoiHoc.sinhLich')}
    >
      <form onSubmit={onSubmit} className="grid gap-4 sm:grid-cols-2">
        {daCoLich && !noiTiep && (
          <div className="sm:col-span-2">
            <CanhBaoLoi>{t('buoiHoc.canhBaoSinhLai')}</CanhBaoLoi>
          </div>
        )}

        {noiTiep && (
          <p className="text-sm text-muted-foreground sm:col-span-2">
            {t('buoiHoc.goiYSinhThem')}
          </p>
        )}

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="ngayKhaiGiang">
            {noiTiep ? t('buoiHoc.tuNgay') : t('lopHoc.ngayKhaiGiang')}
          </Label>
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
      {hop}
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
  const { hoi, hop } = useXacNhan()
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
          <Button
            variant="outline"
            disabled={chot.isPending}
            onClick={() =>
              hoi({
                tieuDe: t('buoiHoc.chotBuoi'),
                thongDiep: t('buoiHoc.hoiChotBuoi'),
                nguyHiem: true,
                onDongY: () => chot.mutate(),
              })
            }
          >
            {t('buoiHoc.chotBuoi')}
          </Button>
          <Button
            disabled={luu.isPending || ds.length === 0}
            onClick={() =>
              hoi({
                tieuDe: t('chung.xacNhanLuu'),
                thongDiep: t('diemDanh.hoiLuu', { soLuong: ds.length }),
                onDongY: () => luu.mutate(),
              })
            }
          >
            {t('diemDanh.luu')}
          </Button>
        </div>
      </div>
      {hop}
    </Modal>
  )
}

/** Thêm một buổi lẻ — dạy bù, ôn tập. Không đụng buổi nào đang có. */
function FormThemBuoi({
  lopHocId,
  onXong,
  onDong,
}: {
  lopHocId: string
  onXong: () => void
  onDong: () => void
}) {
  const { t } = useTranslation()
  const { hoi, hop } = useXacNhan()
  const [laHocBu, setLaHocBu] = useState(true)
  const [maLoi, setMaLoi] = useState<string | null>(null)

  const them = useMutation({
    mutationFn: (body: Record<string, unknown>) =>
      api.post(`/lop-hoc/${lopHocId}/buoi-hoc`, body),
    onSuccess: onXong,
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  return (
    <Modal mo onDong={onDong} tieuDe={t('buoiHoc.themBuoi')} rong="md">
      <form
        onSubmit={(e) => {
          e.preventDefault()
          const fd = new FormData(e.currentTarget)
          const ngay = String(fd.get('ngay'))
          hoi({
            tieuDe: t('buoiHoc.themBuoi'),
            thongDiep: t('buoiHoc.hoiThemBuoi', { ngay }),
            onDongY: () => {
              them.mutate({
                ngay: String(fd.get('ngay')),
                gioBatDau: String(fd.get('gioBatDau')) + ':00',
                gioKetThuc: String(fd.get('gioKetThuc')) + ':00',
                laHocBu,
                phongHoc: (fd.get('phongHoc') as string) || null,
                ghiChu: (fd.get('ghiChu') as string) || null,
              })
            },
          })
        }}
        className="grid gap-4 sm:grid-cols-2"
      >
        <div className="flex flex-col gap-1.5 sm:col-span-2">
          <Label htmlFor="ngay">{t('buoiHoc.ngayHoc')}</Label>
          <Input id="ngay" name="ngay" type="date" required />
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
          <Label htmlFor="phongHoc">{t('lopHoc.phongHoc')}</Label>
          <Input id="phongHoc" name="phongHoc" placeholder={t('buoiHoc.macDinhTheoLop')} />
        </div>

        <label className="flex items-center gap-2 self-end pb-2 text-sm">
          <input
            type="checkbox"
            checked={laHocBu}
            onChange={(e) => setLaHocBu(e.target.checked)}
            className="h-4 w-4 rounded border-input"
          />
          {t('buoiHoc.laHocBu')}
        </label>

        <div className="flex flex-col gap-1.5 sm:col-span-2">
          <Label htmlFor="ghiChu">{t('lopHoc.ghiChu')}</Label>
          <Input id="ghiChu" name="ghiChu" />
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
          <Button type="submit" disabled={them.isPending}>
            {t('chung.them')}
          </Button>
        </div>
      </form>
      {hop}
    </Modal>
  )
}
