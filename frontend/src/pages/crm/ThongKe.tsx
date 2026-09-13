import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api } from '@/lib/api'
import { Card, CardContent, Label, TrangTrong } from '@/components/ui'
import { SelectTimKiem } from '@/components/ui/SelectTimKiem'
import {
  DuongThoiGian, KhungBieuDo, OSo, Pheu, ThanhNgang, tienDayDu, tienGon,
} from '@/components/bieu-do'

interface PhanBo {
  id: string | null
  ten: string | null
  doanhThu: number
  soDon: number
}

interface ThongKeCrmDto {
  tongDoanhThu: number
  doanhThuKyTruoc: number | null
  soDon: number
  giaTriDonTb: number
  daThu: number
  theoThoiGian: { nhan: string; moc: string; doanhThu: number; soDon: number }[]
  theoCaNhan: PhanBo[]
  theoDoiNhom: PhanBo[]
  theoSanPham: PhanBo[]
  theoNguon: PhanBo[]
  pheu: { trangThai: string; soKhach: number }[]
}

/** Khoảng thời gian — tính ở client rồi gửi mốc tuyệt đối, backend không đoán ý. */
const KHOANG = [
  { ma: '12thang', thang: 12 },
  { ma: '6thang', thang: 6 },
  { ma: '3thang', thang: 3 },
  { ma: 'thangNay', thang: 0 },
] as const

type MaKhoang = (typeof KHOANG)[number]['ma']

/**
 * FR-28 — thống kê CRM (14/09/2026).
 *
 * **Doanh số của một đơn tính cho người TẠO HỒ SƠ KHÁCH**, chốt với chủ sản phẩm. Xem
 * `ThongKeCrmDtos.cs` để biết vì sao không dùng "người nhập đơn" hay "người đang chăm".
 *
 * Nguyên tắc trực quan hoá áp ở đây, mỗi cái có lý do:
 * - **Ô số cho con số đơn lẻ**, không dùng biểu đồ một thanh.
 * - **Thanh xếp hạng dùng MỘT màu**: người/đội/mặt hàng không có thứ tự tự nhiên, tô mỗi
 *   thanh một màu là mã hoá hai lần thứ chiều dài đã nói.
 * - **Phễu dùng dải một màu đậm dần**: các bước CÓ thứ tự nên màu phải có thứ tự.
 * - Không có biểu đồ tròn: so sánh góc khó hơn so sánh chiều dài.
 * - Không có biểu đồ hai trục — hai thang đo ghép vào một khung sẽ bịa ra tương quan.
 */
export default function ThongKe() {
  const { t } = useTranslation()
  const [khoang, setKhoang] = useState<MaKhoang>('12thang')

  const { tuNgay, denNgay } = (() => {
    const nay = new Date()
    const den = new Date(nay.getFullYear(), nay.getMonth(), nay.getDate() + 1)
    const cau = KHOANG.find((k) => k.ma === khoang)!
    const tu =
      cau.thang === 0
        ? new Date(nay.getFullYear(), nay.getMonth(), 1)
        : new Date(nay.getFullYear(), nay.getMonth() - (cau.thang - 1), 1)
    return { tuNgay: tu.toISOString(), denNgay: den.toISOString() }
  })()

  const { data: tk, isLoading } = useQuery({
    queryKey: ['thong-ke-crm', tuNgay, denNgay],
    queryFn: async () =>
      (await api.get<ThongKeCrmDto>('/thong-ke-crm', { params: { tuNgay, denNgay } })).data,
  })

  if (isLoading) return <TrangTrong thongDiep={t('chung.dangTai')} />
  if (!tk) return <TrangTrong thongDiep={t('loi.KHONG_TIM_THAY')} />

  const delta =
    tk.doanhThuKyTruoc && tk.doanhThuKyTruoc > 0
      ? ((tk.tongDoanhThu - tk.doanhThuKyTruoc) / tk.doanhThuKyTruoc) * 100
      : undefined

  const conThieu = tk.tongDoanhThu - tk.daThu

  /** Tên hiển thị của một lát; null = nhóm chưa xác định (đơn cũ, người chưa gán phòng ban). */
  const ten = (p: PhanBo) => p.ten ?? t('thongKe.khongXacDinh')

  const hang = (ds: PhanBo[]) =>
    ds.map((p) => ({
      nhan: ten(p),
      giaTri: p.doanhThu,
      phu: t('thongKe.soDonNgan', { so: p.soDon }),
    }))

  return (
    <div className="grid gap-4">
      <div className="flex flex-wrap items-end gap-3">
        <div className="w-48">
          <Label>{t('thongKe.khoangThoiGian')}</Label>
          <SelectTimKiem
            giaTri={khoang}
            luaChon={KHOANG.map((k) => ({ giaTri: k.ma, nhan: t(`thongKe.khoang.${k.ma}`) }))}
            onDoi={(v) => setKhoang((v as MaKhoang) ?? '12thang')}
          />
        </div>
      </div>

      {/* Hàng ô số — bốn con số dẫn, không phải biểu đồ. */}
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <OSo
          nhan={t('thongKe.tongDoanhThu')}
          giaTri={tienGon(tk.tongDoanhThu)}
          phu={delta !== undefined ? t('thongKe.soKyTruoc') : t('thongKe.chuaCoKyTruoc')}
          deltaPhanTram={delta}
          nhanMau="hsl(var(--chart-1))"
        />
        <OSo nhan={t('thongKe.soDon')} giaTri={String(tk.soDon)} />
        <OSo nhan={t('thongKe.giaTriDonTb')} giaTri={tienGon(tk.giaTriDonTb)} />
        <OSo
          nhan={t('thongKe.daThu')}
          giaTri={tienGon(tk.daThu)}
          phu={
            conThieu > 0
              ? t('thongKe.conThieu', { so: tienGon(conThieu) })
              : t('thongKe.thuDu')
          }
        />
      </div>

      <Card>
        <CardContent className="pt-4">
          <KhungBieuDo
            tieuDe={t('thongKe.doanhThuTheoThang')}
            moTa={t('thongKe.doanhThuTheoThangMoTa')}
            trong={tk.theoThoiGian.length === 0 ? t('thongKe.chuaCoDon') : null}
          >
            <DuongThoiGian
              diem={tk.theoThoiGian.map((d) => ({
                nhan: d.nhan,
                giaTri: d.doanhThu,
                phu: t('thongKe.soDonNgan', { so: d.soDon }),
              }))}
              dinhDangDayDu={tienDayDu}
            />
          </KhungBieuDo>
        </CardContent>
      </Card>

      <div className="grid gap-4 lg:grid-cols-2">
        <KhungBieuDo
          tieuDe={t('thongKe.theoCaNhan')}
          moTa={t('thongKe.theoCaNhanMoTa')}
          trong={tk.theoCaNhan.length === 0 ? t('thongKe.chuaCoDon') : null}
        >
          <ThanhNgang hang={hang(tk.theoCaNhan)} />
        </KhungBieuDo>

        <KhungBieuDo
          tieuDe={t('thongKe.theoDoiNhom')}
          moTa={t('thongKe.theoDoiNhomMoTa')}
          trong={tk.theoDoiNhom.length === 0 ? t('thongKe.chuaCoDon') : null}
        >
          <ThanhNgang hang={hang(tk.theoDoiNhom)} mau="hsl(var(--chart-3))" />
        </KhungBieuDo>

        <KhungBieuDo
          tieuDe={t('thongKe.theoSanPham')}
          moTa={t('thongKe.theoSanPhamMoTa')}
          trong={tk.theoSanPham.length === 0 ? t('thongKe.chuaCoDon') : null}
        >
          <ThanhNgang hang={hang(tk.theoSanPham)} mau="hsl(var(--chart-2))" />
        </KhungBieuDo>

        <KhungBieuDo
          tieuDe={t('thongKe.pheu')}
          moTa={t('thongKe.pheuMoTa')}
          trong={tk.pheu.every((b) => b.soKhach === 0) ? t('thongKe.chuaCoKhach') : null}
        >
          <Pheu
            buoc={tk.pheu.map((b) => ({
              nhan: t(`trangThaiKhachHang.${b.trangThai}`),
              soLuong: b.soKhach,
            }))}
          />
        </KhungBieuDo>

        <KhungBieuDo
          tieuDe={t('thongKe.theoNguon')}
          moTa={t('thongKe.theoNguonMoTa')}
          trong={tk.theoNguon.length === 0 ? t('thongKe.chuaCoDon') : null}
          className="lg:col-span-2"
        >
          <ThanhNgang
            hang={tk.theoNguon.map((p) => ({
              nhan: p.ten ? t(`nguonKhachHang.${p.ten}`) : t('thongKe.khongXacDinh'),
              giaTri: p.doanhThu,
              phu: t('thongKe.soDonNgan', { so: p.soDon }),
            }))}
            mau="hsl(var(--chart-4))"
          />
        </KhungBieuDo>
      </div>
    </div>
  )
}
