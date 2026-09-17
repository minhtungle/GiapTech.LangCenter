import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ExternalLink, Pencil, Plus, SlidersHorizontal, Trash2 } from 'lucide-react'
import { api, layMaLoi, trangRong, type KetQuaTrang } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Card, CardContent, Input, Label, Table, Td, Textarea, Th,
  TrangTrong,
} from '@/components/ui'
import { Modal } from '@/components/ui/Modal'
import { PhanTrang } from '@/components/ui/PhanTrang'
import { MenuThaoTac } from '@/components/ui/MenuThaoTac'
import { SelectTimKiem } from '@/components/ui/SelectTimKiem'
import { useQuyen } from '@/lib/quyen'
import { useXacNhan } from '@/lib/xacNhan'
import {
  CAC_DON_VI, CAC_PHUONG_THUC, mauPhanTram, ngayChoInput, ngayVN, phanTram, tien,
  type DangKyDto, type DonViTien, type KhachHangDto, type KhoaHocDto,
  type PhuongThucThanhToan, type SanPhamDto, type TongHopDoanhThuDto,
  type LoaiDonHang,
} from './crmTypes'
import { LocDoiNhom } from '@/components/crm/LocDoiNhom'

/**
 * FR-18 — doanh thu: đăng ký khoá học (CRM).
 *
 * Ba con số tiền, không phải một: **giá gốc** (snapshot lúc đăng ký), **số tiền** thực thu, và
 * **% trên giá gốc** tính động. Xem `docs/nghiep-vu/crm.md`.
 */
export default function DoanhThu() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { coQuyen } = useQuyen()
  const { hoi, hop } = useXacNhan()

  const [trang, setTrang] = useState(1)
  const [soDong, setSoDong] = useState(20)
  const [timKiem, setTimKiem] = useState('')
  const [locKhoa, setLocKhoa] = useState<string | null>(null)
  const [locSanPham, setLocSanPham] = useState<string | null>(null)
  const [phongBanId, setPhongBanId] = useState<string | null>(null)
  const [nhanVienId, setNhanVienId] = useState<string | null>(null)
  const [locPhuongThuc, setLocPhuongThuc] = useState<string | null>(null)
  const [moLocThem, setMoLocThem] = useState(false)
  const [tuNgay, setTuNgay] = useState('')
  const [denNgay, setDenNgay] = useState('')

  const [moForm, setMoForm] = useState(false)
  const [dangSua, setDangSua] = useState<DangKyDto | null>(null)
  const [khachId, setKhachId] = useState<string | null>(null)
  /*
    Đơn hàng có HAI loại mặt hàng (FR-20): khoá học và sản phẩm.

    Trước 17/09/2026 màn này chỉ gửi `khoaHocId`, nên **không ghi được đơn sản phẩm** và
    **sửa đơn sản phẩm thì 400** (`PHAI_CHON_DUNG_MOT_MAT_HANG`) — trên dữ liệu thật có 10/85
    đơn như vậy, tức kế toán không sửa nổi một lỗi gõ trong đó. Màn Khách hàng thì làm được cả
    hai, nên hai màn lệch nhau cả về việc làm được lẫn cách gọi tên.
  */
  const [loaiDon, setLoaiDon] = useState<LoaiDonHang>('KhoaHoc')
  const [matHangId, setMatHangId] = useState<string | null>(null)
  const [soLuong, setSoLuong] = useState('1')
  const [soTien, setSoTien] = useState('')
  const [donVi, setDonVi] = useState<DonViTien>('VND')
  const [tyGia, setTyGia] = useState('1')
  const [phuongThuc, setPhuongThuc] = useState<PhuongThucThanhToan>('ChuyenKhoan')
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [maLoiBang, setMaLoiBang] = useState<string | null>(null)

  // MỘT chỗ khai tham số cho cả danh sách và tổng hợp. Tách ra hai chỗ là con số tổng sẽ
  // không khớp bảng bên dưới nó, mà người dùng lại tin con số tổng.
  const thamSo = {
    timKiem: timKiem || undefined,
    khoaHocId: locKhoa || undefined,
    sanPhamId: locSanPham || undefined,
    phongBanId: phongBanId || undefined,
    nhanVienId: nhanVienId || undefined,
    phuongThuc: locPhuongThuc || undefined,
    tuNgay: tuNgay || undefined,
    denNgay: denNgay ? `${denNgay}T23:59:59Z` : undefined,
  }

  const { data: kq = trangRong<DangKyDto>(), isLoading } = useQuery({
    queryKey: ['doanh-thu', thamSo, trang, soDong],
    queryFn: async () =>
      (await api.get<KetQuaTrang<DangKyDto>>('/doanh-thu', {
        params: { ...thamSo, trang, soDong },
      })).data,
  })

  /**
   * Tổng hợp trên TOÀN BỘ tập lọc — endpoint riêng, không cộng trên trang đang xem.
   *
   * Cộng 20 dòng đầu của 500 đơn ra một số vô nghĩa mà người dùng rất dễ tin là tổng thật.
   */
  const { data: tongHop } = useQuery({
    queryKey: ['doanh-thu-tong-hop', thamSo],
    queryFn: async () =>
      (await api.get<TongHopDoanhThuDto>('/doanh-thu/tong-hop', { params: thamSo })).data,
  })

  const { data: khachs = [] } = useQuery({
    queryKey: ['khach-hang-ngan'],
    queryFn: async () =>
      (await api.get<KetQuaTrang<KhachHangDto>>('/khach-hang', { params: { soDong: 200 } }))
        .data.duLieu,
    enabled: coQuyen('KhachHang'),
  })

  const { data: khoas = [] } = useQuery({
    queryKey: ['khoa-hoc-ngan'],
    queryFn: async () =>
      (await api.get<KetQuaTrang<KhoaHocDto>>('/khoa-hoc', { params: { soDong: 200 } }))
        .data.duLieu,
    enabled: coQuyen('KhoaHoc'),
  })

  // Danh sách sản phẩm để LỌC. Màn này trước chỉ lọc được theo khoá học, dù đơn hàng chứa cả
  // hai loại (khoá và sản phẩm) từ FR-20 — nên doanh thu bán sách không tra cứu riêng được.
  const { data: sanPhams = [] } = useQuery({
    queryKey: ['san-pham-ngan'],
    queryFn: async () =>
      (await api.get<KetQuaTrang<SanPhamDto>>('/san-pham', { params: { soDong: 200 } }))
        .data.duLieu,
    enabled: coQuyen('SanPham'),
  })

  /** Danh mục theo loại đang chọn — hai danh mục hoàn toàn khác nhau, không gộp một ô. */
  const dsMatHang = loaiDon === 'KhoaHoc' ? khoas : sanPhams
  const matHangChon = dsMatHang.find((x) => x.id === matHangId)

  /**
   * Chọn mặt hàng → điền sẵn giá và đơn vị của nó, người bán sửa được.
   *
   * Chỉ làm khi TẠO MỚI: sửa đơn cũ mà tự điền lại giá hôm nay sẽ ghi đè mức đã chốt với khách.
   */
  useEffect(() => {
    if (dangSua || !matHangChon) return
    // Sản phẩm: giá × số lượng. Khoá học luôn 1 suất nên nhân lên cũng không đổi.
    const sl = loaiDon === 'SanPham' ? Math.max(1, Number(soLuong) || 1) : 1
    setSoTien(String(matHangChon.giaTien * sl))
    setDonVi(matHangChon.donViTien)
    setTyGia(matHangChon.donViTien === 'VND' ? '1' : '')
  }, [matHangId, soLuong, loaiDon, dangSua, matHangChon])

  // VND thì tỷ giá luôn 1 — backend cũng ép, đây chỉ để UI không hỏi một câu vô nghĩa.
  useEffect(() => {
    if (donVi === 'VND') setTyGia('1')
  }, [donVi])

  /** Giá gốc để tính %: đơn đang sửa dùng giá đã chụp, đơn mới dùng giá niêm yết hiện tại. */
  const giaGoc = dangSua
    ? dangSua.giaGoc
    : (matHangChon?.giaTien ?? 0) * (loaiDon === 'SanPham' ? Math.max(1, Number(soLuong) || 1) : 1)
  const soTienSo = Number(soTien) || 0
  const ptXemTruoc = giaGoc === 0 ? null : (soTienSo / giaGoc) * 100
  const quyDoiXemTruoc = soTienSo * (Number(tyGia) || 0)

  const lamMoi = () => {
    void qc.invalidateQueries({ queryKey: ['doanh-thu'] })
    void qc.invalidateQueries({ queryKey: ['doanh-thu-tong-hop'] })
    // Cột "số khoá"/"tổng mua" ở màn Khách hàng đổi theo.
    void qc.invalidateQueries({ queryKey: ['khach-hang'] })
    void qc.invalidateQueries({ queryKey: ['khoa-hoc'] })
  }

  const dong = () => {
    setMoForm(false)
    setDangSua(null)
    setMaLoi(null)
  }

  const luu = useMutation({
    mutationFn: async (fd: FormData) => {
      const than = {
        khachHangId: khachId,
        // ĐÚNG MỘT trong hai có giá trị — validator backend chặn cả "không có" lẫn "có cả hai".
        khoaHocId: loaiDon === 'KhoaHoc' ? matHangId : null,
        sanPhamId: loaiDon === 'SanPham' ? matHangId : null,
        soLuong: loaiDon === 'SanPham' ? Math.max(1, Number(soLuong) || 1) : 1,
        soTien: soTienSo,
        donViTien: donVi,
        tyGiaVeVnd: donVi === 'VND' ? 1 : Number(tyGia),
        ngayDangKy: `${String(fd.get('ngayDangKy'))}T00:00:00Z`,
        phuongThuc,
        ghiChu: String(fd.get('ghiChu') ?? '').trim() || null,
      }
      if (dangSua) await api.put(`/doanh-thu/${dangSua.id}`, { ...than, id: dangSua.id })
      else await api.post('/doanh-thu', than)
    },
    onSuccess: () => {
      lamMoi()
      dong()
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const xoa = useMutation({
    mutationFn: (id: string) => api.delete(`/doanh-thu/${id}`),
    onSuccess: lamMoi,
    onError: (e) => setMaLoiBang(layMaLoi(e)),
  })

  const moSua = (d: DangKyDto) => {
    setDangSua(d)
    setKhachId(d.khachHangId)
    // Điền lại ĐÚNG loại và mặt hàng của đơn — đơn sản phẩm trước đây rơi vào nhánh khoá học
    // rồi gửi lên `khoaHocId = null`, nhận 400 và không sửa được.
    setLoaiDon(d.loai)
    setMatHangId(d.loai === 'KhoaHoc' ? d.khoaHocId : d.sanPhamId)
    setSoLuong(String(d.soLuong))
    setSoTien(String(d.soTien))
    setDonVi(d.donViTien)
    setTyGia(String(d.tyGiaVeVnd))
    setPhuongThuc(d.phuongThuc)
    setMaLoi(null)
    setMoForm(true)
  }

  const moThem = () => {
    setDangSua(null)
    setKhachId(null)
    setLoaiDon('KhoaHoc')
    setMatHangId(null)
    setSoLuong('1')
    setSoTien('')
    setDonVi('VND')
    setTyGia('1')
    setPhuongThuc('ChuyenKhoan')
    setMaLoi(null)
    setMoForm(true)
  }

  /**
   * Số bộ lọc NÂNG CAO đang bật. Không đếm các ô luôn hiện trên hàng đầu (tìm kiếm, khoá học,
   * khoảng ngày) — người dùng tự thấy chúng, đếm vào làm số trên nút không khớp panel.
   */
  const soLocDangBat = [phongBanId, nhanVienId, locSanPham, locPhuongThuc].filter(Boolean).length

  const xoaLoc = () => {
    setPhongBanId(null)
    setNhanVienId(null)
    setLocSanPham(null)
    setLocPhuongThuc(null)
    setTrang(1)
  }

  return (
    <div className="space-y-4">
      {/* Tổng hợp: con số duy nhất cộng được khi có nhiều đơn vị tiền là VND đã quy đổi. */}
      {tongHop && (
        <div className="grid gap-3 sm:grid-cols-3">
          <Card>
            <CardContent className="pt-6">
              <p className="text-xs uppercase tracking-wide text-muted-foreground">
                {t('doanhThu.tongDoanhThu')}
              </p>
              <p className="mt-1 text-2xl font-semibold">{tien(tongHop.tongVnd)}</p>
              {tongHop.theoDonVi.length > 1 && (
                <p className="mt-1 text-xs text-muted-foreground">
                  {tongHop.theoDonVi
                    .map((x) => `${tien(x.tong, x.donViTien)}`)
                    .join(' · ')}
                </p>
              )}
            </CardContent>
          </Card>
          <Card>
            <CardContent className="pt-6">
              <p className="text-xs uppercase tracking-wide text-muted-foreground">
                {t('doanhThu.soDangKy')}
              </p>
              <p className="mt-1 text-2xl font-semibold">{tongHop.soDangKy}</p>
            </CardContent>
          </Card>
          <Card>
            <CardContent className="pt-6">
              <p className="text-xs uppercase tracking-wide text-muted-foreground">
                {t('doanhThu.soKhachHang')}
              </p>
              <p className="mt-1 text-2xl font-semibold">{tongHop.soKhachHang}</p>
            </CardContent>
          </Card>
        </div>
      )}

      <div className="flex flex-wrap items-end justify-between gap-3">
        {/* `min-w-0 flex-1`: hàng lọc co giãn theo chỗ còn lại, nên nút hành động chính bám
            bên phải cùng hàng thay vì bị đẩy xuống dòng riêng — ở đó nó trông như một phần
            của panel lọc. */}
        <div className="flex min-w-0 flex-1 flex-wrap items-end gap-3">
          <div className="w-56">
            <Label htmlFor="tim">{t('chung.timKiem')}</Label>
            <Input
              id="tim"
              value={timKiem}
              onChange={(e) => {
                setTimKiem(e.target.value)
                setTrang(1)
              }}
              placeholder={t('doanhThu.timTheo')}
            />
          </div>
          <div className="w-52">
            <Label htmlFor="loc-khoa">{t('doanhThu.khoaHoc')}</Label>
            <SelectTimKiem
              id="loc-khoa"
              luaChon={khoas.map((k) => ({ giaTri: k.id, nhan: k.ten }))}
              giaTri={locKhoa}
              onDoi={(v) => {
                setLocKhoa(v)
                setTrang(1)
              }}
              placeholder={t('chung.tatCa')}
            />
          </div>
          <div className="w-40">
            <Label htmlFor="tu-ngay">{t('doanhThu.tuNgay')}</Label>
            <Input
              id="tu-ngay"
              type="date"
              value={tuNgay}
              onChange={(e) => {
                setTuNgay(e.target.value)
                setTrang(1)
              }}
            />
          </div>
          <div className="w-40">
            <Label htmlFor="den-ngay">{t('doanhThu.denNgay')}</Label>
            <Input
              id="den-ngay"
              type="date"
              value={denNgay}
              onChange={(e) => {
                setDenNgay(e.target.value)
                setTrang(1)
              }}
            />
          </div>

          {/* Bốn ô lọc nữa bày hết ra hàng này thì vỡ bố cục — ẩn sau nút (nguyên tắc UI/UX
              mục 1). Số trên nút cho biết còn bộ lọc đang bật khi panel đã đóng. */}
          <Button
            variant="outline"
            onClick={() => setMoLocThem((x) => !x)}
            aria-expanded={moLocThem}
          >
            <SlidersHorizontal className="h-4 w-4" />
            {t('crmLoc.locThem')}
            {soLocDangBat > 0 && (
              <span className="ml-1 rounded-full bg-primary px-1.5 text-xs text-primary-foreground">
                {soLocDangBat}
              </span>
            )}
          </Button>

          {soLocDangBat > 0 && (
            <Button variant="ghost" onClick={xoaLoc}>
              {t('crmLoc.xoaLoc')}
            </Button>
          )}
        </div>

        {coQuyen('DoanhThu', 'Them') && (
          <Button onClick={moThem}>
            <Plus className="h-4 w-4" />
            {t('donHang.ghiDon')}
          </Button>
        )}
      </div>

      {moLocThem && (
        <div className="rounded-lg border border-border bg-muted/30 p-3">
          <div className="flex flex-wrap items-end gap-3">
            <LocDoiNhom
              phongBanId={phongBanId}
              nhanVienId={nhanVienId}
              onDoiPhongBan={(v) => { setPhongBanId(v); setTrang(1) }}
              onDoiNhanVien={(v) => { setNhanVienId(v); setTrang(1) }}
            />

            <div className="w-52">
              <Label htmlFor="loc-sp">{t('crmLoc.sanPham')}</Label>
              <SelectTimKiem
                id="loc-sp"
                luaChon={sanPhams.map((x) => ({ giaTri: x.id, nhan: x.ten }))}
                giaTri={locSanPham}
                onDoi={(v) => { setLocSanPham(v); setTrang(1) }}
                placeholder={t('chung.tatCa')}
              />
            </div>

            <div className="w-44">
              <Label htmlFor="loc-pt">{t('crmLoc.hinhThuc')}</Label>
              <SelectTimKiem
                id="loc-pt"
                luaChon={CAC_PHUONG_THUC.map((x) => ({
                  giaTri: x, nhan: t(`phuongThucThanhToan.${x}`),
                }))}
                giaTri={locPhuongThuc}
                onDoi={(v) => { setLocPhuongThuc(v); setTrang(1) }}
                placeholder={t('chung.tatCa')}
              />
            </div>
          </div>

          {/* Nói rõ mốc quy đơn: người đọc cần biết "đội nhóm" nghĩa là đội của người MANG
              KHÁCH VỀ, không phải người nhập đơn — nếu không họ sẽ đối chiếu sai với sổ tay. */}
          <p className="mt-2.5 text-xs text-muted-foreground">
            {t('crmLoc.theoNguoiMangKhach')}
          </p>
        </div>
      )}

      {maLoiBang && <CanhBaoLoi>{t(`loi.${maLoiBang}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      <Card>
        <CardContent className="pt-6">
          {isLoading ? (
            <TrangTrong thongDiep={t('chung.dangTai')} />
          ) : kq.duLieu.length === 0 ? (
            <TrangTrong thongDiep={t('donHang.chuaCo')} />
          ) : (
            <div className="overflow-x-auto">
              <Table>
                <thead>
                  <tr>
                    <Th>{t('doanhThu.khachHang')}</Th>
                    <Th>{t('doanhThu.matHang')}</Th>
                    <Th className="text-right">{t('doanhThu.giaGoc')}</Th>
                    <Th className="text-right">{t('doanhThu.soTien')}</Th>
                    <Th className="text-right">{t('doanhThu.phanTram')}</Th>
                    <Th className="text-right">{t('doanhThu.quyDoiVnd')}</Th>
                    <Th>{t('doanhThu.ngayDangKy')}</Th>
                    <Th>{t('khachHang.phuongThuc')}</Th>
                    <Th />
                  </tr>
                </thead>
                <tbody>
                  {kq.duLieu.map((d) => (
                    <tr key={d.id} className="hover:bg-muted/40">
                      <Td>
                        <div className="font-medium">{d.tenKhachHang}</div>
                        <div className="flex gap-2 text-xs text-muted-foreground">
                          {d.soDienThoai && <span>{d.soDienThoai}</span>}
                          {d.linkFacebook && (
                            <a
                              href={d.linkFacebook}
                              target="_blank"
                              rel="noreferrer noopener"
                              className="inline-flex items-center gap-0.5 text-primary hover:underline"
                            >
                              FB
                              <ExternalLink className="h-3 w-3" />
                            </a>
                          )}
                        </div>
                      </Td>
                      <Td>
                        <div className="flex items-center gap-1.5">
                          <span>{d.tenMatHang}</span>
                          {/* Nhãn loại: bảng trộn khoá học và sản phẩm nên phải phân biệt được
                              ngay, không bắt người đọc suy từ cột "số buổi" có trống hay không. */}
                          <Badge variant={d.loai === 'KhoaHoc' ? 'accent' : 'muted'}>
                            {t(`loaiDonHang.${d.loai}`)}
                          </Badge>
                        </div>
                        <div className="text-xs text-muted-foreground">
                          {d.soBuoi !== null
                            ? t('khoaHoc.soBuoiNgan', { so: d.soBuoi })
                            : t('doanhThu.soLuongNgan', { so: d.soLuong })}
                        </div>
                      </Td>
                      <Td className="text-right text-muted-foreground">
                        {tien(d.giaGoc, d.donViTien)}
                      </Td>
                      <Td className="text-right font-medium">{tien(d.soTien, d.donViTien)}</Td>
                      <Td className="text-right">
                        <Badge variant={mauPhanTram(d.phanTramTrenGiaGoc)}>
                          {phanTram(d.phanTramTrenGiaGoc)}
                        </Badge>
                      </Td>
                      <Td className="text-right text-muted-foreground">
                        {tien(d.quyDoiVnd)}
                        {d.donViTien !== 'VND' && (
                          <div className="text-xs">
                            {t('doanhThu.tyGiaNgan', { ty: d.tyGiaVeVnd.toLocaleString('vi-VN') })}
                          </div>
                        )}
                      </Td>
                      <Td className="text-muted-foreground">{ngayVN(d.ngayDangKy)}</Td>
                      <Td className="text-muted-foreground">
                        {t(`phuongThucThanhToan.${d.phuongThuc}`)}
                      </Td>
                      <Td>
                        <div className="flex justify-end">
                          <MenuThaoTac
                            nhanMo={t('chung.thaoTac')}
                            muc={[
                              {
                                nhan: t('chung.sua'),
                                icon: Pencil,
                                an: !coQuyen('DoanhThu', 'Sua'),
                                onChon: () => moSua(d),
                              },
                              {
                                nhan: t('chung.xoa'),
                                icon: Trash2,
                                nguyHiem: true,
                                ngatNhom: true,
                                an: !coQuyen('DoanhThu', 'Xoa'),
                                onChon: () =>
                                  hoi({
                                    tieuDe: t('chung.xacNhanXoa'),
                                    thongDiep: t('doanhThu.hoiXoa', {
                                      ten: d.tenKhachHang,
                                      khoa: d.tenMatHang,
                                    }),
                                    nhanDongY: t('chung.xoa'),
                                    nguyHiem: true,
                                    onDongY: () => xoa.mutate(d.id),
                                  }),
                              },
                            ]}
                          />
                        </div>
                      </Td>
                    </tr>
                  ))}
                </tbody>
              </Table>

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
            </div>
          )}
        </CardContent>
      </Card>

      <Modal
        mo={moForm}
        onDong={dong}
        chanDoiKhiXuLy={luu.isPending}
        tieuDe={dangSua ? t('donHang.suaDon') : t('donHang.ghiDon')}
        moTa={dangSua?.tenKhachHang}
        rong="md"
      >
        <form
          className="grid gap-3"
          onSubmit={(e) => {
            e.preventDefault()
            const fd = new FormData(e.currentTarget)
            hoi({
              tieuDe: t('chung.xacNhanLuu'),
              thongDiep: t('donHang.hoiLuu'),
              onDongY: () => luu.mutate(fd),
            })
          }}
        >
          <div>
            <Label htmlFor="khach">{t('doanhThu.khachHang')} *</Label>
            <SelectTimKiem
              id="khach"
              luaChon={khachs.map((k) => ({
                giaTri: k.id,
                nhan: k.soDienThoai ? `${k.hoTen} · ${k.soDienThoai}` : k.hoTen,
              }))}
              giaTri={khachId}
              onDoi={setKhachId}
              placeholder={t('doanhThu.chonKhach')}
            />
          </div>

          {/* Chọn LOẠI trước — cùng bố cục với form ở màn Khách hàng để hai chỗ thao tác như nhau. */}
          <div>
            <Label>{t('donHang.loaiMatHang')} *</Label>
            <div className="mt-1 flex gap-1 rounded-lg border border-border p-1">
              {(['KhoaHoc', 'SanPham'] as LoaiDonHang[]).map((x) => (
                <button
                  key={x}
                  type="button"
                  // Đổi loại thì bỏ mặt hàng đang chọn: id khoá học không có nghĩa trong danh
                  // mục sản phẩm, giữ lại sẽ gửi lên một id không thuộc loại đã khai.
                  onClick={() => { setLoaiDon(x); setMatHangId(null) }}
                  className={
                    'flex-1 rounded-md px-3 py-1.5 text-sm font-medium transition-colors '
                    + (loaiDon === x
                      ? 'bg-primary text-primary-foreground'
                      : 'text-muted-foreground hover:bg-muted')
                  }
                >
                  {t(`loaiDonHang.${x}`)}
                </button>
              ))}
            </div>
          </div>

          <div>
            <Label htmlFor="mat-hang">
              {t(loaiDon === 'KhoaHoc' ? 'doanhThu.khoaHoc' : 'sanPham.tenSp')} *
            </Label>
            <SelectTimKiem
              id="mat-hang"
              luaChon={dsMatHang
                // Mặt hàng ngừng bán vẫn hiện khi SỬA đơn cũ đã dùng nó — ẩn đi thì ô trống trơn.
                .filter((x) => x.dangBan || x.id === matHangId)
                .map((x) => ({
                  giaTri: x.id,
                  nhan: `${x.ten} · ${tien(x.giaTien, x.donViTien)}`,
                }))}
              giaTri={matHangId}
              onDoi={setMatHangId}
              placeholder={t(loaiDon === 'KhoaHoc' ? 'doanhThu.chonKhoa' : 'donHang.chonSanPham')}
            />
            {/*
              Gợi ý giá niêm yết chỉ hiện với KHOÁ HỌC vì nó kèm số buổi — `SanPhamDto` không có
              trường đó (tsc bắt được khi tôi dùng chung một nhánh). Thu hẹp kiểu bằng `find`
              trên đúng danh mục, không ép kiểu.
            */}
            {!dangSua && loaiDon === 'KhoaHoc' && (() => {
              const k = khoas.find((x) => x.id === matHangId)
              return k ? (
                <p className="mt-1 text-xs text-muted-foreground">
                  {t('doanhThu.giaNiemYet', {
                    gia: tien(k.giaTien, k.donViTien),
                    so: k.soBuoi,
                  })}
                </p>
              ) : null
            })()}
          </div>

          {/* Số lượng chỉ có nghĩa với sản phẩm — khoá học không ai mua 2 suất trong một đơn. */}
          {loaiDon === 'SanPham' && (
            <div className="w-32">
              <Label htmlFor="soLuong">{t('donHang.soLuong')} *</Label>
              <Input
                id="soLuong"
                type="number"
                min={1}
                value={soLuong}
                onChange={(e) => setSoLuong(e.target.value)}
              />
            </div>
          )}

          <div className="grid gap-3 sm:grid-cols-3">
            <div>
              <Label htmlFor="soTien">{t('doanhThu.soTien')} *</Label>
              <Input
                id="soTien"
                type="number"
                min={0}
                step="0.01"
                required
                value={soTien}
                onChange={(e) => setSoTien(e.target.value)}
              />
            </div>
            <div>
              <Label htmlFor="donVi">{t('khoaHoc.donViTien')} *</Label>
              <SelectTimKiem
                id="donVi"
                luaChon={CAC_DON_VI.map((d) => ({ giaTri: d, nhan: t(`donViTien.${d}`) }))}
                giaTri={donVi}
                onDoi={(v) => setDonVi((v as DonViTien) ?? 'VND')}
                choPhepXoa={false}
              />
            </div>
            <div>
              <Label htmlFor="tyGia">{t('doanhThu.tyGia')} *</Label>
              <Input
                id="tyGia"
                type="number"
                min={0}
                step="0.000001"
                required
                // VND thì tỷ giá luôn 1 — khoá ô lại thay vì để người dùng nhập 25000.
                disabled={donVi === 'VND'}
                value={tyGia}
                onChange={(e) => setTyGia(e.target.value)}
              />
            </div>
          </div>

          {/* Xem trước % và quy đổi NGAY khi nhập — người bán thấy mình đang giảm bao nhiêu. */}
          <div className="flex flex-wrap items-center gap-4 rounded-md border border-border bg-muted/30 px-3 py-2 text-sm">
            <span className="text-muted-foreground">
              {t('doanhThu.giaGoc')}: <strong>{tien(giaGoc, donVi)}</strong>
            </span>
            <span className="text-muted-foreground">
              {t('doanhThu.phanTram')}:{' '}
              <Badge variant={mauPhanTram(ptXemTruoc)}>{phanTram(ptXemTruoc)}</Badge>
            </span>
            {donVi !== 'VND' && (
              <span className="text-muted-foreground">
                {t('doanhThu.quyDoiVnd')}: <strong>{tien(quyDoiXemTruoc)}</strong>
              </span>
            )}
          </div>
          <p className="text-xs text-muted-foreground">{t('doanhThu.tyGiaGoiY')}</p>

          <div className="grid gap-3 sm:grid-cols-2">
            <div>
              <Label htmlFor="ngayDangKy">{t('doanhThu.ngayDangKy')} *</Label>
              <Input
                id="ngayDangKy"
                name="ngayDangKy"
                type="date"
                required
                defaultValue={
                  dangSua
                    ? ngayChoInput(dangSua.ngayDangKy)
                    : new Date().toISOString().slice(0, 10)
                }
              />
            </div>
            <div>
              <Label htmlFor="phuongThuc">{t('khachHang.phuongThuc')}</Label>
              <SelectTimKiem
                id="phuongThuc"
                luaChon={CAC_PHUONG_THUC.map((p) => ({
                  giaTri: p,
                  nhan: t(`phuongThucThanhToan.${p}`),
                }))}
                giaTri={phuongThuc}
                onDoi={(v) => setPhuongThuc((v as PhuongThucThanhToan) ?? 'ChuyenKhoan')}
                choPhepXoa={false}
              />
            </div>
          </div>

          <div>
            <Label htmlFor="ghiChu">{t('chung.ghiChu')}</Label>
            <Textarea id="ghiChu" name="ghiChu" rows={2} defaultValue={dangSua?.ghiChu ?? ''} />
          </div>

          {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

          <div className="flex justify-end gap-2">
            <Button type="button" variant="outline" onClick={dong}>
              {t('chung.huy')}
            </Button>
            <Button type="submit" disabled={luu.isPending || !khachId || !matHangId}>
              {t('chung.luu')}
            </Button>
          </div>
        </form>
      </Modal>

      {hop}
    </div>
  )
}
