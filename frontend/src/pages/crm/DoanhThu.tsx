import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ExternalLink, Pencil, Plus, Trash2 } from 'lucide-react'
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
  type PhuongThucThanhToan, type TongHopDoanhThuDto,
} from './crmTypes'

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
  const [tuNgay, setTuNgay] = useState('')
  const [denNgay, setDenNgay] = useState('')

  const [moForm, setMoForm] = useState(false)
  const [dangSua, setDangSua] = useState<DangKyDto | null>(null)
  const [khachId, setKhachId] = useState<string | null>(null)
  const [khoaId, setKhoaId] = useState<string | null>(null)
  const [soTien, setSoTien] = useState('')
  const [donVi, setDonVi] = useState<DonViTien>('VND')
  const [tyGia, setTyGia] = useState('1')
  const [phuongThuc, setPhuongThuc] = useState<PhuongThucThanhToan>('ChuyenKhoan')
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [maLoiBang, setMaLoiBang] = useState<string | null>(null)

  const thamSo = {
    timKiem: timKiem || undefined,
    khoaHocId: locKhoa || undefined,
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

  const khoaChon = khoas.find((k) => k.id === khoaId)

  /**
   * Chọn khoá → điền sẵn giá và đơn vị của khoá đó, người bán sửa được.
   *
   * Chỉ làm khi TẠO MỚI: sửa đơn cũ mà tự điền lại giá hôm nay sẽ ghi đè mức đã chốt với khách.
   */
  useEffect(() => {
    if (dangSua || !khoaChon) return
    setSoTien(String(khoaChon.giaTien))
    setDonVi(khoaChon.donViTien)
    setTyGia(khoaChon.donViTien === 'VND' ? '1' : '')
  }, [khoaId, dangSua, khoaChon])

  // VND thì tỷ giá luôn 1 — backend cũng ép, đây chỉ để UI không hỏi một câu vô nghĩa.
  useEffect(() => {
    if (donVi === 'VND') setTyGia('1')
  }, [donVi])

  /** Giá gốc để tính %: đơn đang sửa dùng giá đã chụp, đơn mới dùng giá niêm yết hiện tại. */
  const giaGoc = dangSua ? dangSua.giaGoc : (khoaChon?.giaTien ?? 0)
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
        khoaHocId: khoaId,
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
    setKhoaId(d.khoaHocId)
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
    setKhoaId(null)
    setSoTien('')
    setDonVi('VND')
    setTyGia('1')
    setPhuongThuc('ChuyenKhoan')
    setMaLoi(null)
    setMoForm(true)
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
        <div className="flex flex-wrap items-end gap-3">
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
        </div>

        {coQuyen('DoanhThu', 'Them') && (
          <Button onClick={moThem}>
            <Plus className="h-4 w-4" />
            {t('doanhThu.them')}
          </Button>
        )}
      </div>

      {maLoiBang && <CanhBaoLoi>{t(`loi.${maLoiBang}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      <Card>
        <CardContent className="pt-6">
          {isLoading ? (
            <TrangTrong thongDiep={t('chung.dangTai')} />
          ) : kq.duLieu.length === 0 ? (
            <TrangTrong thongDiep={t('doanhThu.chuaCo')} />
          ) : (
            <div className="overflow-x-auto">
              <Table>
                <thead>
                  <tr>
                    <Th>{t('doanhThu.khachHang')}</Th>
                    <Th>{t('doanhThu.khoaHoc')}</Th>
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
                        <div>{d.tenKhoaHoc}</div>
                        <div className="text-xs text-muted-foreground">
                          {t('khoaHoc.soBuoiNgan', { so: d.soBuoi })}
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
                                      khoa: d.tenKhoaHoc,
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
        tieuDe={dangSua ? t('doanhThu.sua') : t('doanhThu.them')}
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
              thongDiep: t('doanhThu.hoiLuu'),
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

          <div>
            <Label htmlFor="khoa">{t('doanhThu.khoaHoc')} *</Label>
            <SelectTimKiem
              id="khoa"
              luaChon={khoas
                // Khoá ngừng bán vẫn hiện khi SỬA đơn cũ đã dùng nó — ẩn đi thì ô trống trơn.
                .filter((k) => k.dangBan || k.id === dangSua?.khoaHocId)
                .map((k) => ({
                  giaTri: k.id,
                  nhan: `${k.ten} · ${tien(k.giaTien, k.donViTien)}`,
                }))}
              giaTri={khoaId}
              onDoi={setKhoaId}
              placeholder={t('doanhThu.chonKhoa')}
            />
            {khoaChon && !dangSua && (
              <p className="mt-1 text-xs text-muted-foreground">
                {t('doanhThu.giaNiemYet', {
                  gia: tien(khoaChon.giaTien, khoaChon.donViTien),
                  so: khoaChon.soBuoi,
                })}
              </p>
            )}
          </div>

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
            <Button type="submit" disabled={luu.isPending || !khachId || !khoaId}>
              {t('chung.luu')}
            </Button>
          </div>
        </form>
      </Modal>

      {hop}
    </div>
  )
}
