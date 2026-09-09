import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import {
  ArrowLeft, ChevronDown, ChevronRight, ExternalLink, Pencil, Plus, Send, ShoppingCart,
  Trash2,
} from 'lucide-react'
import { api, layMaLoi, type KetQuaTrang } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Card, CardContent, Input, Label, Table, Td, Textarea, Th,
  TrangTrong,
} from '@/components/ui'
import { Modal } from '@/components/ui/Modal'
import { MenuThaoTac } from '@/components/ui/MenuThaoTac'
import { SelectTimKiem } from '@/components/ui/SelectTimKiem'
import { useQuyen } from '@/lib/quyen'
import { useXacNhan } from '@/lib/xacNhan'
import {
  CAC_DON_VI, CAC_HINH_THUC, CAC_PHUONG_THUC, CAC_TRANG_THAI_KH, gioNgayVN, mauPhanTram,
  mauTrangThaiKh, mauTrangThaiXepLop, ngayChoInput, ngayVN, phanTram, tien,
  type ChiTietKhachHangDto, type DangKyKemThuDto, type DonViTien, type HinhThucChamSoc,
  type KhoaHocDto, type LanThuDto, type LichSuChamSocDto, type LoaiDonHang,
  type PhuongThucThanhToan, type SanPhamDto, type TrangThaiKhachHang,
} from './crmTypes'

/**
 * Mã tab (nằm trong URL) đi kèm khoá i18n — **không ghép chuỗi động**.
 *
 * Mã tab dùng gạch ngang cho URL đẹp, khoá i18n dùng camelCase; ghép động sinh ra
 * `tab_thong-tin` không khớp khoá nào và i18next trả về chính chuỗi khoá cho người dùng thấy.
 * Lỗi này đã xảy ra thật (07/09/2026) ở view chi tiết lớp học.
 */
const CAC_TAB = [
  { ma: 'thong-tin', khoa: 'chiTietKhach.tabThongTin', can: undefined },
  { ma: 'cham-soc', khoa: 'chiTietKhach.tabChamSoc', can: undefined },
  { ma: 'mua-hang', khoa: 'chiTietKhach.tabMuaHang', can: 'DoanhThu' },
] as const

type Tab = (typeof CAC_TAB)[number]['ma']

/** FR-17 — view chi tiết khách hàng, 3 tab. */
export default function ChiTietKhachHang() {
  const { t } = useTranslation()
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const [sp, setSp] = useSearchParams()
  const { coQuyen, dangTai: dangTaiQuyen } = useQuyen()

  const { data: kh, isLoading, isError } = useQuery({
    queryKey: ['khach-hang', id],
    queryFn: async () => (await api.get<ChiTietKhachHangDto>(`/khach-hang/${id}`)).data,
    enabled: !!id,
  })

  const tabHienThi = dangTaiQuyen ? CAC_TAB : CAC_TAB.filter((x) => !x.can || coQuyen(x.can))
  const tabQuery = sp.get('tab') as Tab | null
  const tab: Tab = tabQuery && tabHienThi.some((x) => x.ma === tabQuery) ? tabQuery : 'thong-tin'
  // `replace` để 4 lần bấm tab không sinh 4 mục lịch sử — Back phải về danh sách khách.
  const doiTab = (x: Tab) => setSp(x === 'thong-tin' ? {} : { tab: x }, { replace: true })

  if (isLoading) return <TrangTrong thongDiep={t('chung.dangTai')} />
  if (isError || !kh) return <TrangTrong thongDiep={t('loi.KHONG_TIM_THAY')} />

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-3">
        <Link
          to="/crm/khach-hang"
          className="inline-flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="h-4 w-4" />
          {t('menu.khachHangDs')}
        </Link>
        <h2 className="text-lg font-semibold">{kh.hoTen}</h2>
        <Badge variant={mauTrangThaiKh(kh.trangThai)}>
          {t(`trangThaiKhachHang.${kh.trangThai}`)}
        </Badge>
        {kh.tenHocVien && <Badge variant="accent">{kh.tenHocVien}</Badge>}
      </div>

      <div className="flex flex-wrap gap-1 rounded-lg border border-border p-1">
        {tabHienThi.map((x) => (
          <button
            key={x.ma}
            type="button"
            onClick={() => doiTab(x.ma)}
            className={
              'rounded-md px-3 py-1.5 text-sm font-medium transition-colors ' +
              (tab === x.ma
                ? 'bg-primary text-primary-foreground'
                : 'text-muted-foreground hover:bg-muted')
            }
          >
            {t(x.khoa)}
          </button>
        ))}
      </div>

      {tab === 'thong-tin' && <TabThongTin kh={kh} onXong={() => navigate(0)} />}
      {tab === 'cham-soc' && <TabChamSoc khachHangId={kh.id} />}
      {tab === 'mua-hang' && <TabKhoaHoc khachHangId={kh.id} />}
    </div>
  )
}

// ---------- Tab 1: Thông tin chung ----------

function TabThongTin({ kh, onXong }: { kh: ChiTietKhachHangDto; onXong: () => void }) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { coQuyen } = useQuyen()
  const { hoi, hop } = useXacNhan()
  const [phuongThuc, setPhuongThuc] = useState<PhuongThucThanhToan>(kh.phuongThucThanhToan)
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [moSua, setMoSua] = useState(false)

  const luu = useMutation({
    mutationFn: (fd: FormData) =>
      api.put(`/khach-hang/${kh.id}`, {
        id: kh.id,
        hoTen: String(fd.get('hoTen')).trim(),
        email: String(fd.get('email') ?? '').trim() || null,
        soDienThoai: String(fd.get('soDienThoai') ?? '').trim() || null,
        linkFacebook: String(fd.get('linkFacebook') ?? '').trim() || null,
        ghiChu: String(fd.get('ghiChu') ?? '').trim() || null,
        phuongThucThanhToan: phuongThuc,
        // Gửi lại mối nối hiện có — bỏ đi là âm thầm ngắt liên kết với hồ sơ học viên
        // (quy tắc #1: trường lệnh ghi đè phải có trong form).
        nguoiDungId: kh.nguoiDungId,
      }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['khach-hang'] })
      setMaLoi(null)
      // Modal đóng lại là phản hồi đủ rõ — không cần dòng "Đã lưu" tạm như khi form hiện sẵn.
      setMoSua(false)
      onXong()
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  return (
    <div className="grid gap-4">
      {/*
        Nút Sửa mở modal, KHÔNG hiện form sẵn bên cạnh (đổi 09/09/2026).

        Trước đó form sửa chiếm hẳn một cột của lưới `lg:grid-cols-2`, nên phần lớn thời gian
        người dùng chỉ muốn ĐỌC thông tin lại phải nhìn một form không dùng tới, và khối thông
        tin bị bóp còn nửa bề rộng. Nay thông tin dàn hết chiều ngang, sửa là hành động có chủ ý.
      */}
      {coQuyen('KhachHang', 'Sua') && (
        <div className="flex justify-end">
          <Button variant="outline" onClick={() => { setMaLoi(null); setMoSua(true) }}>
            <Pencil className="h-4 w-4" />
            {t('chiTietKhach.suaThongTin')}
          </Button>
        </div>
      )}

      <Card>
        <CardContent className="grid gap-3 pt-6 sm:grid-cols-2">
          <Dong nhan={t('khachHang.soDienThoai')} giaTri={kh.soDienThoai ?? '—'} />
          <Dong nhan={t('khachHang.email')} giaTri={kh.email ?? '—'} />
          {/* Hai trường dài trải hết hàng — link Facebook bị bó trong một cột thì xuống dòng
              giữa URL, đọc không ra. */}
          <div className="sm:col-span-2">
            <Dong
              nhan={t('khachHang.linkFacebook')}
              giaTri={
                kh.linkFacebook ? (
                  <a
                    href={kh.linkFacebook}
                    target="_blank"
                    rel="noreferrer noopener"
                    className="inline-flex items-center gap-1 text-primary hover:underline"
                  >
                    {kh.linkFacebook}
                    <ExternalLink className="h-3.5 w-3.5" />
                  </a>
                ) : (
                  '—'
                )
              }
            />
          </div>
          <Dong
            nhan={t('khachHang.phuongThuc')}
            giaTri={t(`phuongThucThanhToan.${kh.phuongThucThanhToan}`)}
          />
          <Dong nhan={t('chiTietKhach.soLanChamSoc')} giaTri={String(kh.soLanChamSoc)} />
          <Dong nhan={t('khachHang.soKhoa')} giaTri={String(kh.soDangKy)} />
          <Dong
            nhan={t('khachHang.hocVien')}
            giaTri={kh.tenHocVien ?? t('khachHang.chuaVaoHoc')}
          />
          <div className="sm:col-span-2">
            <Dong nhan={t('chung.ghiChu')} giaTri={kh.ghiChu ?? '—'} />
          </div>

          {/* Tiền chỉ hiện cho người có quyền DoanhThu — người trực tổng đài không thấy. */}
          {coQuyen('DoanhThu') && kh.soDangKy > 0 && (
            <>
              <Dong nhan={t('chiTietKhach.tongCamKet')} giaTri={tien(kh.tongCamKetVnd)} />
              <Dong
                nhan={t('chiTietKhach.tongDaThu')}
                giaTri={
                  <>
                    {tien(kh.tongDaThuVnd)}
                    {kh.tongDaThuVnd < kh.tongCamKetVnd && (
                      <Badge variant="cho" className="ml-2">
                        {t('chiTietKhach.conThieu', {
                          so: tien(kh.tongCamKetVnd - kh.tongDaThuVnd),
                        })}
                      </Badge>
                    )}
                  </>
                }
              />
            </>
          )}
        </CardContent>
      </Card>

      <Modal
        mo={moSua}
        onDong={() => {
          setMoSua(false)
          // Trả select về giá trị đang lưu: nó là state chứ không phải input có `defaultValue`,
          // nên đóng modal mà không reset thì lần mở sau vẫn giữ lựa chọn vừa bỏ dở.
          setPhuongThuc(kh.phuongThucThanhToan)
        }}
        chanDoiKhiXuLy={luu.isPending}
        tieuDe={t('chiTietKhach.suaThongTin')}
        moTa={kh.hoTen}
      >
        <form
          className="grid gap-3"
          onSubmit={(e) => {
            e.preventDefault()
            const fd = new FormData(e.currentTarget)
            hoi({
              tieuDe: t('chung.xacNhanLuu'),
              thongDiep: t('khachHang.hoiLuu'),
              onDongY: () => luu.mutate(fd),
            })
          }}
        >
          <div>
            <Label htmlFor="hoTen">{t('khachHang.hoTen')} *</Label>
            <Input id="hoTen" name="hoTen" required defaultValue={kh.hoTen} />
          </div>
          <div className="grid gap-3 sm:grid-cols-2">
            <div>
              <Label htmlFor="soDienThoai">{t('khachHang.soDienThoai')}</Label>
              <Input id="soDienThoai" name="soDienThoai" defaultValue={kh.soDienThoai ?? ''} />
            </div>
            <div>
              <Label htmlFor="email">{t('khachHang.email')}</Label>
              <Input id="email" name="email" type="email" defaultValue={kh.email ?? ''} />
            </div>
          </div>
          <div>
            <Label htmlFor="linkFacebook">{t('khachHang.linkFacebook')}</Label>
            <Input id="linkFacebook" name="linkFacebook" defaultValue={kh.linkFacebook ?? ''} />
          </div>
          <div>
            <Label htmlFor="pt">{t('khachHang.phuongThuc')}</Label>
            <SelectTimKiem
              id="pt"
              luaChon={CAC_PHUONG_THUC.map((p) => ({
                giaTri: p,
                nhan: t(`phuongThucThanhToan.${p}`),
              }))}
              giaTri={phuongThuc}
              onDoi={(v) => setPhuongThuc((v as PhuongThucThanhToan) ?? 'ChuyenKhoan')}
              choPhepXoa={false}
            />
          </div>
          <div>
            <Label htmlFor="ghiChu">{t('chung.ghiChu')}</Label>
            <Textarea id="ghiChu" name="ghiChu" rows={3} defaultValue={kh.ghiChu ?? ''} />
          </div>

          {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

          <div className="flex justify-end gap-2">
            <Button
              type="button"
              variant="outline"
              onClick={() => {
                setMoSua(false)
                setPhuongThuc(kh.phuongThucThanhToan)
              }}
            >
              {t('chung.huy')}
            </Button>
            <Button type="submit" disabled={luu.isPending}>
              {t('chung.luu')}
            </Button>
          </div>
        </form>
      </Modal>

      {hop}
    </div>
  )
}

// ---------- Tab 2: Lịch sử chăm sóc ----------

function TabChamSoc({ khachHangId }: { khachHangId: string }) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { coQuyen } = useQuyen()
  const { hoi, hop } = useXacNhan()
  const [moForm, setMoForm] = useState(false)
  const [dangSua, setDangSua] = useState<LichSuChamSocDto | null>(null)
  const [hinhThuc, setHinhThuc] = useState<HinhThucChamSoc>('GoiDien')
  const [trangThai, setTrangThai] = useState<TrangThaiKhachHang>('DangTuVan')
  const [maLoi, setMaLoi] = useState<string | null>(null)

  const { data: ds = [], isLoading } = useQuery({
    queryKey: ['khach-hang', khachHangId, 'cham-soc'],
    queryFn: async () =>
      (await api.get<LichSuChamSocDto[]>(`/khach-hang/${khachHangId}/cham-soc`)).data,
  })

  const lamMoi = () => {
    void qc.invalidateQueries({ queryKey: ['khach-hang', khachHangId, 'cham-soc'] })
    // Trạng thái phễu ở đầu trang suy từ lần chăm sóc mới nhất — phải làm mới theo.
    void qc.invalidateQueries({ queryKey: ['khach-hang', khachHangId] })
    void qc.invalidateQueries({ queryKey: ['khach-hang'] })
  }

  const dong = () => {
    setMoForm(false)
    setDangSua(null)
    setMaLoi(null)
  }

  const luu = useMutation({
    mutationFn: async (fd: FormData) => {
      const than = {
        khachHangId,
        thoiDiem: `${String(fd.get('thoiDiem'))}T00:00:00Z`,
        hinhThuc,
        noiDung: String(fd.get('noiDung')).trim(),
        trangThaiSau: trangThai,
      }
      if (dangSua) await api.put(`/khach-hang/cham-soc/${dangSua.id}`, { ...than, id: dangSua.id })
      else await api.post(`/khach-hang/${khachHangId}/cham-soc`, than)
    },
    onSuccess: () => {
      lamMoi()
      dong()
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const xoa = useMutation({
    mutationFn: (id: string) => api.delete(`/khach-hang/cham-soc/${id}`),
    onSuccess: lamMoi,
  })

  const moSua = (l: LichSuChamSocDto) => {
    setDangSua(l)
    setHinhThuc(l.hinhThuc)
    setTrangThai(l.trangThaiSau)
    setMaLoi(null)
    setMoForm(true)
  }

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <p className="text-sm text-muted-foreground">{t('chiTietKhach.chamSocGoiY')}</p>
        <div className="flex gap-2">
          {coQuyen('KhachHang', 'Them') && (
            <Button
              variant="outline"
              onClick={() => {
                setDangSua(null)
                setHinhThuc('GoiDien')
                setTrangThai('DangTuVan')
                setMaLoi(null)
                setMoForm(true)
              }}
            >
              <Plus className="h-4 w-4" />
              {t('chiTietKhach.themChamSoc')}
            </Button>
          )}

        </div>
      </div>

      <Card>
        <CardContent className="pt-6">
          {isLoading ? (
            <TrangTrong thongDiep={t('chung.dangTai')} />
          ) : ds.length === 0 ? (
            <TrangTrong thongDiep={t('chiTietKhach.chuaChamSoc')} />
          ) : (
            <ul className="grid gap-3">
              {ds.map((l) => (
                <li key={l.id} className="rounded-md border border-border p-3">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="text-sm font-medium">{ngayVN(l.thoiDiem)}</span>
                    <Badge variant="muted">{t(`hinhThucChamSoc.${l.hinhThuc}`)}</Badge>
                    <Badge variant={mauTrangThaiKh(l.trangThaiSau)}>
                      {t(`trangThaiKhachHang.${l.trangThaiSau}`)}
                    </Badge>
                    {l.tenNguoiPhuTrach && (
                      <span className="text-xs text-muted-foreground">
                        · {l.tenNguoiPhuTrach}
                      </span>
                    )}
                    <div className="ml-auto">
                      <MenuThaoTac
                        nhanMo={t('chung.thaoTac')}
                        muc={[
                          {
                            nhan: t('chung.sua'),
                            icon: Pencil,
                            an: !coQuyen('KhachHang', 'Sua'),
                            onChon: () => moSua(l),
                          },
                          {
                            nhan: t('chung.xoa'),
                            icon: Trash2,
                            nguyHiem: true,
                            ngatNhom: true,
                            an: !coQuyen('KhachHang', 'Xoa'),
                            onChon: () =>
                              hoi({
                                tieuDe: t('chung.xacNhanXoa'),
                                thongDiep: t('chiTietKhach.hoiXoaChamSoc'),
                                nhanDongY: t('chung.xoa'),
                                nguyHiem: true,
                                onDongY: () => xoa.mutate(l.id),
                              }),
                          },
                        ]}
                      />
                    </div>
                  </div>
                  <p className="mt-1 whitespace-pre-wrap text-sm text-muted-foreground">
                    {l.noiDung}
                  </p>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>

      <Modal
        mo={moForm}
        onDong={dong}
        chanDoiKhiXuLy={luu.isPending}
        tieuDe={dangSua ? t('chiTietKhach.suaChamSoc') : t('chiTietKhach.themChamSoc')}
        rong="md"
      >
        <form
          className="grid gap-3"
          onSubmit={(e) => {
            e.preventDefault()
            const fd = new FormData(e.currentTarget)
            hoi({
              tieuDe: t('chung.xacNhanLuu'),
              thongDiep: t('chiTietKhach.hoiLuuChamSoc'),
              onDongY: () => luu.mutate(fd),
            })
          }}
        >
          <div className="grid gap-3 sm:grid-cols-2">
            <div>
              <Label htmlFor="thoiDiem">{t('chiTietKhach.thoiDiem')} *</Label>
              <Input
                id="thoiDiem"
                name="thoiDiem"
                type="date"
                required
                defaultValue={
                  dangSua
                    ? ngayChoInput(dangSua.thoiDiem)
                    : new Date().toISOString().slice(0, 10)
                }
              />
            </div>
            <div>
              <Label htmlFor="hinhThuc">{t('chiTietKhach.hinhThuc')} *</Label>
              <SelectTimKiem
                id="hinhThuc"
                luaChon={CAC_HINH_THUC.map((h) => ({
                  giaTri: h,
                  nhan: t(`hinhThucChamSoc.${h}`),
                }))}
                giaTri={hinhThuc}
                onDoi={(v) => setHinhThuc((v as HinhThucChamSoc) ?? 'GoiDien')}
                choPhepXoa={false}
              />
            </div>
          </div>

          <div>
            <Label htmlFor="noiDung">{t('chiTietKhach.noiDung')} *</Label>
            <Textarea
              id="noiDung"
              name="noiDung"
              rows={4}
              required
              placeholder={t('chiTietKhach.noiDungGoiY')}
              defaultValue={dangSua?.noiDung ?? ''}
            />
          </div>

          <div>
            <Label htmlFor="trangThai">{t('chiTietKhach.trangThaiSau')} *</Label>
            <SelectTimKiem
              id="trangThai"
              luaChon={CAC_TRANG_THAI_KH.map((x) => ({
                giaTri: x,
                nhan: t(`trangThaiKhachHang.${x}`),
              }))}
              giaTri={trangThai}
              onDoi={(v) => setTrangThai((v as TrangThaiKhachHang) ?? 'DangTuVan')}
              choPhepXoa={false}
            />
            <p className="mt-1 text-xs text-muted-foreground">
              {t('chiTietKhach.trangThaiSauGoiY')}
            </p>
          </div>

          {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

          <div className="flex justify-end gap-2">
            <Button type="button" variant="outline" onClick={dong}>
              {t('chung.huy')}
            </Button>
            <Button type="submit" disabled={luu.isPending}>
              {t('chung.luu')}
            </Button>
          </div>
        </form>
      </Modal>

      {hop}
    </div>
  )
}

/**
 * Form ghi MUA HÀNG (mở từ tab Lịch sử mua hàng).
 *
 * Gọi `POST /khach-hang/{id}/mua-hang` — một lệnh ghi **cả** đơn hàng **và** dòng lịch sử chăm
 * sóc trong cùng transaction. Không gọi hai API riêng: API thứ hai lỗi sẽ để lại đơn hàng không
 * có dấu vết chăm sóc.
 */
function FormMuaHang({
  mo,
  khachHangId,
  onDong,
  onXong,
}: {
  mo: boolean
  khachHangId: string
  onDong: () => void
  onXong: () => void
}) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { coQuyen } = useQuyen()
  const { hoi, hop } = useXacNhan()

  const [loai, setLoai] = useState<LoaiDonHang>('KhoaHoc')
  const [matHangId, setMatHangId] = useState<string | null>(null)
  const [soLuong, setSoLuong] = useState('1')
  const [soTien, setSoTien] = useState('')
  const [donVi, setDonVi] = useState<DonViTien>('VND')
  const [tyGia, setTyGia] = useState('1')
  const [phuongThuc, setPhuongThuc] = useState<PhuongThucThanhToan>('ChuyenKhoan')
  const [hinhThuc, setHinhThuc] = useState<HinhThucChamSoc>('GapTrucTiep')
  const [daThuDu, setDaThuDu] = useState(true)
  const [maLoi, setMaLoi] = useState<string | null>(null)

  const { data: khoas = [] } = useQuery({
    queryKey: ['khoa-hoc-ngan'],
    queryFn: async () =>
      (await api.get<KetQuaTrang<KhoaHocDto>>('/khoa-hoc', { params: { soDong: 200 } }))
        .data.duLieu,
    enabled: mo && coQuyen('KhoaHoc'),
  })

  const { data: sanPhams = [] } = useQuery({
    queryKey: ['san-pham-ngan'],
    queryFn: async () =>
      (await api.get<KetQuaTrang<SanPhamDto>>('/san-pham', { params: { soDong: 200 } }))
        .data.duLieu,
    enabled: mo && coQuyen('SanPham'),
  })

  const dsMatHang = loai === 'KhoaHoc' ? khoas : sanPhams
  const chon = dsMatHang.find((x) => x.id === matHangId)

  /**
   * Chọn mặt hàng → điền sẵn giá và đơn vị tiền của nó.
   *
   * Sản phẩm nhân theo số lượng; khoá học luôn 1 suất. Người bán sửa được số tiền sau đó
   * (miễn giảm), nên đây chỉ là giá trị khởi đầu.
   */
  useEffect(() => {
    if (!chon) return
    const sl = loai === 'SanPham' ? Math.max(1, Number(soLuong) || 1) : 1
    setSoTien(String(chon.giaTien * sl))
    setDonVi(chon.donViTien)
    setTyGia(chon.donViTien === 'VND' ? '1' : '')
  }, [matHangId, soLuong, loai, chon])

  useEffect(() => {
    if (donVi === 'VND') setTyGia('1')
  }, [donVi])

  // Đổi loại mặt hàng thì bỏ lựa chọn cũ — id khoá học không tồn tại trong danh sách sản phẩm.
  useEffect(() => {
    setMatHangId(null)
    setSoLuong('1')
    setSoTien('')
  }, [loai])

  const giaGoc = chon ? chon.giaTien * (loai === 'SanPham' ? Number(soLuong) || 1 : 1) : 0
  const soTienSo = Number(soTien) || 0
  const ptXemTruoc = giaGoc === 0 ? null : (soTienSo / giaGoc) * 100

  const mua = useMutation({
    mutationFn: (fd: FormData) =>
      api.post(`/khach-hang/${khachHangId}/mua-hang`, {
        khoaHocId: loai === 'KhoaHoc' ? matHangId : null,
        sanPhamId: loai === 'SanPham' ? matHangId : null,
        soLuong: loai === 'SanPham' ? Number(soLuong) || 1 : 1,
        soTien: soTienSo,
        donViTien: donVi,
        tyGiaVeVnd: donVi === 'VND' ? 1 : Number(tyGia),
        ngayMua: `${String(fd.get('ngayMua'))}T00:00:00Z`,
        phuongThuc,
        hinhThucChamSoc: hinhThuc,
        noiDungChamSoc: String(fd.get('noiDung') ?? '').trim() || null,
        daThuDu,
      }),
    onSuccess: () => {
      // Làm mới cả doanh thu và danh sách khách: đơn mới hiện ở đó ngay.
      void qc.invalidateQueries({ queryKey: ['khach-hang'] })
      void qc.invalidateQueries({ queryKey: ['doanh-thu'] })
      void qc.invalidateQueries({ queryKey: ['doanh-thu-tong-hop'] })
      void qc.invalidateQueries({ queryKey: ['khoa-hoc'] })
      void qc.invalidateQueries({ queryKey: ['san-pham'] })
      onXong()
      onDong()
      setMatHangId(null)
      setSoTien('')
      setMaLoi(null)
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  return (
    <Modal
      mo={mo}
      onDong={onDong}
      chanDoiKhiXuLy={mua.isPending}
      tieuDe={t('chiTietKhach.ghiMuaHang')}
      moTa={t('chiTietKhach.ghiMuaHangMoTa')}
      rong="md"
    >
      <form
        className="grid gap-3"
        onSubmit={(e) => {
          e.preventDefault()
          const fd = new FormData(e.currentTarget)
          hoi({
            tieuDe: t('chung.xacNhanLuu'),
            thongDiep: t('chiTietKhach.hoiMuaHang'),
            onDongY: () => mua.mutate(fd),
          })
        }}
      >
        {/* Chọn LOẠI trước: hai danh mục khác nhau hoàn toàn, gộp một ô chọn thì người bán
            phải lọc bằng mắt giữa khoá học và sách. */}
        <div>
          <Label>{t('chiTietKhach.loaiMuaHang')} *</Label>
          <div className="mt-1 flex gap-1 rounded-lg border border-border p-1">
            {(['KhoaHoc', 'SanPham'] as LoaiDonHang[]).map((x) => (
              <button
                key={x}
                type="button"
                onClick={() => setLoai(x)}
                className={
                  'flex-1 rounded-md px-3 py-1.5 text-sm font-medium transition-colors ' +
                  (loai === x
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
          <Label htmlFor="matHang">
            {t(loai === 'KhoaHoc' ? 'doanhThu.khoaHoc' : 'sanPham.tenSp')} *
          </Label>
          <SelectTimKiem
            id="matHang"
            luaChon={dsMatHang
              .filter((x) => x.dangBan)
              .map((x) => ({ giaTri: x.id, nhan: `${x.ten} · ${tien(x.giaTien, x.donViTien)}` }))}
            giaTri={matHangId}
            onDoi={setMatHangId}
            placeholder={t('chiTietKhach.chonMatHang')}
          />
        </div>

        <div className="grid gap-3 sm:grid-cols-3">
          {/* Số lượng chỉ có nghĩa với sản phẩm — khoá học không ai mua 2 suất trong một đơn. */}
          {loai === 'SanPham' && (
            <div>
              <Label htmlFor="soLuong">{t('chiTietKhach.soLuong')} *</Label>
              <Input
                id="soLuong"
                type="number"
                min={1}
                required
                value={soLuong}
                onChange={(e) => setSoLuong(e.target.value)}
              />
            </div>
          )}
          <div>
            <Label htmlFor="soTienMua">{t('doanhThu.soTien')} *</Label>
            <Input
              id="soTienMua"
              type="number"
              min={0}
              step="0.01"
              required
              value={soTien}
              onChange={(e) => setSoTien(e.target.value)}
            />
          </div>
          <div>
            <Label htmlFor="donViMua">{t('khoaHoc.donViTien')} *</Label>
            <SelectTimKiem
              id="donViMua"
              luaChon={CAC_DON_VI.map((d) => ({ giaTri: d, nhan: t(`donViTien.${d}`) }))}
              giaTri={donVi}
              onDoi={(v) => setDonVi((v as DonViTien) ?? 'VND')}
              choPhepXoa={false}
            />
          </div>
          {donVi !== 'VND' && (
            <div>
              <Label htmlFor="tyGiaMua">{t('doanhThu.tyGia')} *</Label>
              <Input
                id="tyGiaMua"
                type="number"
                min={0}
                step="0.000001"
                required
                value={tyGia}
                onChange={(e) => setTyGia(e.target.value)}
              />
            </div>
          )}
        </div>

        {chon && (
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
                {t('doanhThu.quyDoiVnd')}:{' '}
                <strong>{tien(soTienSo * (Number(tyGia) || 0))}</strong>
              </span>
            )}
          </div>
        )}

        <div className="grid gap-3 sm:grid-cols-3">
          <div>
            <Label htmlFor="ngayMua">{t('chiTietKhach.ngayMua')} *</Label>
            <Input
              id="ngayMua"
              name="ngayMua"
              type="date"
              required
              defaultValue={new Date().toISOString().slice(0, 10)}
            />
          </div>
          <div>
            <Label htmlFor="ptMua">{t('khachHang.phuongThuc')}</Label>
            <SelectTimKiem
              id="ptMua"
              luaChon={CAC_PHUONG_THUC.map((x) => ({
                giaTri: x,
                nhan: t(`phuongThucThanhToan.${x}`),
              }))}
              giaTri={phuongThuc}
              onDoi={(v) => setPhuongThuc((v as PhuongThucThanhToan) ?? 'ChuyenKhoan')}
              choPhepXoa={false}
            />
          </div>
          <div>
            <Label htmlFor="htMua">{t('chiTietKhach.hinhThuc')}</Label>
            <SelectTimKiem
              id="htMua"
              luaChon={CAC_HINH_THUC.map((x) => ({
                giaTri: x,
                nhan: t(`hinhThucChamSoc.${x}`),
              }))}
              giaTri={hinhThuc}
              onDoi={(v) => setHinhThuc((v as HinhThucChamSoc) ?? 'GapTrucTiep')}
              choPhepXoa={false}
            />
          </div>
        </div>

        <div>
          <Label htmlFor="noiDungMua">{t('chiTietKhach.noiDung')}</Label>
          <Textarea
            id="noiDungMua"
            name="noiDung"
            rows={2}
            placeholder={t('chiTietKhach.noiDungMuaGoiY')}
          />
        </div>

        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            className="h-4 w-4 accent-[hsl(var(--primary))]"
            checked={daThuDu}
            onChange={(e) => setDaThuDu(e.target.checked)}
          />
          {t('chiTietKhach.daThuDu')}
        </label>
        <p className="text-xs text-muted-foreground">{t('chiTietKhach.daThuDuGoiY')}</p>

        {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

        <div className="flex justify-end gap-2">
          <Button type="button" variant="outline" onClick={onDong}>
            {t('chung.huy')}
          </Button>
          <Button type="submit" disabled={mua.isPending || !matHangId}>
            {t('chung.luu')}
          </Button>
        </div>
      </form>

      {hop}
    </Modal>
  )
}

// ---------- Tab 3: Lịch sử mua hàng (gộp cả sổ tiền đã đóng) ----------

/**
 * Mọi thứ khách đã mua **và** đã trả, trong một tab.
 *
 * Trước 09/09/2026 đây là hai tab riêng ("Lịch sử mua hàng" · "Số tiền đã đóng") đọc cùng một
 * endpoint. Gộp lại vì hai câu hỏi đó luôn được hỏi cùng lúc — "khách mua gì" đi liền
 * "trả bao nhiêu rồi" — và tách ra thì người bán phải nhớ số ở tab này để so với tab kia.
 *
 * Cấu trúc: nhóm theo LOẠI (khoá học · sản phẩm) → mỗi đơn một dòng có tình trạng thanh toán →
 * bấm mở ra sổ thu từng lần của chính đơn đó. Sổ thu ẩn mặc định vì phần lớn lúc xem người dùng
 * chỉ cần biết "đủ hay thiếu".
 */
function TabKhoaHoc({ khachHangId }: { khachHangId: string }) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { coQuyen } = useQuyen()
  const { hoi, hop } = useXacNhan()
  const [thuCho, setThuCho] = useState<DangKyKemThuDto | null>(null)
  const [dangSuaThu, setDangSuaThu] = useState<LanThuDto | null>(null)
  const [phuongThuc, setPhuongThuc] = useState<PhuongThucThanhToan>('ChuyenKhoan')
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [moMuaHang, setMoMuaHang] = useState(false)
  /** Id các đơn đang mở sổ thu — mở nhiều đơn cùng lúc để so sánh được. */
  const [moSo, setMoSo] = useState<string[]>([])
  /** Đơn đang mở form gửi yêu cầu xếp lớp (null = đóng). */
  const [guiCho, setGuiCho] = useState<DangKyKemThuDto | null>(null)

  const { data: ds = [], isLoading } = useQuery({
    queryKey: ['khach-hang', khachHangId, 'dang-ky'],
    queryFn: async () =>
      (await api.get<DangKyKemThuDto[]>(`/khach-hang/${khachHangId}/dang-ky`)).data,
  })

  const lamMoi = () => {
    void qc.invalidateQueries({ queryKey: ['khach-hang', khachHangId] })
    void qc.invalidateQueries({ queryKey: ['doanh-thu'] })
    // Danh sách chờ xếp lớp bên LMS đọc cùng dữ liệu đơn — gửi yêu cầu xong phải mất cache.
    void qc.invalidateQueries({ queryKey: ['cho-xep-lop'] })
  }

  const luuThu = useMutation({
    mutationFn: async (fd: FormData) => {
      const than = {
        dangKyId: thuCho!.id,
        soTien: Number(fd.get('soTien')),
        ngayThu: `${String(fd.get('ngayThu'))}T00:00:00Z`,
        phuongThuc,
        ghiChu: String(fd.get('ghiChu') ?? '').trim() || null,
      }
      if (dangSuaThu)
        await api.put(`/doanh-thu/thu-tien/${dangSuaThu.id}`, { ...than, id: dangSuaThu.id })
      // `ghiChamSoc` chỉ khi TẠO: backend tự thêm một dòng chăm sóc "khách đóng thêm tiền",
      // nên bổ sung thanh toán sinh đúng một lần doanh thu + một lần chăm sóc.
      else await api.post(`/doanh-thu/${thuCho!.id}/thu-tien`, { ...than, ghiChamSoc: true })
    },
    onSuccess: () => {
      lamMoi()
      // Sổ thu của đơn vừa ghi mở ra luôn — người dùng thấy ngay dòng mình vừa tạo.
      if (thuCho && !moSo.includes(thuCho.id)) setMoSo((cu) => [...cu, thuCho.id])
      setThuCho(null)
      setDangSuaThu(null)
      setMaLoi(null)
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const xoaThu = useMutation({
    mutationFn: (id: string) => api.delete(`/doanh-thu/thu-tien/${id}`),
    onSuccess: lamMoi,
  })

  const guiYeuCau = useMutation({
    mutationFn: ({ dangKyId, ghiChu }: { dangKyId: string; ghiChu: string | null }) =>
      api.post(`/doanh-thu/${dangKyId}/yeu-cau-xep-lop`, { dangKyId, ghiChu }),
    onSuccess: () => {
      lamMoi()
      // Mở sổ của đơn vừa gửi: người bán thấy ngay dòng lần gửi mới trong lịch sử.
      if (guiCho && !moSo.includes(guiCho.id)) setMoSo((cu) => [...cu, guiCho.id])
      setGuiCho(null)
      setMaLoi(null)
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  /**
   * Nhóm theo LOẠI.
   *
   * Danh sách phẳng theo thời gian thì người xem phải tự lọc bằng mắt "khách đã mua khoá nào,
   * mua sản phẩm gì" — mà đó chính là câu hỏi của tab này.
   *
   * Tổng mỗi nhóm quy về VND vì một khách có thể mua khoá giá CAD và sách giá VND — cộng thẳng
   * hai đơn vị là con số vô nghĩa.
   */
  const nhom = (['KhoaHoc', 'SanPham'] as LoaiDonHang[])
    .map((loai) => ({ loai, muc: ds.filter((d) => d.loai === loai) }))
    // Bỏ nhóm rỗng: tiêu đề "Sản phẩm" không có gì bên dưới trông như giao diện hỏng.
    .filter((n) => n.muc.length > 0)

  return (
    <div className="space-y-4">
      {/*
        Nút "Ghi mua hàng" ở ĐÂY, không ở tab chăm sóc (chuyển 09/09/2026): mua hàng thuộc về
        màn nói về hàng đã mua. Backend vẫn ghi kèm một dòng chăm sóc như trước.

        Gác bằng `DoanhThu` vì nó ghi dữ liệu tiền — người trực tổng đài chỉ có quyền
        `KhachHang` không thấy nút này.
      */}
      {coQuyen('DoanhThu', 'Them') && (
        <div className="flex justify-end">
          <Button onClick={() => setMoMuaHang(true)}>
            <ShoppingCart className="h-4 w-4" />
            {t('chiTietKhach.ghiMuaHang')}
          </Button>
        </div>
      )}

      {maLoi && !thuCho && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      {isLoading ? (
        <TrangTrong thongDiep={t('chung.dangTai')} />
      ) : ds.length === 0 ? (
        <TrangTrong thongDiep={t('chiTietKhach.chuaDangKy')} />
      ) : (
        <>
          {nhom.map((n) => (
            <div key={n.loai} className="space-y-2">
              <div className="flex flex-wrap items-center gap-2">
                <h3 className="font-semibold">{t(`loaiDonHang.${n.loai}`)}</h3>
                <Badge variant="muted">
                  {t('chiTietKhach.soLanMua', { so: n.muc.length })}
                </Badge>
                <span className="ml-auto text-sm text-muted-foreground">
                  {t('chiTietKhach.tongNhom')}:{' '}
                  <strong>
                    {tien(n.muc.reduce((tong, d) => tong + d.soTien * d.tyGiaVeVnd, 0))}
                  </strong>
                </span>
              </div>

              <div className="grid gap-2">
                {n.muc.map((d) => (
                  <DongDonHang
                    key={d.id}
                    d={d}
                    mo={moSo.includes(d.id)}
                    onDoiMo={() =>
                      setMoSo((cu) =>
                        cu.includes(d.id) ? cu.filter((x) => x !== d.id) : [...cu, d.id],
                      )
                    }
                    onGhiThu={() => {
                      setDangSuaThu(null)
                      setPhuongThuc('ChuyenKhoan')
                      setMaLoi(null)
                      setThuCho(d)
                    }}
                    onSuaThu={(lt) => {
                      setDangSuaThu(lt)
                      setPhuongThuc(lt.phuongThuc)
                      setMaLoi(null)
                      setThuCho(d)
                    }}
                    onXoaThu={(lt) =>
                      hoi({
                        tieuDe: t('chung.xacNhanXoa'),
                        thongDiep: t('chiTietKhach.hoiXoaThu', {
                          so: tien(lt.soTien, d.donViTien),
                        }),
                        nhanDongY: t('chung.xoa'),
                        nguyHiem: true,
                        onDongY: () => xoaThu.mutate(lt.id),
                      })
                    }
                    onGuiYeuCau={() => {
                      setMaLoi(null)
                      setGuiCho(d)
                    }}
                  />
                ))}
              </div>
            </div>
          ))}

          {/* Tổng CHUNG cả hai nhóm — con số người quản lý hỏi đầu tiên. */}
          {nhom.length > 1 && (
            <div className="flex justify-end border-t border-border pt-3 text-sm">
              <span className="text-muted-foreground">
                {t('chiTietKhach.tongTatCa')}:{' '}
                <strong className="text-base">
                  {tien(ds.reduce((tong, d) => tong + d.soTien * d.tyGiaVeVnd, 0))}
                </strong>
              </span>
            </div>
          )}
        </>
      )}

      <FormMuaHang
        mo={moMuaHang}
        khachHangId={khachHangId}
        onDong={() => setMoMuaHang(false)}
        onXong={lamMoi}
      />

      {/*
        Form gửi yêu cầu xếp lớp — modal chứ không phải hộp confirm trơn, vì cần ô GHI CHÚ:
        bên đào tạo chọn lớp dựa vào bối cảnh (trình độ, nguyện vọng giờ học), và khi gửi LẠI
        sau khi bị từ chối thì ghi chú là chỗ người bán bổ sung thông tin còn thiếu.
      */}
      <Modal
        mo={!!guiCho}
        onDong={() => setGuiCho(null)}
        chanDoiKhiXuLy={guiYeuCau.isPending}
        tieuDe={t('chiTietKhach.guiYeuCauXepLop')}
        moTa={guiCho?.tenMatHang}
        rong="sm"
      >
        {guiCho && (
          <form
            className="grid gap-3"
            onSubmit={(e) => {
              e.preventDefault()
              const fd = new FormData(e.currentTarget)
              const ghiChu = String(fd.get('ghiChuYeuCau') ?? '').trim() || null
              hoi({
                tieuDe: t('chiTietKhach.guiYeuCauXepLop'),
                thongDiep: t('chiTietKhach.hoiGuiYeuCau', { ten: guiCho.tenMatHang }),
                nhanDongY: t('chiTietKhach.guiYeuCau'),
                onDongY: () => guiYeuCau.mutate({ dangKyId: guiCho.id, ghiChu }),
              })
            }}
          >
            {/* Gửi lại lần thứ n: nhắc lại lý do bị từ chối ngay trên form, để người bán biết
                phải bổ sung gì mà không cần đóng modal đi đọc lịch sử. */}
            {guiCho.cacLanGuiXepLop.length > 0 && (
              <div className="rounded-md border border-border bg-muted/40 p-2.5 text-sm">
                <p className="font-medium">
                  {t('chiTietKhach.guiLanThu', {
                    so: guiCho.cacLanGuiXepLop[0].lanGui + 1,
                  })}
                </p>
                {guiCho.cacLanGuiXepLop[0].lyDoTuChoi && (
                  <p className="mt-1 text-muted-foreground">
                    {t('chiTietKhach.lyDoTuChoiTruoc')}:{' '}
                    <em>{guiCho.cacLanGuiXepLop[0].lyDoTuChoi}</em>
                  </p>
                )}
              </div>
            )}

            <div>
              <Label htmlFor="ghiChuYeuCau">{t('chiTietKhach.ghiChuYeuCau')}</Label>
              <Textarea
                id="ghiChuYeuCau"
                name="ghiChuYeuCau"
                rows={3}
                maxLength={500}
                placeholder={t('chiTietKhach.ghiChuYeuCauGoiY')}
              />
            </div>

            {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

            <div className="flex justify-end gap-2">
              <Button type="button" variant="outline" onClick={() => setGuiCho(null)}>
                {t('chung.huy')}
              </Button>
              <Button type="submit" disabled={guiYeuCau.isPending}>
                <Send className="h-4 w-4" />
                {t('chiTietKhach.guiYeuCau')}
              </Button>
            </div>
          </form>
        )}
      </Modal>

      <Modal
        mo={!!thuCho}
        onDong={() => {
          setThuCho(null)
          setDangSuaThu(null)
        }}
        chanDoiKhiXuLy={luuThu.isPending}
        tieuDe={dangSuaThu ? t('chiTietKhach.suaThu') : t('chiTietKhach.ghiThu')}
        moTa={thuCho?.tenMatHang}
        rong="sm"
      >
        {thuCho && (
          <form
            className="grid gap-3"
            onSubmit={(e) => {
              e.preventDefault()
              const fd = new FormData(e.currentTarget)
              hoi({
                tieuDe: t('chung.xacNhanLuu'),
                thongDiep: t('chiTietKhach.hoiLuuThu'),
                onDongY: () => luuThu.mutate(fd),
              })
            }}
          >
            <p className="text-sm text-muted-foreground">
              {t('chiTietKhach.conThieuGoiY', {
                so: tien(thuCho.conThieu, thuCho.donViTien),
              })}
            </p>

            <div>
              <Label htmlFor="soTien">{t('doanhThu.soTien')} *</Label>
              <Input
                id="soTien"
                name="soTien"
                type="number"
                min={0}
                step="0.01"
                required
                // Điền sẵn số CÒN THIẾU — ca hay dùng nhất là đóng hết phần còn lại.
                defaultValue={dangSuaThu?.soTien ?? thuCho.conThieu}
              />
            </div>
            <div>
              <Label htmlFor="ngayThu">{t('chiTietKhach.ngayThu')} *</Label>
              <Input
                id="ngayThu"
                name="ngayThu"
                type="date"
                required
                defaultValue={
                  dangSuaThu
                    ? ngayChoInput(dangSuaThu.ngayThu)
                    : new Date().toISOString().slice(0, 10)
                }
              />
            </div>
            <div>
              <Label htmlFor="ptThu">{t('khachHang.phuongThuc')}</Label>
              <SelectTimKiem
                id="ptThu"
                luaChon={CAC_PHUONG_THUC.map((p) => ({
                  giaTri: p,
                  nhan: t(`phuongThucThanhToan.${p}`),
                }))}
                giaTri={phuongThuc}
                onDoi={(v) => setPhuongThuc((v as PhuongThucThanhToan) ?? 'ChuyenKhoan')}
                choPhepXoa={false}
              />
            </div>
            <div>
              <Label htmlFor="ghiChuThu">{t('chung.ghiChu')}</Label>
              <Input id="ghiChuThu" name="ghiChu" defaultValue={dangSuaThu?.ghiChu ?? ''} />
            </div>

            {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

            <div className="flex justify-end gap-2">
              <Button
                type="button"
                variant="outline"
                onClick={() => {
                  setThuCho(null)
                  setDangSuaThu(null)
                }}
              >
                {t('chung.huy')}
              </Button>
              <Button type="submit" disabled={luuThu.isPending}>
                {t('chung.luu')}
              </Button>
            </div>
          </form>
        )}
      </Modal>

      {hop}
    </div>
  )
}

/**
 * Một đơn hàng: tiền cam kết, tình trạng thanh toán, sổ thu mở/đóng được.
 *
 * Tách component vì mỗi dòng có state mở/đóng riêng và ba hành động riêng — để trong vòng lặp
 * thì phần JSX của tab dài gấp đôi và không đọc được.
 */
function DongDonHang({
  d,
  mo,
  onDoiMo,
  onGhiThu,
  onSuaThu,
  onXoaThu,
  onGuiYeuCau,
}: {
  d: DangKyKemThuDto
  mo: boolean
  onDoiMo: () => void
  onGhiThu: () => void
  onSuaThu: (lt: LanThuDto) => void
  onXoaThu: (lt: LanThuDto) => void
  onGuiYeuCau: () => void
}) {
  const { t } = useTranslation()
  const { coQuyen } = useQuyen()

  return (
    <Card>
      <CardContent className="pt-6">
        <div className="flex flex-wrap items-center gap-3">
          <button
            type="button"
            onClick={onDoiMo}
            className="flex min-w-0 items-center gap-2 text-left"
          >
            {mo ? (
              <ChevronDown className="h-4 w-4 shrink-0 text-muted-foreground" />
            ) : (
              <ChevronRight className="h-4 w-4 shrink-0 text-muted-foreground" />
            )}
            <span className="min-w-0">
              <span className="block font-semibold">{d.tenMatHang}</span>
              <span className="block text-xs text-muted-foreground">
                {d.soBuoi !== null
                  ? t('khoaHoc.soBuoiNgan', { so: d.soBuoi })
                  : t('doanhThu.soLuongNgan', { so: d.soLuong })}{' '}
                · {ngayVN(d.ngayDangKy)}
                {d.ghiChu ? ` · ${d.ghiChu}` : ''}
              </span>
            </span>
          </button>

          <div className="ml-auto flex flex-wrap items-center gap-3 text-sm">
            <span className="text-muted-foreground">
              {t('chiTietKhach.camKet')}: <strong>{tien(d.soTien, d.donViTien)}</strong>
              {/* Đơn ngoại tệ hiện thêm số quy đổi: tổng nhóm là VND nên không có dòng này thì
                  người đọc không nối được hai con số. */}
              {d.donViTien !== 'VND' && (
                <span className="ml-1 text-xs">= {tien(d.soTien * d.tyGiaVeVnd)}</span>
              )}
            </span>

            {d.phanTramTrenGiaGoc !== null && (
              <Badge variant={mauPhanTram(d.phanTramTrenGiaGoc)}>
                {phanTram(d.phanTramTrenGiaGoc)}
              </Badge>
            )}

            {/* Ba trạng thái, không hai: đã đủ · đã đóng một phần · CHƯA đóng gì. Hiện
                "0,00 CA$" cho đơn chưa thu đồng nào thì nhìn giống một số tiền bình thường. */}
            {d.conThieu <= 0 ? (
              <Badge variant="ok">{t('chiTietKhach.daDu')}</Badge>
            ) : d.daThu <= 0 ? (
              <Badge variant="loi">{t('chiTietKhach.chuaDong')}</Badge>
            ) : (
              <Badge variant="cho">
                {t('chiTietKhach.thieu', { so: tien(d.conThieu, d.donViTien) })}
              </Badge>
            )}

            {/* BỔ SUNG THANH TOÁN — chỉ đơn còn thiếu. Một lần bấm sinh một lần doanh thu mới
                và một lần chăm sóc mới (backend làm trong cùng transaction). */}
            {coQuyen('DoanhThu', 'Them') && d.conThieu > 0 && (
              <Button size="sm" onClick={onGhiThu}>
                <Plus className="h-4 w-4" />
                {t('chiTietKhach.boSungThanhToan')}
              </Button>
            )}

            {/* XẾP LỚP — chỉ đơn KHOÁ HỌC (sách không có lớp).
                Ba trạng thái loại trừ nhau, theo đúng thứ tự ưu tiên đọc:
                đã vào lớp → đang chờ → còn gửi được (kể cả sau khi bị từ chối). */}
            {d.loai === 'KhoaHoc' && (
              <>
                {d.tenLopDaXep ? (
                  <Badge variant="ok">
                    {t('chiTietKhach.daXepLop', { ten: d.tenLopDaXep })}
                  </Badge>
                ) : d.dangChoXepLop ? (
                  <Badge variant="cho">{t('chiTietKhach.dangChoXepLop')}</Badge>
                ) : (
                  coQuyen('DoanhThu', 'Sua') && (
                    <Button size="sm" variant="outline" onClick={onGuiYeuCau}>
                      <Send className="h-4 w-4" />
                      {d.cacLanGuiXepLop.length > 0
                        ? t('chiTietKhach.guiLai')
                        : t('chiTietKhach.guiYeuCau')}
                    </Button>
                  )
                )}

                {/* SỐ LẦN GỬI — chỉ hiện khi đã gửi từ 2 lần, một lần thì con số không nói
                    thêm gì mà chiếm chỗ. Bấm vào mở luôn phần lịch sử bên dưới. */}
                {d.cacLanGuiXepLop.length > 1 && (
                  <button type="button" onClick={onDoiMo}>
                    <Badge variant="muted">
                      {t('chiTietKhach.daGuiNLan', { so: d.cacLanGuiXepLop.length })}
                    </Badge>
                  </button>
                )}
              </>
            )}
          </div>
        </div>

        {mo && (
          <div className="mt-3 border-t border-border pt-3">
            {d.cacLanThu.length === 0 ? (
              <p className="text-sm text-muted-foreground">{t('chiTietKhach.chuaThu')}</p>
            ) : (
              <Table>
                <thead>
                  <tr>
                    <Th>{t('chiTietKhach.ngayThu')}</Th>
                    <Th className="text-right">{t('doanhThu.soTien')}</Th>
                    <Th>{t('khachHang.phuongThuc')}</Th>
                    <Th>{t('chiTietKhach.nguoiThu')}</Th>
                    <Th>{t('chung.ghiChu')}</Th>
                    <Th />
                  </tr>
                </thead>
                <tbody>
                  {d.cacLanThu.map((lt) => (
                    <tr key={lt.id} className="hover:bg-muted/40">
                      <Td>{ngayVN(lt.ngayThu)}</Td>
                      <Td className="text-right font-medium">{tien(lt.soTien, d.donViTien)}</Td>
                      <Td className="text-muted-foreground">
                        {t(`phuongThucThanhToan.${lt.phuongThuc}`)}
                      </Td>
                      <Td className="text-muted-foreground">{lt.tenNguoiThu ?? '—'}</Td>
                      <Td className="text-muted-foreground">{lt.ghiChu ?? '—'}</Td>
                      <Td>
                        <div className="flex justify-end">
                          <MenuThaoTac
                            nhanMo={t('chung.thaoTac')}
                            muc={[
                              {
                                nhan: t('chung.sua'),
                                icon: Pencil,
                                an: !coQuyen('DoanhThu', 'Sua'),
                                onChon: () => onSuaThu(lt),
                              },
                              {
                                nhan: t('chung.xoa'),
                                icon: Trash2,
                                nguyHiem: true,
                                ngatNhom: true,
                                an: !coQuyen('DoanhThu', 'Xoa'),
                                onChon: () => onXoaThu(lt),
                              },
                            ]}
                          />
                        </div>
                      </Td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            )}

            {/*
              LỊCH SỬ GỬI YÊU CẦU XẾP LỚP (FR-21) — từng lần gửi kèm người gửi, ghi chú, kết quả
              và lý do từ chối. Đây là chỗ trả lời câu hỏi của khách "sao em chưa có lớp": người
              bán mở ra là thấy đã gửi mấy lần và trung tâm trả lời gì.

              Mới nhất trước (backend đã sắp) — lần gần nhất là thứ đang cần biết.
            */}
            {d.cacLanGuiXepLop.length > 0 && (
              <div className="mt-4">
                <h5 className="mb-2 text-sm font-semibold">
                  {t('chiTietKhach.lichSuGuiYeuCau')}
                </h5>
                <ul className="grid gap-2">
                  {d.cacLanGuiXepLop.map((y) => (
                    <li key={y.id} className="rounded-md border border-border p-2.5 text-sm">
                      <div className="flex flex-wrap items-center gap-2">
                        <Badge variant="muted">
                          {t('chiTietKhach.lanThu', { so: y.lanGui })}
                        </Badge>
                        <Badge variant={mauTrangThaiXepLop(y.trangThai)}>
                          {t(`trangThaiXepLop.${y.trangThai}`)}
                        </Badge>
                        {y.tenLopHoc && <Badge variant="accent">{y.tenLopHoc}</Badge>}
                        <span className="ml-auto text-xs text-muted-foreground">
                          {gioNgayVN(y.thoiDiemGui)}
                        </span>
                      </div>

                      <p className="mt-1.5 text-xs text-muted-foreground">
                        {t('chiTietKhach.nguoiGui')}: {y.tenNguoiGui ?? '—'}
                        {y.thoiDiemXuLy && (
                          <>
                            {' · '}
                            {t('chiTietKhach.nguoiXuLy')}: {y.tenNguoiXuLy ?? '—'}
                            {' · '}
                            {gioNgayVN(y.thoiDiemXuLy)}
                          </>
                        )}
                      </p>

                      {y.ghiChu && (
                        <p className="mt-1.5">
                          <span className="text-muted-foreground">
                            {t('chung.ghiChu')}:{' '}
                          </span>
                          {y.ghiChu}
                        </p>
                      )}

                      {/* Lý do từ chối nổi bật hơn ghi chú: đó là việc người bán phải xử lý. */}
                      {y.lyDoTuChoi && (
                        <p className="mt-1.5 rounded bg-destructive/10 px-2 py-1 text-destructive">
                          <span className="font-medium">
                            {t('chiTietKhach.lyDoTuChoi')}:{' '}
                          </span>
                          {y.lyDoTuChoi}
                        </p>
                      )}
                    </li>
                  ))}
                </ul>
              </div>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  )
}

function Dong({ nhan, giaTri }: { nhan: string; giaTri: React.ReactNode }) {
  return (
    <div className="grid gap-0.5">
      <span className="text-xs uppercase tracking-wide text-muted-foreground">{nhan}</span>
      <span className="text-sm">{giaTri}</span>
    </div>
  )
}
