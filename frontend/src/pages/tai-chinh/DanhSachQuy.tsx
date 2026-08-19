import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Check, ClipboardCopy, Pencil, Plus, Trash2, Undo2, Users } from 'lucide-react'
import { api, layMaLoi, trangRong, type KetQuaTrang } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Input, Label, Table, Td, Th, Textarea, TrangTrong,
} from '@/components/ui'
import { Anh } from '@/components/ui/Anh'
import { Modal, ModalChan } from '@/components/ui/Modal'
import { PhanTrang } from '@/components/ui/PhanTrang'
import { SelectTimKiemNhieu } from '@/components/ui/SelectTimKiem'
import { HopXacNhan } from '@/components/ui/HopXacNhan'
import { cn } from '@/lib/utils'
import { tienVnd } from './TaiChinh'

type TrangThaiQuy = 'DangMo' | 'DaDong'

export interface QuyDto {
  id: string
  tenQuy: string
  thoiHan: string | null
  ghiChu: string | null
  trangThai: TrangThaiQuy
  tongCanThu: number
  tongDaThu: number
  soNguoi: number
  soNguoiDaDongDu: number
  quaHan: boolean
  hienThongTinChuyenKhoan: boolean
}
interface DongGopDto {
  id: string
  cauThuId: string
  hoTen: string
  soTienCanDong: number
  soTienDaDong: number
  ngayDong: string | null
  ghiChu: string | null
  daDongDu: boolean
}
interface ThongTinChuyenKhoan {
  soTaiKhoan: string | null
  tenNganHang: string | null
  chuTaiKhoan: string | null
  anhQrUrl: string | null
}
interface ChiTietQuyDto {
  quy: QuyDto
  dongGops: DongGopDto[]
  /** null = đợt quỹ tắt hiển thị, HOẶC CLB chưa khai gì. Backend gộp hai ca này. */
  chuyenKhoan: ThongTinChuyenKhoan | null
}
interface CauThuNgan {
  id: string
  hoTen: string
}

/**
 * FR-15 — danh sách đợt quỹ với tiến độ thu.
 *
 * Màu theo quy ước chung (docs/frontend/ui-ux-nguyen-tac.md): **xanh** = thu đủ, **đỏ** =
 * quá hạn còn nợ, **vàng** = đang chờ.
 */
export default function DanhSachQuy() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [trang, setTrang] = useState(1)
  const [soDong, setSoDong] = useState(20)
  const [moForm, setMoForm] = useState(false)
  const [dangSua, setDangSua] = useState<QuyDto | null>(null)
  const [moChiTiet, setMoChiTiet] = useState<QuyDto | null>(null)
  const [xoa, setXoa] = useState<QuyDto | null>(null)
  const [maLoi, setMaLoi] = useState<string | null>(null)

  const { data: ketQua, isLoading } = useQuery({
    queryKey: ['quy', trang, soDong],
    queryFn: async () =>
      (await api.get<KetQuaTrang<QuyDto>>('/quy', { params: { trang, soDong } })).data,
  })

  const kq = ketQua ?? trangRong<QuyDto>()

  const xoaMut = useMutation({
    mutationFn: async (id: string) => api.delete(`/quy/${id}`),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['quy'] })
      void qc.invalidateQueries({ queryKey: ['tai-chinh-tong-quan'] })
      setXoa(null)
      setMaLoi(null)
    },
    onError: (e) => {
      setMaLoi(layMaLoi(e))
      setXoa(null)
    },
  })

  return (
    <div className="flex flex-col gap-4">
      <div className="flex justify-end">
        <Button
          onClick={() => {
            setDangSua(null)
            setMoForm(true)
          }}
        >
          <Plus className="h-4 w-4" />
          {t('taiChinh.themQuy')}
        </Button>
      </div>

      {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      {isLoading ? (
        <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
      ) : !kq.duLieu.length ? (
        <TrangTrong
          thongDiep={t('taiChinh.chuaCoQuy')}
          hanhDong={
            <Button onClick={() => setMoForm(true)}>
              <Plus className="h-4 w-4" />
              {t('taiChinh.themQuy')}
            </Button>
          }
        />
      ) : (
        <Table>
          <thead>
            <tr>
              <Th>{t('taiChinh.tenQuy')}</Th>
              <Th className="w-32">{t('taiChinh.thoiHan')}</Th>
              <Th className="w-64">{t('taiChinh.tienDo')}</Th>
              <Th className="w-28">{t('taiChinh.trangThai')}</Th>
              <Th className="w-28" />
            </tr>
          </thead>
          <tbody>
            {kq.duLieu.map((q) => (
              <tr key={q.id} className="hover:bg-muted/40">
                <Td>
                  <button
                    type="button"
                    onClick={() => setMoChiTiet(q)}
                    className="font-medium hover:text-primary hover:underline"
                  >
                    {q.tenQuy}
                  </button>
                  {q.ghiChu && (
                    <span className="block text-xs text-muted-foreground">{q.ghiChu}</span>
                  )}
                </Td>
                <Td className="whitespace-nowrap text-muted-foreground">
                  {q.thoiHan ? new Date(q.thoiHan).toLocaleDateString('vi-VN') : '—'}
                </Td>
                <Td>
                  <ThanhTienDo quy={q} />
                </Td>
                <Td>
                  {q.trangThai === 'DaDong' ? (
                    <Badge variant="muted">{t('taiChinh.tt.DaDong')}</Badge>
                  ) : q.quaHan ? (
                    <Badge variant="lose">{t('taiChinh.quaHan')}</Badge>
                  ) : q.soNguoi > 0 && q.soNguoiDaDongDu === q.soNguoi ? (
                    <Badge variant="win">{t('taiChinh.duTien')}</Badge>
                  ) : (
                    <Badge variant="draw">{t('taiChinh.dangThu')}</Badge>
                  )}
                </Td>
                <Td>
                  <div className="flex gap-1">
                    <Button
                      variant="ghost"
                      size="sm"
                      title={t('taiChinh.thuTien')}
                      onClick={() => setMoChiTiet(q)}
                    >
                      <Users className="h-3.5 w-3.5" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      title={t('chung.sua')}
                      onClick={() => {
                        setDangSua(q)
                        setMoForm(true)
                      }}
                    >
                      <Pencil className="h-3.5 w-3.5" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      title={t('chung.xoa')}
                      onClick={() => {
                        setMaLoi(null)
                        setXoa(q)
                      }}
                    >
                      <Trash2 className="h-3.5 w-3.5 text-destructive" />
                    </Button>
                  </div>
                </Td>
              </tr>
            ))}
          </tbody>
        </Table>
      )}

      {kq.duLieu.length > 0 && (
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

      {moForm && (
        <FormQuy
          quy={dangSua}
          onDong={() => {
            setMoForm(false)
            setDangSua(null)
          }}
          qc={qc}
        />
      )}
      {moChiTiet && (
        <ModalThuTien quy={moChiTiet} onDong={() => setMoChiTiet(null)} qc={qc} />
      )}
      <HopXacNhan
        mo={xoa !== null}
        tieuDe={t('taiChinh.xacNhanXoaQuy')}
        thongDiep={t('taiChinh.xacNhanXoaQuyMoTa', {
          ten: xoa?.tenQuy ?? '',
          soNguoi: xoa?.soNguoi ?? 0,
        })}
        onDongY={() => xoa && xoaMut.mutate(xoa.id)}
        onHuy={() => setXoa(null)}
      />
    </div>
  )
}

/** Thanh tiến độ thu — màu theo trạng thái, kèm số tuyệt đối vì tỷ lệ không nói ra số tiền. */
function ThanhTienDo({ quy }: { quy: QuyDto }) {
  const { t } = useTranslation()
  // Chia cho 0 khi đợt quỹ chưa có ai: hiện 0% thay vì NaN.
  const tyLe = quy.tongCanThu > 0 ? Math.min(100, (quy.tongDaThu / quy.tongCanThu) * 100) : 0
  const du = quy.soNguoi > 0 && quy.soNguoiDaDongDu === quy.soNguoi

  return (
    <div className="flex flex-col gap-1">
      <div className="flex items-baseline justify-between gap-2 text-xs">
        <span className="font-medium tabular-nums">
          {tienVnd(quy.tongDaThu)} / {tienVnd(quy.tongCanThu)}
        </span>
        <span className="text-muted-foreground">
          {quy.soNguoiDaDongDu}/{quy.soNguoi} {t('taiChinh.nguoi')}
        </span>
      </div>
      <div className="h-2 w-full overflow-hidden rounded-full bg-muted">
        <div
          className={cn(
            'h-full rounded-full transition-all',
            du ? 'bg-status-win' : quy.quaHan ? 'bg-status-lose' : 'bg-status-draw',
          )}
          style={{ width: `${tyLe}%` }}
        />
      </div>
    </div>
  )
}

/** FR-16 — tạo/sửa đợt quỹ: tên, thời hạn, danh sách người đóng và số tiền từng người. */
function FormQuy({
  quy,
  onDong,
  qc,
}: {
  quy: QuyDto | null
  onDong: () => void
  qc: ReturnType<typeof useQueryClient>
}) {
  const { t } = useTranslation()
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [chon, setChon] = useState<string[] | null>(null)
  /** Số tiền theo từng người — đặc tả FR-16 cho phép khác nhau. */
  const [soTien, setSoTien] = useState<Record<string, number>>({})
  const [dongLoat, setDongLoat] = useState('')

  const { data: cauThus } = useQuery({
    queryKey: ['cau-thu'],
    queryFn: async () =>
      (await api.get<KetQuaTrang<CauThuNgan>>('/cau-thu', { params: { soDong: 200 } })).data.duLieu,
  })

  // Sửa: nạp danh sách người đang có kèm số tiền của họ.
  const { data: chiTiet } = useQuery({
    enabled: quy !== null,
    queryKey: ['quy-chi-tiet', quy?.id],
    queryFn: async () => (await api.get<ChiTietQuyDto>(`/quy/${quy!.id}`)).data,
  })

  const luu = useMutation({
    mutationFn: async (body: Record<string, unknown>) =>
      quy ? api.put(`/quy/${quy.id}`, { ...body, id: quy.id }) : api.post('/quy', body),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['quy'] })
      void qc.invalidateQueries({ queryKey: ['tai-chinh-tong-quan'] })
      // Cả CHI TIẾT đợt quỹ: modal thu tiền đọc từ `quy-chi-tiet`, không phải `quy`. Thiếu
      // dòng này thì sửa đợt quỹ xong mở lại màn thu tiền vẫn thấy dữ liệu cũ — tắt hiển thị
      // chuyển khoản mà khối số tài khoản vẫn còn đó (lỗi thật, phát hiện khi xem màn hình).
      void qc.invalidateQueries({ queryKey: ['quy-chi-tiet'] })
      onDong()
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const dangChon = chon ?? chiTiet?.dongGops.map((d) => d.cauThuId) ?? []

  /** Số tiền của một người: nháp cục bộ → giá trị đã lưu → 0. */
  const tienCua = (cauThuId: string) =>
    soTien[cauThuId] ??
    chiTiet?.dongGops.find((d) => d.cauThuId === cauThuId)?.soTienCanDong ??
    0

  const apDongLoat = () => {
    const n = Number(dongLoat) || 0
    setSoTien(Object.fromEntries(dangChon.map((id) => [id, n])))
  }

  return (
    <Modal
      mo
      onDong={onDong}
      chanDoiKhiXuLy={luu.isPending}
      tieuDe={quy ? t('taiChinh.suaQuy') : t('taiChinh.themQuy')}
      moTa={quy?.tenQuy}
      rong="lg"
    >
      <form
        onSubmit={(e) => {
          e.preventDefault()
          const fd = new FormData(e.currentTarget)
          // Mọi trường lệnh cập nhật ghi đè đều đọc TỪ FORM (quy tắc #1).
          luu.mutate({
            tenQuy: String(fd.get('tenQuy')),
            thoiHan: (fd.get('thoiHan') as string) || null,
            ghiChu: (fd.get('ghiChu') as string) || null,
            trangThai: (fd.get('trangThai') as string) || 'DangMo',
            thanhViens: dangChon.map((cauThuId) => ({
              cauThuId,
              soTienCanDong: tienCua(cauThuId),
            })),
            // Đọc từ form như mọi trường khác (quy tắc #1): gửi giá trị cứng sẽ tắt hiển thị
            // mỗi lần thủ quỹ sửa tên đợt quỹ.
            hienThongTinChuyenKhoan: fd.get('hienChuyenKhoan') === 'on',
          })
        }}
        className="flex flex-col gap-4"
      >
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="tenQuy">{t('taiChinh.tenQuy')}</Label>
            <Input
              id="tenQuy"
              name="tenQuy"
              required
              autoFocus
              defaultValue={quy?.tenQuy}
              placeholder={t('taiChinh.tenQuyGoiY')}
            />
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="thoiHan">{t('taiChinh.thoiHan')}</Label>
            <Input id="thoiHan" name="thoiHan" type="date" defaultValue={quy?.thoiHan ?? ''} />
          </div>
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="trangThai">{t('taiChinh.trangThai')}</Label>
          <select
            id="trangThai"
            name="trangThai"
            defaultValue={quy?.trangThai ?? 'DangMo'}
            className="h-9 rounded-md border border-input bg-background px-3 text-sm"
          >
            <option value="DangMo">{t('taiChinh.tt.DangMo')}</option>
            <option value="DaDong">{t('taiChinh.tt.DaDong')}</option>
          </select>
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="thanhVien">{t('taiChinh.thanhVien')}</Label>
          <SelectTimKiemNhieu
            id="thanhVien"
            luaChon={(cauThus ?? []).map((c) => ({ giaTri: c.id, nhan: c.hoTen }))}
            giaTri={dangChon}
            onDoi={setChon}
            placeholder={t('taiChinh.chonThanhVien')}
            placeholderTimKiem={t('taiKhoan.timCauThu')}
          />
        </div>

        {dangChon.length > 0 && (
          <>
            {/* Đặt cùng số cho tất cả rồi sửa lẻ vài người — cách thủ quỹ làm thật, đỡ gõ
                cùng một con số mười lăm lần. */}
            <div className="flex flex-wrap items-end gap-2">
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="dongLoat">{t('taiChinh.dongLoat')}</Label>
                <Input
                  id="dongLoat"
                  type="number"
                  min={0}
                  step={1000}
                  value={dongLoat}
                  onChange={(e) => setDongLoat(e.target.value)}
                  placeholder="50000"
                  className="w-32"
                />
              </div>
              <Button type="button" variant="outline" size="sm" onClick={apDongLoat}>
                <Check className="h-4 w-4" />
                {t('taiChinh.apChoTatCa')}
              </Button>
            </div>

            <div className="max-h-64 overflow-y-auto rounded-lg border border-border">
              <Table>
                <thead>
                  <tr>
                    <Th>{t('cauThu.hoTen')}</Th>
                    <Th className="w-40">{t('taiChinh.soTienCanDong')}</Th>
                  </tr>
                </thead>
                <tbody>
                  {dangChon.map((id) => (
                    <tr key={id}>
                      <Td>{cauThus?.find((c) => c.id === id)?.hoTen ?? '—'}</Td>
                      <Td>
                        <Input
                          type="number"
                          min={0}
                          step={1000}
                          value={tienCua(id)}
                          onChange={(e) =>
                            setSoTien({ ...soTien, [id]: Number(e.target.value) || 0 })
                          }
                          className="h-8"
                        />
                      </Td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            </div>

            <p className="text-sm">
              {t('taiChinh.tongCanThu')}:{' '}
              <b className="tabular-nums text-primary">
                {tienVnd(dangChon.reduce((s, id) => s + tienCua(id), 0))}
              </b>
            </p>
          </>
        )}

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="ghiChu">{t('cauThu.ghiChu')}</Label>
          <Textarea id="ghiChu" name="ghiChu" defaultValue={quy?.ghiChu ?? ''} />
        </div>

        {/* Theo TỪNG ĐỢT, không phải bật/tắt toàn cục: có đợt thu tiền mặt tại sân, có đợt thu
            chuyển khoản. Hiện QR cho đợt thu tiền mặt chỉ làm người ta chuyển khoản trong khi
            thủ quỹ đang đứng chờ nhận tiền tươi. */}
        <div className="flex flex-col gap-1.5">
          <label className="flex items-start gap-2 text-sm">
            <input
              type="checkbox"
              name="hienChuyenKhoan"
              defaultChecked={quy?.hienThongTinChuyenKhoan ?? false}
              className="mt-0.5 h-4 w-4 rounded border-input accent-primary"
            />
            <span>
              {t('taiChinh.hienChuyenKhoan')}
              <span className="mt-0.5 block text-xs text-muted-foreground">
                {t('taiChinh.hienChuyenKhoanMoTa')}
              </span>
            </span>
          </label>
        </div>

        {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

        <ModalChan>
          <Button type="button" variant="outline" onClick={onDong} disabled={luu.isPending}>
            {t('chung.huy')}
          </Button>
          <Button type="submit" disabled={luu.isPending}>
            {luu.isPending ? t('chung.dangTai') : t('chung.luu')}
          </Button>
        </ModalChan>
      </form>
    </Modal>
  )
}

/** Ghi nhận tiền đã thu của từng người + sao chép danh sách nhắc nợ. */
function ModalThuTien({
  quy,
  onDong,
  qc,
}: {
  quy: QuyDto
  onDong: () => void
  qc: ReturnType<typeof useQueryClient>
}) {
  const { t } = useTranslation()
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [daChep, setDaChep] = useState(false)
  const [hoanTac, setHoanTac] = useState<DongGopDto | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['quy-chi-tiet', quy.id],
    queryFn: async () => (await api.get<ChiTietQuyDto>(`/quy/${quy.id}`)).data,
  })

  const thu = useMutation({
    mutationFn: async ({ id, soTien }: { id: string; soTien: number }) =>
      api.put(`/quy/dong-gop/${id}`, { dongGopId: id, soTienDaDong: soTien, ghiChu: null }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['quy-chi-tiet', quy.id] })
      void qc.invalidateQueries({ queryKey: ['quy'] })
      void qc.invalidateQueries({ queryKey: ['tai-chinh-tong-quan'] })
      setMaLoi(null)
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  /**
   * Sao chép danh sách nợ để dán vào Zalo/Messenger.
   *
   * Thay cho SMS/Email của đặc tả FR-16 — hạ tầng gửi thật chưa có, mà đây đúng là cách CLB
   * phong trào nhắc nợ: dán vào nhóm chat.
   */
  const chepDanhSachNo = async () => {
    const no = (data?.dongGops ?? []).filter((d) => !d.daDongDu)
    if (no.length === 0) return

    const dong = no.map(
      (d) => `- ${d.hoTen}: còn ${tienVnd(d.soTienCanDong - d.soTienDaDong)}`,
    )
    const noiDung = [
      `${quy.tenQuy}${quy.thoiHan ? ` (hạn ${new Date(quy.thoiHan).toLocaleDateString('vi-VN')})` : ''}`,
      ...dong,
    ].join('\n')

    await navigator.clipboard.writeText(noiDung)
    setDaChep(true)
    setTimeout(() => setDaChep(false), 2500)
  }

  const soNo = (data?.dongGops ?? []).filter((d) => !d.daDongDu).length

  return (
    <Modal mo onDong={onDong} tieuDe={t('taiChinh.thuTien')} moTa={quy.tenQuy} rong="lg">
      {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      {/* Thông tin chuyển khoản — backend chỉ trả khi đợt quỹ bật hiển thị VÀ CLB đã khai gì đó,
          nên ở đây chỉ cần kiểm null. Đặt TRÊN danh sách: thủ quỹ mở màn này để đọc số tài khoản
          cho người khác, không phải cuộn xuống cuối tìm. */}
      {data?.chuyenKhoan && <KhoiChuyenKhoan tt={data.chuyenKhoan} />}

      {isLoading ? (
        <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
      ) : !data?.dongGops.length ? (
        <TrangTrong thongDiep={t('taiChinh.quyChuaCoNguoi')} />
      ) : (
        <>
          <div className="mb-3 flex flex-wrap items-center gap-3">
            <ThanhTienDo quy={data.quy} />
            {soNo > 0 && (
              <Button variant="outline" size="sm" onClick={chepDanhSachNo}>
                <ClipboardCopy className="h-4 w-4" />
                {daChep ? t('taiChinh.daChep') : t('taiChinh.chepDanhSachNo', { so: soNo })}
              </Button>
            )}
          </div>

          <Table>
            <thead>
              <tr>
                <Th>{t('cauThu.hoTen')}</Th>
                <Th className="w-32">{t('taiChinh.canDong')}</Th>
                <Th className="w-52">{t('taiChinh.daDong')}</Th>
                <Th className="w-28">{t('taiChinh.conThieu')}</Th>
              </tr>
            </thead>
            <tbody>
              {data.dongGops.map((d) => (
                <tr key={d.id} className="hover:bg-muted/40">
                  <Td className="font-medium">{d.hoTen}</Td>
                  <Td className="tabular-nums text-muted-foreground">
                    {tienVnd(d.soTienCanDong)}
                  </Td>
                  <Td>
                    <div className="flex items-center gap-1.5">
                      <Input
                        type="number"
                        min={0}
                        step={1000}
                        // `key` buộc React dựng lại ô khi số tiền đổi từ phía server.
                        //
                        // Ô này dùng `defaultValue` (không kiểm soát) để gõ giữa chừng không bị
                        // ghi đè. Nhưng `defaultValue` chỉ có tác dụng ở lần render ĐẦU: bấm
                        // "đã đóng đủ" xong, dữ liệu về 100.000 mà ô vẫn hiện 0 — người dùng
                        // thấy ô mâu thuẫn với cột "Còn thiếu" và tưởng chưa lưu được.
                        key={`${d.id}-${d.soTienDaDong}`}
                        defaultValue={d.soTienDaDong}
                        // Lưu khi rời ô, không lưu mỗi ký tự: gõ "50000" mà gửi 5 request
                        // thì con số trung gian (5, 50, 500…) cũng bị ghi vào DB.
                        onBlur={(e) => {
                          const v = Number(e.target.value) || 0
                          if (v !== d.soTienDaDong) thu.mutate({ id: d.id, soTien: v })
                        }}
                        className="h-8"
                      />

                      {/* Hai nút có CHỖ RIÊNG cố định, không chồng vị trí nhau.
                          Dùng chung một chỗ thì sau khi bấm ✓ (thu đủ), nút hoàn tác nhảy vào
                          đúng toạ độ đó — cú bấm tiếp theo theo quán tính sẽ xoá mất khoản vừa
                          ghi. Có hộp xác nhận vẫn đỡ, nhưng đừng dựng cái bẫy ngay từ đầu. */}
                      <span className="w-8 shrink-0">
                        {/* Thu đủ: thao tác thường gặp nhất, đỡ gõ lại đúng con số. */}
                        {!d.daDongDu && (
                          <Button
                            variant="ghost"
                            size="sm"
                            title={t('taiChinh.thuDu')}
                            onClick={() => thu.mutate({ id: d.id, soTien: d.soTienCanDong })}
                          >
                            <Check className="h-4 w-4 text-status-win" />
                          </Button>
                        )}
                      </span>

                      <span className="w-8 shrink-0">
                        {/* Hoàn tác về 0. Trước đây chỉ sửa được bằng cách tự xoá ô rồi gõ "0"
                            — không ai đoán ra, nên bấm nhầm ✓ là coi như xong.

                            Có XÁC NHẬN vì đây là thao tác trên tiền: nó xoá vết một khoản đã
                            ghi nhận. */}
                        {d.soTienDaDong > 0 && (
                          <Button
                            variant="ghost"
                            size="sm"
                            title={t('taiChinh.hoanTac')}
                            onClick={() => setHoanTac(d)}
                          >
                            <Undo2 className="h-4 w-4 text-muted-foreground" />
                          </Button>
                        )}
                      </span>
                    </div>
                  </Td>
                  <Td>
                    {d.daDongDu ? (
                      <Badge variant="win">{t('taiChinh.du')}</Badge>
                    ) : (
                      <span className="font-medium tabular-nums text-status-lose">
                        {tienVnd(d.soTienCanDong - d.soTienDaDong)}
                      </span>
                    )}
                  </Td>
                </tr>
              ))}
            </tbody>
          </Table>
        </>
      )}

      <ModalChan>
        <Button type="button" variant="outline" onClick={onDong}>
          {t('chung.dong')}
        </Button>
      </ModalChan>

      {hoanTac && (
        <HopXacNhan
          mo
          tieuDe={t('taiChinh.hoanTac')}
          thongDiep={t('taiChinh.hoanTacXacNhan', {
            ten: hoanTac.hoTen,
            tien: tienVnd(hoanTac.soTienDaDong),
          })}
          nhanDongY={t('taiChinh.hoanTac')}
          onHuy={() => setHoanTac(null)}
          onDongY={() => {
            thu.mutate({ id: hoanTac.id, soTien: 0 })
            setHoanTac(null)
          }}
        />
      )}
    </Modal>
  )
}

/**
 * Khối thông tin chuyển khoản trong màn thu tiền.
 *
 * Chỉ HIỂN THỊ — hệ thống không xử lý tiền. Thủ quỹ đọc số này cho thành viên (hoặc chụp lại gửi
 * nhóm chat), người ta chuyển, rồi thủ quỹ nhập tay số đã nhận vào bảng bên dưới. Không có
 * webhook, không đối chiếu sao kê: tự động ghi nhận đòi quyền đọc sao kê ngân hàng của CLB.
 */
function KhoiChuyenKhoan({ tt }: { tt: ThongTinChuyenKhoan }) {
  const { t } = useTranslation()
  const [daChep, setDaChep] = useState(false)

  const chepSoTaiKhoan = async () => {
    if (!tt.soTaiKhoan) return
    await navigator.clipboard.writeText(tt.soTaiKhoan)
    setDaChep(true)
    setTimeout(() => setDaChep(false), 2000)
  }

  return (
    <div className="mb-4 flex flex-wrap items-start gap-4 rounded-lg border border-border bg-muted/40 p-3">
      {tt.anhQrUrl && (
        // Cỡ vừa phải: QR to quá thì đẩy danh sách người đóng xuống dưới màn hình, mà thủ quỹ
        // mở modal này chủ yếu để thu tiền.
        <Anh khoa={tt.anhQrUrl} className="h-32 w-32 shrink-0 rounded-md object-contain" />
      )}

      <div className="flex min-w-0 flex-col gap-1 text-sm">
        <p className="font-medium">{t('taiChinh.chuyenKhoanTieuDe')}</p>

        {tt.soTaiKhoan && (
          <p className="flex flex-wrap items-center gap-2">
            <span className="font-mono text-base tracking-wide">{tt.soTaiKhoan}</span>
            <Button type="button" variant="ghost" size="sm" onClick={() => void chepSoTaiKhoan()}>
              {daChep ? t('taiChinh.daChep') : t('taiChinh.saoChepSoTk')}
            </Button>
          </p>
        )}
        {tt.tenNganHang && <p className="text-muted-foreground">{tt.tenNganHang}</p>}
        {tt.chuTaiKhoan && <p className="text-muted-foreground">{tt.chuTaiKhoan}</p>}

        {/* Nói rõ hệ thống KHÔNG tự ghi nhận: thành viên chuyển xong mà thấy tiến độ vẫn 0 sẽ
            tưởng chuyển thất bại và chuyển lại lần nữa. */}
        <p className="mt-1 text-xs text-muted-foreground">{t('taiChinh.chuyenKhoanLuuY')}</p>
      </div>
    </div>
  )
}
