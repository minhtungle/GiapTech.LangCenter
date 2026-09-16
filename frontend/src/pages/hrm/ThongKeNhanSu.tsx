import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Star } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Card, CardContent, Input, Label, Table, Td, Textarea, Th,
  TrangTrong,
} from '@/components/ui'
import { Modal, ModalChan } from '@/components/ui/Modal'
import { KhungBieuDo, ThanhNgang, tienGon } from '@/components/bieu-do'
import { useQuyen } from '@/lib/quyen'
import { useXacNhan } from '@/lib/xacNhan'
import type { TieuChiDto } from './TieuChiDanhGia'

/** `yyyy-MM-dd` theo giờ ĐỊA PHƯƠNG — `toISOString()` quy về UTC nên lùi một ngày ở UTC+7. */
const ngayISO = (d: Date) =>
  `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`

/** Mặc định 12 tháng gần nhất — cùng kỳ mặc định với Thống kê CRM để hai màn so được. */
const KHOANG_MAC_DINH = () => {
  const nay = new Date()
  return { tuNgay: ngayISO(new Date(nay.getFullYear(), nay.getMonth() - 11, 1)), denNgay: ngayISO(nay) }
}

interface HangKinhDoanh {
  id: string
  hoTen: string
  tenPhongBan: string | null
  doanhThu: number
  soDon: number
  soHocVien: number
  diemChatLuong: number | null
  soPhieu: number
}

interface HangGiangDay {
  id: string
  hoTen: string
  tenPhongBan: string | null
  soLop: number
  soBuoiDayDu: number
  soBuoiHuy: number
  diemChatLuong: number | null
  soPhieu: number
}

interface ThongKeNhanSuDto {
  kinhDoanh: HangKinhDoanh[]
  giaoVien: HangGiangDay[]
  troGiang: HangGiangDay[]
  tieuChiKinhDoanh: TieuChiDto[]
  tieuChiGiangDay: TieuChiDto[]
}

type TabVaiTro = 'kinh-doanh' | 'giao-vien' | 'tro-giang'

/** Điểm 1–5 hiện dạng số + sao: số để so nhanh, sao để đọc không cần quy đổi. */
function DiemSao({ diem, soPhieu }: { diem: number | null; soPhieu: number }) {
  const { t } = useTranslation()
  if (diem === null) return <span className="text-xs text-muted-foreground">—</span>
  return (
    <span className="inline-flex items-center gap-1" title={t('thongKeNs.soPhieu', { so: soPhieu })}>
      <span className="font-medium">{diem.toFixed(1)}</span>
      <Star className="h-3.5 w-3.5 fill-[hsl(var(--chart-3))] text-[hsl(var(--chart-3))]" />
      <span className="text-xs text-muted-foreground">({soPhieu})</span>
    </span>
  )
}

/**
 * FR-29 — **thống kê nhân sự** (16/09/2026), theo yêu cầu chủ sản phẩm: *"tương tự thống kê tại
 * CRM nhưng chỉ cho nhân viên kinh doanh, giáo viên và trợ giảng"*.
 *
 * Ba tab, mỗi vai trò một bộ chỉ số riêng — chủ sản phẩm nêu tường minh:
 *
 * | Vai trò | Chỉ số |
 * |---|---|
 * | Kinh doanh | doanh thu · số học viên · chất lượng chăm sóc |
 * | Giáo viên | số lớp · chất lượng giảng dạy · số buổi dạy đủ |
 * | Trợ giảng | như giáo viên |
 *
 * **Bảng chứ không biểu đồ cho phần chính**: mỗi người có 3–4 chỉ số không cùng đơn vị (tiền,
 * số đếm, điểm 1–5) — ghép vào một biểu đồ là bịa ra tương quan giữa chúng. Biểu đồ thanh chỉ
 * dùng cho **một** chỉ số tại một lúc, ở khối dưới.
 */
export default function ThongKeNhanSu() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { coQuyen } = useQuyen()
  const { hoi, hop } = useXacNhan()

  const macDinh = KHOANG_MAC_DINH()
  const [tuNgay, setTuNgay] = useState(macDinh.tuNgay)
  const [denNgay, setDenNgay] = useState(macDinh.denNgay)
  const [tab, setTab] = useState<TabVaiTro>('kinh-doanh')

  /** Nhân viên đang được chấm điểm (null = không mở hộp thoại). */
  const [dangCham, setDangCham] = useState<HangKinhDoanh | null>(null)
  const [ky, setKy] = useState(() => new Date().toISOString().slice(0, 7))
  const [diems, setDiems] = useState<Record<string, number>>({})
  const [nhanXet, setNhanXet] = useState('')
  const [maLoiCham, setMaLoiCham] = useState<string | null>(null)

  // `denNgay` + 1 ngày: handler so `< den` (cùng quy ước với thống kê CRM). Gửi thẳng ngày đã
  // chọn thì mất trọn ngày cuối kỳ — lỗi lệch một ngày không có gì báo.
  const denGuiLen = (() => {
    if (!denNgay) return undefined
    const d = new Date(`${denNgay}T00:00:00`)
    d.setDate(d.getDate() + 1)
    return ngayISO(d)
  })()

  const khoangSai = !!tuNgay && !!denNgay && tuNgay > denNgay

  const { data: tk, isLoading } = useQuery({
    queryKey: ['thong-ke-nhan-su', tuNgay, denGuiLen],
    enabled: !khoangSai,
    queryFn: async () =>
      (await api.get<ThongKeNhanSuDto>('/thong-ke-nhan-su', {
        params: { tuNgay: tuNgay || undefined, denNgay: denGuiLen },
      })).data,
  })

  const luuPhieu = useMutation({
    mutationFn: async () => {
      if (!dangCham) return
      await api.post('/thong-ke-nhan-su/phieu', {
        nhanVienId: dangCham.id,
        ky,
        nhanXet: nhanXet.trim() || null,
        diems: Object.entries(diems).map(([tieuChiId, diem]) => ({ tieuChiId, diem })),
      })
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['thong-ke-nhan-su'] })
      setDangCham(null)
      setMaLoiCham(null)
    },
    onError: (e) => setMaLoiCham(layMaLoi(e)),
  })

  /** Mở hộp thoại chấm, tải phiếu kỳ đó nếu đã chấm để sửa thay vì chấm lại từ đầu. */
  const moCham = async (nv: HangKinhDoanh) => {
    setDangCham(nv)
    setMaLoiCham(null)
    setDiems({})
    setNhanXet('')
    try {
      const { data } = await api.get<{
        nhanXet: string | null
        diems: { tieuChiId: string; diem: number }[]
      } | null>('/thong-ke-nhan-su/phieu', { params: { nhanVienId: nv.id, ky } })
      if (data) {
        setNhanXet(data.nhanXet ?? '')
        setDiems(Object.fromEntries(data.diems.map((d) => [d.tieuChiId, d.diem])))
      }
    } catch {
      // Chưa có phiếu là chuyện thường — để form trống, không báo lỗi.
    }
  }

  const duocCham = coQuyen('ThongKeNhanSu', 'Cham')

  return (
    <div className="space-y-4">
      {/* Bộ lọc thời gian — cùng kiểu và cùng nhãn với Khách hàng / Doanh thu / Thống kê CRM. */}
      <div className="flex flex-wrap items-end gap-3">
        <div className="w-40">
          <Label htmlFor="ns-tu-ngay">{t('doanhThu.tuNgay')}</Label>
          <Input id="ns-tu-ngay" type="date" value={tuNgay} max={denNgay || undefined}
                 onChange={(e) => setTuNgay(e.target.value)} />
        </div>
        <div className="w-40">
          <Label htmlFor="ns-den-ngay">{t('doanhThu.denNgay')}</Label>
          <Input id="ns-den-ngay" type="date" value={denNgay} min={tuNgay || undefined}
                 onChange={(e) => setDenNgay(e.target.value)} />
        </div>

        {(tuNgay !== macDinh.tuNgay || denNgay !== macDinh.denNgay) && (
          <Button variant="outline" size="sm"
                  onClick={() => { setTuNgay(macDinh.tuNgay); setDenNgay(macDinh.denNgay) }}>
            {t('thongKe.kyMacDinh')}
          </Button>
        )}

        {/* Ba tab vai trò */}
        <div className="ml-auto flex flex-wrap gap-1 rounded-lg border border-border p-1">
          {([
            ['kinh-doanh', 'thongKeNs.tabKinhDoanh'],
            ['giao-vien', 'thongKeNs.tabGiaoVien'],
            ['tro-giang', 'thongKeNs.tabTroGiang'],
          ] as const).map(([ma, khoa]) => (
            <button
              key={ma}
              type="button"
              onClick={() => setTab(ma)}
              className={
                'rounded-md px-3 py-1.5 text-sm font-medium transition-colors '
                + (tab === ma
                  ? 'bg-primary text-primary-foreground'
                  : 'text-muted-foreground hover:bg-muted')
              }
            >
              {t(khoa)}
            </button>
          ))}
        </div>
      </div>

      {khoangSai && <CanhBaoLoi>{t('thongKe.khoangSai')}</CanhBaoLoi>}
      {isLoading && <TrangTrong thongDiep={t('chung.dangTai')} />}

      {tk && tab === 'kinh-doanh' && (
        <>
          {tk.tieuChiKinhDoanh.length === 0 && (
            <p className="text-sm text-muted-foreground">{t('thongKeNs.chuaCoTieuChiKd')}</p>
          )}

          <Card>
            <CardContent className="pt-6">
              {tk.kinhDoanh.length === 0 ? (
                <TrangTrong thongDiep={t('thongKeNs.chuaCoNhanVienKd')} />
              ) : (
                <Table>
                  <thead>
                    <tr>
                      <Th>{t('thongKeNs.hoTen')}</Th>
                      <Th>{t('crmLoc.doiNhom')}</Th>
                      <Th className="text-right">{t('thongKe.tongDoanhThu')}</Th>
                      <Th className="text-right">{t('thongKe.soDon')}</Th>
                      <Th className="text-right">{t('thongKeNs.soHocVien')}</Th>
                      <Th>{t('thongKeNs.chatLuongChamSoc')}</Th>
                      {duocCham && <Th />}
                    </tr>
                  </thead>
                  <tbody>
                    {tk.kinhDoanh.map((nv) => (
                      <tr key={nv.id} className="hover:bg-muted/40">
                        <Td className="font-medium">{nv.hoTen}</Td>
                        <Td className="text-muted-foreground">{nv.tenPhongBan ?? '—'}</Td>
                        <Td className="text-right font-medium">{tienGon(nv.doanhThu)}</Td>
                        <Td className="text-right">{nv.soDon}</Td>
                        <Td className="text-right">
                          <Badge variant="muted">{nv.soHocVien}</Badge>
                        </Td>
                        <Td><DiemSao diem={nv.diemChatLuong} soPhieu={nv.soPhieu} /></Td>
                        {duocCham && (
                          <Td>
                            <div className="flex justify-end">
                              <Button variant="outline" size="sm" onClick={() => void moCham(nv)}>
                                {t('thongKeNs.cham')}
                              </Button>
                            </div>
                          </Td>
                        )}
                      </tr>
                    ))}
                  </tbody>
                </Table>
              )}
            </CardContent>
          </Card>

          <div className="grid gap-3 lg:grid-cols-2">
            <KhungBieuDo
              tieuDe={t('thongKeNs.xepHangDoanhThu')}
              trong={tk.kinhDoanh.every((x) => x.doanhThu === 0) ? t('thongKe.chuaCoDon') : null}
            >
              <ThanhNgang
                hang={tk.kinhDoanh.filter((x) => x.doanhThu > 0)
                  .map((x) => ({ nhan: x.hoTen, giaTri: x.doanhThu,
                                 phu: t('thongKe.soDonNgan', { so: x.soDon }) }))}
              />
            </KhungBieuDo>

            <KhungBieuDo
              tieuDe={t('thongKeNs.xepHangHocVien')}
              moTa={t('thongKeNs.xepHangHocVienMoTa')}
              trong={tk.kinhDoanh.every((x) => x.soHocVien === 0)
                ? t('thongKeNs.chuaCoHocVien') : null}
            >
              <ThanhNgang
                hang={tk.kinhDoanh.filter((x) => x.soHocVien > 0)
                  .map((x) => ({ nhan: x.hoTen, giaTri: x.soHocVien }))}
                dinhDang={(v) => String(v)}
                mau="hsl(var(--chart-2))"
              />
            </KhungBieuDo>
          </div>
        </>
      )}

      {tk && tab !== 'kinh-doanh' && (() => {
        const hang = tab === 'giao-vien' ? tk.giaoVien : tk.troGiang
        return (
          <>
            {tk.tieuChiGiangDay.length === 0 && (
              <p className="text-sm text-muted-foreground">{t('thongKeNs.chuaCoTieuChiGd')}</p>
            )}

            <Card>
              <CardContent className="pt-6">
                {hang.length === 0 ? (
                  <TrangTrong thongDiep={t('thongKeNs.chuaCoNguoi')} />
                ) : (
                  <Table>
                    <thead>
                      <tr>
                        <Th>{t('thongKeNs.hoTen')}</Th>
                        <Th>{t('crmLoc.doiNhom')}</Th>
                        <Th className="text-right">{t('thongKeNs.soLop')}</Th>
                        <Th className="text-right">{t('thongKeNs.soBuoiDayDu')}</Th>
                        <Th className="text-right">{t('thongKeNs.soBuoiHuy')}</Th>
                        <Th>{t('thongKeNs.chatLuongGiangDay')}</Th>
                      </tr>
                    </thead>
                    <tbody>
                      {hang.map((gv) => (
                        <tr key={gv.id} className="hover:bg-muted/40">
                          <Td className="font-medium">{gv.hoTen}</Td>
                          <Td className="text-muted-foreground">{gv.tenPhongBan ?? '—'}</Td>
                          <Td className="text-right">{gv.soLop}</Td>
                          <Td className="text-right font-medium">{gv.soBuoiDayDu}</Td>
                          <Td className="text-right text-muted-foreground">{gv.soBuoiHuy}</Td>
                          <Td><DiemSao diem={gv.diemChatLuong} soPhieu={gv.soPhieu} /></Td>
                        </tr>
                      ))}
                    </tbody>
                  </Table>
                )}
              </CardContent>
            </Card>

            <div className="grid gap-3 lg:grid-cols-2">
              <KhungBieuDo
                tieuDe={t('thongKeNs.xepHangBuoiDay')}
                trong={hang.every((x) => x.soBuoiDayDu === 0)
                  ? t('thongKeNs.chuaCoBuoi') : null}
              >
                <ThanhNgang
                  hang={hang.filter((x) => x.soBuoiDayDu > 0)
                    .map((x) => ({ nhan: x.hoTen, giaTri: x.soBuoiDayDu,
                                   phu: t('thongKeNs.soLopNgan', { so: x.soLop }) }))}
                  dinhDang={(v) => String(v)}
                />
              </KhungBieuDo>

              <KhungBieuDo
                tieuDe={t('thongKeNs.xepHangChatLuong')}
                moTa={t('thongKeNs.xepHangChatLuongMoTa')}
                trong={hang.every((x) => x.diemChatLuong === null)
                  ? t('thongKeNs.chuaCoDanhGia') : null}
              >
                <ThanhNgang
                  hang={hang.filter((x) => x.diemChatLuong !== null)
                    .map((x) => ({ nhan: x.hoTen, giaTri: x.diemChatLuong!,
                                   phu: t('thongKeNs.soPhieu', { so: x.soPhieu }) }))}
                  dinhDang={(v) => v.toFixed(1)}
                  mau="hsl(var(--chart-3))"
                />
              </KhungBieuDo>
            </div>
          </>
        )
      })()}

      {/* Hộp thoại chấm điểm nhân viên kinh doanh */}
      <Modal
        mo={!!dangCham}
        onDong={() => setDangCham(null)}
        chanDoiKhiXuLy={luuPhieu.isPending}
        tieuDe={t('thongKeNs.chamTieuDe')}
        moTa={dangCham?.hoTen}
        rong="sm"
      >
        <div className="grid gap-3">
          <div>
            <Label htmlFor="ky-cham">{t('thongKeNs.ky')} *</Label>
            {/*
              `type=month` cho đúng dạng `yyyy-MM` mà backend đòi — gõ tay thì "2026-9" lọt qua
              và thành một kỳ khác với "2026-09".
            */}
            <Input id="ky-cham" type="month" value={ky}
                   onChange={(e) => { setKy(e.target.value); setDiems({}); setNhanXet('') }} />
            <p className="mt-1 text-xs text-muted-foreground">{t('thongKeNs.kyGoiY')}</p>
          </div>

          {(tk?.tieuChiKinhDoanh ?? []).filter((x) => x.dangDung).length === 0 ? (
            <CanhBaoLoi>{t('thongKeNs.chuaCoTieuChiKd')}</CanhBaoLoi>
          ) : (
            <div className="grid gap-2">
              {(tk?.tieuChiKinhDoanh ?? []).filter((x) => x.dangDung).map((tc) => (
                <div key={tc.id} className="rounded-md border border-border p-2">
                  <div className="text-sm font-medium">{tc.ten}</div>
                  {tc.moTa && <div className="text-xs text-muted-foreground">{tc.moTa}</div>}
                  <div className="mt-1.5 flex gap-1">
                    {[1, 2, 3, 4, 5].map((d) => (
                      <button
                        key={d}
                        type="button"
                        aria-label={`${tc.ten} ${d}`}
                        onClick={() => setDiems((cu) => ({ ...cu, [tc.id]: d }))}
                        className="rounded p-0.5 transition-transform hover:scale-110"
                      >
                        <Star
                          className={
                            'h-5 w-5 '
                            + ((diems[tc.id] ?? 0) >= d
                              ? 'fill-[hsl(var(--chart-3))] text-[hsl(var(--chart-3))]'
                              : 'text-muted-foreground')
                          }
                        />
                      </button>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          )}

          <div>
            <Label htmlFor="nx-cham">{t('thongKeNs.nhanXet')}</Label>
            <Textarea id="nx-cham" rows={2} maxLength={2000} value={nhanXet}
                      onChange={(e) => setNhanXet(e.target.value)} />
          </div>

          {maLoiCham && <CanhBaoLoi>{t(`loi.${maLoiCham}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

          <ModalChan>
            <Button variant="outline" onClick={() => setDangCham(null)}>{t('chung.huy')}</Button>
            <Button
              disabled={Object.keys(diems).length === 0 || luuPhieu.isPending}
              onClick={() =>
                hoi({
                  tieuDe: t('chung.xacNhanLuu'),
                  thongDiep: t('thongKeNs.hoiLuuPhieu', { ten: dangCham?.hoTen ?? '', ky }),
                  onDongY: () => luuPhieu.mutate(),
                })
              }
            >
              {t('chung.luu')}
            </Button>
          </ModalChan>
        </div>
      </Modal>

      {hop}
    </div>
  )
}
