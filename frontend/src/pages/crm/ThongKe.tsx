import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api } from '@/lib/api'
import {
  Button, CanhBaoLoi, Card, CardContent, Input, Label, TrangTrong,
} from '@/components/ui'
import { SelectTimKiem, SelectTimKiemNhieu } from '@/components/ui/SelectTimKiem'
import {
  DuongThoiGian, KhungBieuDo, OSo, Pheu, ThanhNgang, tienDayDu, tienGon,
} from '@/components/bieu-do'

interface PhanBo {
  id: string | null
  ten: string | null
  doanhThu: number
  soDon: number
}

type LoaiThongKe = 'KhoaHoc' | 'SanPham' | 'Elearning' | 'DoiNhom'

const CAC_LOAI: LoaiThongKe[] = ['KhoaHoc', 'SanPham', 'Elearning', 'DoiNhom']

/**
 * Màu gắn với TỪNG LOẠI, cố định — không đổi theo thứ hạng hay số lượng.
 *
 * Người dùng học được "khoá học màu xanh lá" thì nó phải xanh lá ở mọi kỳ, mọi bộ lọc. Đổi màu
 * theo thứ hạng là cách chắc chắn làm người đọc hiểu sai.
 */
const MAU_LOAI: Record<LoaiThongKe, string> = {
  KhoaHoc: 'hsl(var(--chart-1))',
  SanPham: 'hsl(var(--chart-2))',
  Elearning: 'hsl(var(--chart-3))',
  DoiNhom: 'hsl(var(--chart-4))',
}

interface MucLoc {
  id: string
  ten: string
}

interface SoLieuElearning {
  soKhoa: number
  soNguoiHoc: number
  soLuotGhiDanh: number
  soBaiHoanThanh: number
  tongLuotCanHoc: number
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
  theoLoai: PhanBo[]
  danhMucLoc: MucLoc[]
  elearning: SoLieuElearning | null
}

/** `yyyy-MM-dd` theo giờ ĐỊA PHƯƠNG — `toISOString()` quy về UTC nên lùi một ngày ở UTC+7. */
const ngayISO = (d: Date) =>
  `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`

/**
 * Mặc định: 12 tháng gần nhất, tính từ ngày 1 của tháng đầu kỳ.
 *
 * Vẫn cần mặc định dù đã cho chọn tay (16/09/2026): mở trang ra phải có số ngay, và **ô số
 * "so kỳ trước" suy độ dài kỳ từ chính khoảng này** — để trống hai đầu thì không so được với
 * gì cả.
 */
const KHOANG_MAC_DINH = () => {
  const nay = new Date()
  const tu = new Date(nay.getFullYear(), nay.getMonth() - 11, 1)
  return { tuNgay: ngayISO(tu), denNgay: ngayISO(nay) }
}

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
  const macDinh = KHOANG_MAC_DINH()
  const [tuNgay, setTuNgay] = useState(macDinh.tuNgay)
  const [denNgay, setDenNgay] = useState(macDinh.denNgay)
  const [loai, setLoai] = useState<LoaiThongKe>('KhoaHoc')
  const [chiMuc, setChiMuc] = useState<string[]>([])

  /*
    Mốc gửi lên API.

    `denNgay` phải là **NGÀY HÔM SAU**, không phải chính ngày người dùng chọn: handler thống kê
    so `NgayDangKy < den` (khác màn Doanh thu dùng `<= denNgay`, nên bên đó gắn `T23:59:59Z`).
    Gửi thẳng ngày đã chọn thì **mất trọn ngày cuối kỳ** — chọn "đến 16/09" mà đơn ngày 16/09
    không được tính, một lỗi lệch một ngày không có gì báo.

    Gửi dạng `yyyy-MM-dd` (không kèm giờ/offset) để backend diễn giải theo **múi giờ trung tâm**
    — nó đã làm đúng việc đó cho giá trị mặc định. Gắn `Z` vào đây sẽ cắt kỳ theo UTC và đẩy đơn
    sáng sớm sang kỳ trước (đúng bài học FR-15 ghi trong `ThongKeCrmDtos`).
  */
  const denGuiLen = (() => {
    if (!denNgay) return undefined
    const d = new Date(`${denNgay}T00:00:00`)
    d.setDate(d.getDate() + 1)
    return ngayISO(d)
  })()

  /** Người dùng đảo hai đầu thì không gọi API với khoảng âm — báo ngay trên màn. */
  const khoangSai = !!tuNgay && !!denNgay && tuNgay > denNgay

  const { data: tk, isLoading } = useQuery({
    queryKey: ['thong-ke-crm', tuNgay, denGuiLen, loai, chiMuc],
    enabled: !khoangSai,
    queryFn: async () =>
      (await api.get<ThongKeCrmDto>('/thong-ke-crm', {
        params: { tuNgay: tuNgay || undefined, denNgay: denGuiLen, loai, chiMuc },
        // Mảng id gửi thành `chiMuc=a&chiMuc=b` — ASP.NET bind `List<Guid>` theo dạng này.
        paramsSerializer: { indexes: null },
      })).data,
  })

  /*
    KHÔNG `return` sớm khi chưa có dữ liệu.

    Trước 16/09/2026 màn này `return <TrangTrong/>` khi `tk` rỗng — hợp lý hồi bộ lọc thời gian
    là ô chọn sẵn (luôn có giá trị hợp lệ). Nhưng từ khi cho **nhập tay từ ngày / đến ngày**, một
    khoảng sai sẽ làm cả trang biến thành "Không tìm thấy dữ liệu" — **kể cả bộ lọc**, nên người
    dùng không còn ô nào để sửa lại và phải F5. Đã gặp thật lúc kiểm chứng.

    Nay bộ lọc LUÔN render; chỉ phần thân mới phụ thuộc `tk`.
  */
  const delta =
    tk?.doanhThuKyTruoc && tk.doanhThuKyTruoc > 0
      ? ((tk.tongDoanhThu - tk.doanhThuKyTruoc) / tk.doanhThuKyTruoc) * 100
      : undefined

  const conThieu = tk ? tk.tongDoanhThu - tk.daThu : 0

  /** Tên hiển thị của một lát; null = nhóm chưa xác định (đơn cũ, người chưa gán phòng ban). */
  /**
   * Nhãn một cột/lát của biểu đồ.
   *
   * `KHAC` là **mã**, không phải tên phòng: biểu đồ đội nhóm gom mọi đơn KHÔNG thuộc phòng tag
   * Kinh doanh vào một mục (16/09/2026) — gom chứ không ẩn, để tổng biểu đồ vẫn khớp ô "tổng
   * doanh thu" ngay trên nó. Backend trả mã để frontend dịch (quy tắc #3).
   */
  const ten = (p: PhanBo) =>
    p.ten === 'KHAC' ? t('tagVaiTro.KHAC') : (p.ten ?? t('thongKe.khongXacDinh'))

  const hang = (ds: PhanBo[]) =>
    ds.map((p) => ({
      nhan: ten(p),
      giaTri: p.doanhThu,
      phu: t('thongKe.soDonNgan', { so: p.soDon }),
    }))

  return (
    <div className="grid gap-4">
      {/* Bộ lọc trên MỘT hàng, ngay trên các biểu đồ — đổi bộ lọc là đổi mọi số bên dưới. */}
      <div className="flex flex-wrap items-end gap-3">
        <div className="w-48">
          <Label>{t('thongKe.loaiThongKe')}</Label>
          <SelectTimKiem
            giaTri={loai}
            luaChon={CAC_LOAI.map((l) => ({ giaTri: l, nhan: t(`thongKe.loai.${l}`) }))}
            onDoi={(v) => {
              setLoai((v as LoaiThongKe) ?? 'KhoaHoc')
              // Đổi loại thì bỏ lọc cũ: id khoá học không có nghĩa gì ở danh sách sản phẩm,
              // giữ lại sẽ lọc ra rỗng mà người dùng không hiểu vì sao.
              setChiMuc([])
            }}
          />
        </div>

        {/*
          Từ ngày / đến ngày — giống màn Khách hàng và Doanh thu (yêu cầu 16/09/2026).

          Thay cho ô chọn sẵn "12 tháng / 6 tháng / 3 tháng / tháng này": người dùng cần xem
          đúng một kỳ kế toán hay một đợt tuyển sinh, mà không preset nào khớp. Ba màn CRM giờ
          cùng một cách lọc thời gian — cùng nhãn, cùng kiểu ô, cùng bề rộng.

          Dùng nhãn của màn Doanh thu ("Từ ngày"/"Đến ngày"), KHÔNG của màn Khách hàng
          ("Tạo hồ sơ từ"): thống kê lọc theo **ngày đăng ký đơn**, không theo ngày tạo hồ sơ
          khách — hai mốc khác nhau, mượn nhãn sai sẽ nói sai về con số bên dưới.
        */}
        <div className="w-40">
          <Label htmlFor="tk-tu-ngay">{t('doanhThu.tuNgay')}</Label>
          <Input
            id="tk-tu-ngay"
            type="date"
            value={tuNgay}
            max={denNgay || undefined}
            onChange={(e) => setTuNgay(e.target.value)}
          />
        </div>

        <div className="w-40">
          <Label htmlFor="tk-den-ngay">{t('doanhThu.denNgay')}</Label>
          <Input
            id="tk-den-ngay"
            type="date"
            value={denNgay}
            min={tuNgay || undefined}
            onChange={(e) => setDenNgay(e.target.value)}
          />
        </div>

        {/* Elearning không lọc theo danh sách: nó không đụng tới đơn hàng. */}
        {loai !== 'Elearning' && (tk?.danhMucLoc.length ?? 0) > 0 && (
          <div className="min-w-64 flex-1">
            <Label>{t(`thongKe.locTheo.${loai}`)}</Label>
            <SelectTimKiemNhieu
              giaTri={chiMuc}
              luaChon={(tk?.danhMucLoc ?? []).map((m) => ({ giaTri: m.id, nhan: m.ten }))}
              onDoi={setChiMuc}
            />
          </div>
        )}

        {/*
          Đặt lại kỳ mặc định. Ô chọn sẵn cũ làm việc này bằng một cú bấm, nên bỏ nó đi mà không
          có đường về "12 tháng gần nhất" là khiến việc thường gặp nhất thành việc phải gõ tay.
          Chỉ hiện khi đang lệch khỏi mặc định.
        */}
        {(tuNgay !== macDinh.tuNgay || denNgay !== macDinh.denNgay) && (
          <Button
            variant="outline"
            size="sm"
            onClick={() => { setTuNgay(macDinh.tuNgay); setDenNgay(macDinh.denNgay) }}
          >
            {t('thongKe.kyMacDinh')}
          </Button>
        )}

        {chiMuc.length > 0 && (
          <Button variant="outline" size="sm" onClick={() => setChiMuc([])}>
            {t('thongKe.boLoc')}
          </Button>
        )}
      </div>

      {/* Khoảng đảo đầu: nói ngay, không gọi API rồi hiện "không có dữ liệu" gây hiểu sai. */}
      {khoangSai && <CanhBaoLoi>{t('thongKe.khoangSai')}</CanhBaoLoi>}

      {chiMuc.length > 0 && (
        <p className="text-xs text-muted-foreground">
          {t('thongKe.dangLoc', { so: chiMuc.length })}
        </p>
      )}

      {isLoading && <TrangTrong thongDiep={t('chung.dangTai')} />}
      {!isLoading && !tk && !khoangSai && (
        <TrangTrong thongDiep={t('loi.KHONG_TIM_THAY')} />
      )}

      {tk && (
        <>
      {/*
        Hàng ô số đổi theo loại. Elearning KHÔNG hiện số tiền — hiện "tổng doanh thu" ngay trên
        dòng chữ "không đo tiền" là tự mâu thuẫn, và người đọc sẽ tưởng 17 triệu kia là doanh
        thu của khoá trực tuyến (thấy được khi chụp màn hình thật 14/09/2026).
      */}
      {loai === 'Elearning' ? (
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <OSo
            nhan={t('thongKe.soKhoaOnline')}
            giaTri={String(tk.elearning?.soKhoa ?? 0)}
            nhanMau={MAU_LOAI.Elearning}
          />
          <OSo nhan={t('thongKe.soNguoiHoc')} giaTri={String(tk.elearning?.soNguoiHoc ?? 0)} />
          <OSo
            nhan={t('thongKe.soLuotGhiDanh')}
            giaTri={String(tk.elearning?.soLuotGhiDanh ?? 0)}
          />
          <OSo
            nhan={t('thongKe.tyLeHoanThanh')}
            giaTri={
              !tk.elearning || tk.elearning.tongLuotCanHoc === 0
                ? '—'
                : `${Math.round(
                    (tk.elearning.soBaiHoanThanh / tk.elearning.tongLuotCanHoc) * 100,
                  )}%`
            }
            phu={
              tk.elearning && tk.elearning.tongLuotCanHoc > 0
                ? t('thongKe.baiTrenTong', {
                    da: tk.elearning.soBaiHoanThanh,
                    tong: tk.elearning.tongLuotCanHoc,
                  })
                : undefined
            }
          />
        </div>
      ) : (
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <OSo
            nhan={t('thongKe.tongDoanhThu')}
            giaTri={tienGon(tk.tongDoanhThu)}
            phu={delta !== undefined ? t('thongKe.soKyTruoc') : t('thongKe.chuaCoKyTruoc')}
            deltaPhanTram={delta}
            nhanMau={MAU_LOAI[loai]}
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
      )}

      {/* Đường doanh thu cũng ẩn với Elearning — cùng lẽ với hàng ô số. */}
      {loai !== 'Elearning' && (
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
      )}

      <div className="grid gap-4 lg:grid-cols-2">
        {/* Khối CHÍNH — đổi theo loại đang chọn. */}
        {loai === 'Elearning' ? (
          <KhungBieuDo
            tieuDe={t('thongKe.loai.Elearning')}
            moTa={t('thongKe.elearningMoTa')}
            trong={
              (tk.elearning?.soKhoa ?? 0) === 0 ? t('thongKe.chuaCoKhoaMo') : null
            }
            className="lg:col-span-2"
          >
            <p className="text-sm text-muted-foreground">
              {t('thongKe.elearningGoiY')}
            </p>
          </KhungBieuDo>
        ) : (
          <KhungBieuDo
            tieuDe={t(`thongKe.loai.${loai}`)}
            moTa={t(`thongKe.loaiMoTa.${loai}`)}
            trong={tk.theoLoai.length === 0 ? t('thongKe.chuaCoDon') : null}
          >
            <ThanhNgang hang={hang(tk.theoLoai)} mau={MAU_LOAI[loai]} />
          </KhungBieuDo>
        )}

        <KhungBieuDo
          tieuDe={t('thongKe.theoCaNhan')}
          moTa={t('thongKe.theoCaNhanMoTa')}
          trong={tk.theoCaNhan.length === 0 ? t('thongKe.chuaCoDon') : null}
        >
          <ThanhNgang hang={hang(tk.theoCaNhan)} />
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
        </>
      )}
    </div>
  )
}
