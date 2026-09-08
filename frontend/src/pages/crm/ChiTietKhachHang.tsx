import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { ArrowLeft, ExternalLink, Pencil, Plus, Trash2 } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
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
  CAC_HINH_THUC, CAC_PHUONG_THUC, CAC_TRANG_THAI_KH, mauPhanTram, mauTrangThaiKh,
  ngayChoInput, ngayVN, phanTram, tien,
  type ChiTietKhachHangDto, type DangKyKemThuDto, type HinhThucChamSoc, type LanThuDto,
  type LichSuChamSocDto, type PhuongThucThanhToan, type TrangThaiKhachHang,
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
  { ma: 'khoa-hoc', khoa: 'chiTietKhach.tabKhoaHoc', can: 'DoanhThu' },
  { ma: 'da-dong', khoa: 'chiTietKhach.tabDaDong', can: 'DoanhThu' },
] as const

type Tab = (typeof CAC_TAB)[number]['ma']

/** FR-17 — view chi tiết khách hàng, 4 tab. */
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
      {tab === 'khoa-hoc' && <TabKhoaHoc khachHangId={kh.id} chiTien={false} />}
      {tab === 'da-dong' && <TabKhoaHoc khachHangId={kh.id} chiTien />}
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
  const [daLuu, setDaLuu] = useState(false)

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
      setDaLuu(true)
      window.setTimeout(() => setDaLuu(false), 2500)
      onXong()
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  return (
    <div className="grid gap-4 lg:grid-cols-2">
      <Card>
        <CardContent className="grid gap-3 pt-6 sm:grid-cols-2">
          <Dong nhan={t('khachHang.soDienThoai')} giaTri={kh.soDienThoai ?? '—'} />
          <Dong nhan={t('khachHang.email')} giaTri={kh.email ?? '—'} />
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

      {coQuyen('KhachHang', 'Sua') && (
        <Card>
          <CardContent className="pt-6">
            <h3 className="mb-3 font-semibold">{t('chiTietKhach.suaThongTin')}</h3>
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
                  <Input
                    id="soDienThoai"
                    name="soDienThoai"
                    defaultValue={kh.soDienThoai ?? ''}
                  />
                </div>
                <div>
                  <Label htmlFor="email">{t('khachHang.email')}</Label>
                  <Input id="email" name="email" type="email" defaultValue={kh.email ?? ''} />
                </div>
              </div>
              <div>
                <Label htmlFor="linkFacebook">{t('khachHang.linkFacebook')}</Label>
                <Input
                  id="linkFacebook"
                  name="linkFacebook"
                  defaultValue={kh.linkFacebook ?? ''}
                />
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

              <div className="flex items-center justify-end gap-2">
                {daLuu && (
                  <span className="mr-auto text-sm text-status-ok">{t('chung.daLuu')}</span>
                )}
                <Button type="submit" disabled={luu.isPending}>
                  {t('chung.luu')}
                </Button>
              </div>
            </form>
          </CardContent>
        </Card>
      )}

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
        {coQuyen('KhachHang', 'Them') && (
          <Button
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

// ---------- Tab 3 & 4: Khoá học tham gia / Số tiền đã đóng ----------

/**
 * Một nguồn dữ liệu, hai góc nhìn.
 *
 * `chiTien = false` → tab **Khoá học tham gia**: khoá nào, cam kết bao nhiêu, giảm mấy %.
 * `chiTien = true`  → tab **Số tiền đã đóng**: từng lần thu và còn thiếu bao nhiêu.
 *
 * Không tách hai component: cùng gọi một endpoint, tách ra là hai bản sao sẽ trôi khỏi nhau.
 */
function TabKhoaHoc({ khachHangId, chiTien }: { khachHangId: string; chiTien: boolean }) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { coQuyen } = useQuyen()
  const { hoi, hop } = useXacNhan()
  const [thuCho, setThuCho] = useState<DangKyKemThuDto | null>(null)
  const [dangSuaThu, setDangSuaThu] = useState<LanThuDto | null>(null)
  const [phuongThuc, setPhuongThuc] = useState<PhuongThucThanhToan>('ChuyenKhoan')
  const [maLoi, setMaLoi] = useState<string | null>(null)

  const { data: ds = [], isLoading } = useQuery({
    queryKey: ['khach-hang', khachHangId, 'dang-ky'],
    queryFn: async () =>
      (await api.get<DangKyKemThuDto[]>(`/khach-hang/${khachHangId}/dang-ky`)).data,
  })

  const lamMoi = () => {
    void qc.invalidateQueries({ queryKey: ['khach-hang', khachHangId] })
    void qc.invalidateQueries({ queryKey: ['doanh-thu'] })
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
      else await api.post(`/doanh-thu/${thuCho!.id}/thu-tien`, than)
    },
    onSuccess: () => {
      lamMoi()
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

  if (isLoading) return <TrangTrong thongDiep={t('chung.dangTai')} />
  if (ds.length === 0) return <TrangTrong thongDiep={t('chiTietKhach.chuaDangKy')} />

  return (
    <div className="space-y-3">
      {ds.map((d) => (
        <Card key={d.id}>
          <CardContent className="pt-6">
            <div className="flex flex-wrap items-center gap-3">
              <div className="min-w-0">
                <p className="font-semibold">{d.tenKhoaHoc}</p>
                <p className="text-xs text-muted-foreground">
                  {t('khoaHoc.soBuoiNgan', { so: d.soBuoi })} · {ngayVN(d.ngayDangKy)}
                </p>
              </div>

              <div className="ml-auto flex flex-wrap items-center gap-4 text-sm">
                <span className="text-muted-foreground">
                  {t('chiTietKhach.camKet')}: <strong>{tien(d.soTien, d.donViTien)}</strong>
                </span>
                {!chiTien && (
                  <Badge variant={mauPhanTram(d.phanTramTrenGiaGoc)}>
                    {phanTram(d.phanTramTrenGiaGoc)}
                  </Badge>
                )}
                {chiTien && (
                  <>
                    <span className="text-muted-foreground">
                      {t('chiTietKhach.daThu')}: <strong>{tien(d.daThu, d.donViTien)}</strong>
                    </span>
                    <Badge variant={d.conThieu <= 0 ? 'ok' : 'cho'}>
                      {d.conThieu <= 0
                        ? t('chiTietKhach.daDu')
                        : t('chiTietKhach.thieu', { so: tien(d.conThieu, d.donViTien) })}
                    </Badge>
                  </>
                )}
                {chiTien && coQuyen('DoanhThu', 'Them') && d.conThieu > 0 && (
                  <Button
                    size="sm"
                    onClick={() => {
                      setDangSuaThu(null)
                      setPhuongThuc('ChuyenKhoan')
                      setMaLoi(null)
                      setThuCho(d)
                    }}
                  >
                    <Plus className="h-4 w-4" />
                    {t('chiTietKhach.ghiThu')}
                  </Button>
                )}
              </div>
            </div>

            {d.ghiChu && !chiTien && (
              <p className="mt-2 text-sm text-muted-foreground">{d.ghiChu}</p>
            )}

            {/* Sổ thu chỉ hiện ở tab Số tiền đã đóng — tab Khoá học nói về cam kết. */}
            {chiTien && d.cacLanThu.length > 0 && (
              <div className="mt-3">
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
                        <Td className="text-right font-medium">
                          {tien(lt.soTien, d.donViTien)}
                        </Td>
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
                                  onChon: () => {
                                    setDangSuaThu(lt)
                                    setPhuongThuc(lt.phuongThuc)
                                    setMaLoi(null)
                                    setThuCho(d)
                                  },
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
                                      thongDiep: t('chiTietKhach.hoiXoaThu', {
                                        so: tien(lt.soTien, d.donViTien),
                                      }),
                                      nhanDongY: t('chung.xoa'),
                                      nguyHiem: true,
                                      onDongY: () => xoaThu.mutate(lt.id),
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
              </div>
            )}

            {chiTien && d.cacLanThu.length === 0 && (
              <p className="mt-3 text-sm text-muted-foreground">
                {t('chiTietKhach.chuaThu')}
              </p>
            )}
          </CardContent>
        </Card>
      ))}

      <Modal
        mo={!!thuCho}
        onDong={() => {
          setThuCho(null)
          setDangSuaThu(null)
        }}
        chanDoiKhiXuLy={luuThu.isPending}
        tieuDe={dangSuaThu ? t('chiTietKhach.suaThu') : t('chiTietKhach.ghiThu')}
        moTa={thuCho?.tenKhoaHoc}
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

function Dong({ nhan, giaTri }: { nhan: string; giaTri: React.ReactNode }) {
  return (
    <div className="grid gap-0.5">
      <span className="text-xs uppercase tracking-wide text-muted-foreground">{nhan}</span>
      <span className="text-sm">{giaTri}</span>
    </div>
  )
}
