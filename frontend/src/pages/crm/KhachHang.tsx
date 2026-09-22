import { useState } from 'react'
import { DO_DAI_MAT_KHAU_TOI_THIEU } from '@/lib/chinhSachMatKhau'
import { useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  ExternalLink, Eye, KeyRound, Pencil, Plus, SlidersHorizontal, Trash2,
} from 'lucide-react'
import { api, layMaLoi, trangRong, type KetQuaTrang } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Card, CardContent, Input, Label, Table, Td, Textarea, Th,
  TrangTrong,
} from '@/components/ui'
import { Modal } from '@/components/ui/Modal'
import { PhanTrang } from '@/components/ui/PhanTrang'
import { MenuThaoTac } from '@/components/ui/MenuThaoTac'
import { SelectTimKiem } from '@/components/ui/SelectTimKiem'
import { LocDoiNhom } from '@/components/crm/LocDoiNhom'
import { useQuyen } from '@/lib/quyen'
import { useXacNhan } from '@/lib/xacNhan'
import {
  CAC_PHUONG_THUC, tien, type KhachHangDto, type PhuongThucThanhToan,
} from './crmTypes'

interface HocVienNgan {
  id: string
  hoTen: string
}

/**
 * FR-17 — khách hàng (CRM): người quan tâm khoá học, chưa chắc thành học viên.
 *
 * Bảng riêng chứ không dùng `NGUOI_DUNG` — xem `docs/nghiep-vu/crm.md`. Khi khách thật sự vào
 * học thì **nối** bằng `nguoiDungId`, không copy họ tên sang.
 */
export default function KhachHang() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const { coQuyen } = useQuyen()
  const { hoi, hop } = useXacNhan()

  const [trang, setTrang] = useState(1)
  const [soDong, setSoDong] = useState(20)
  const [timKiem, setTimKiem] = useState('')
  const [locMua, setLocMua] = useState<string | null>(null)
  const [phongBanId, setPhongBanId] = useState<string | null>(null)
  const [nhanVienId, setNhanVienId] = useState<string | null>(null)
  const [nguon, setNguon] = useState<string | null>(null)
  const [tuNgay, setTuNgay] = useState('')
  const [denNgay, setDenNgay] = useState('')
  const [moLocThem, setMoLocThem] = useState(false)

  const [moForm, setMoForm] = useState(false)
  const [dangSua, setDangSua] = useState<KhachHangDto | null>(null)
  const [phuongThuc, setPhuongThuc] = useState<PhuongThucThanhToan>('ChuyenKhoan')
  const [hocVienId, setHocVienId] = useState<string | null>(null)
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [maLoiBang, setMaLoiBang] = useState<string | null>(null)

  const { data: kq = trangRong<KhachHangDto>(), isLoading } = useQuery({
    queryKey: ['khach-hang', timKiem, locMua, phongBanId, nhanVienId, nguon,
               tuNgay, denNgay, trang, soDong],
    queryFn: async () =>
      (await api.get<KetQuaTrang<KhachHangDto>>('/khach-hang', {
        params: {
          timKiem: timKiem || undefined,
          daMua: locMua === null ? undefined : locMua === 'true',
          phongBanId: phongBanId || undefined,
          nhanVienId: nhanVienId || undefined,
          nguon: nguon || undefined,
          // Ngày lọc theo NGÀY TẠO HỒ SƠ khách, không phải ngày mua.
          tuNgay: tuNgay || undefined,
          denNgay: denNgay || undefined,
          trang,
          soDong,
        },
      })).data,
  })

  /**
   * Danh sách học viên để NỐI khách với hồ sơ học tập.
   *
   * `enabled: coQuyen('TaiKhoan')` — người trực tổng đài chỉ có quyền `KhachHang` sẽ nhận 403
   * ở endpoint này; gọi vô điều kiện thì họ thấy một lỗi mạng không giải thích được.
   */
  const { data: hocViens = [] } = useQuery({
    queryKey: ['hoc-vien-ngan'],
    queryFn: async () =>
      (await api.get<KetQuaTrang<HocVienNgan>>('/hoc-vien', { params: { soDong: 200 } }))
        .data.duLieu,
    enabled: coQuyen('TaiKhoan'),
  })

  /**
   * Khách đang được cấp tài khoản học viên (13/09/2026).
   *
   * Thay cho màn `/lms/hoc-vien` đã bỏ: học viên nay quản lý tập trung ở CRM. Tạo từ đây thì
   * hồ sơ **tự nối** `nguoi_dung_id`, nên không bao giờ có chuyện một con người thành hai hồ
   * sơ ở hai hệ thống — rủi ro lớn nhất khi tạo hồ sơ ở hai chỗ khác nhau.
   */
  const [capTkCho, setCapTkCho] = useState<KhachHangDto | null>(null)

  /** Nhóm quyền để gán cho tài khoản mới. Chỉ tải khi thật sự mở hộp cấp tài khoản. */
  const { data: quyens = [] } = useQuery({
    queryKey: ['quyen-ngan'],
    queryFn: async () =>
      (await api.get<{ id: string; tenQuyen: string }[]>('/quyen')).data,
    enabled: !!capTkCho,
  })

  const capTaiKhoan = useMutation({
    mutationFn: async (fd: FormData) => {
      const quyenHv = quyens.find((q) => q.tenQuyen === 'Học viên')
      return api.post('/nguoi-dung', {
        hoTen: capTkCho!.hoTen,
        email: capTkCho!.email,
        soDienThoai: capTkCho!.soDienThoai,
        loaiNguoiDung: 'HocVien',
        // Nối ngay khi tạo — đây là lý do cấp tài khoản từ CRM chứ không từ màn khác.
        khachHangId: capTkCho!.id,
        taiKhoan: {
          username: String(fd.get('username')).trim(),
          matKhau: String(fd.get('matKhau')),
          quyenIds: quyenHv ? [quyenHv.id] : [],
          // Mật khẩu do người điều phối đặt rồi đọc cho khách — buộc đổi ở lần đăng nhập đầu.
          phaiDoiMatKhau: true,
        },
      })
    },
    onSuccess: () => {
      lamMoi()
      void qc.invalidateQueries({ queryKey: ['hoc-vien-ngan'] })
      setCapTkCho(null)
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  /**
   * Số điện thoại đang gõ trong form, để tra trùng NGAY (14/09/2026).
   *
   * Trước đây người dùng phải bấm Lưu mới biết trùng, rồi tự đóng form đi tìm khách đó bằng
   * tay. Nay gõ xong số là hiện cảnh báo kèm nút mở thẳng hồ sơ.
   */
  const [sdtDangGo, setSdtDangGo] = useState('')

  /**
   * Khách đã dùng số này. `enabled` chỉ bật khi số đủ dài — gõ tới ký tự thứ hai đã gọi API là
   * gửi một loạt request vô ích, và chưa ký tự nào đủ để nói "trùng".
   */
  const { data: khachTrung } = useQuery({
    queryKey: ['khach-hang', 'tra-sdt', sdtDangGo],
    queryFn: async () =>
      (await api.get<KetQuaTrang<KhachHangDto>>('/khach-hang', {
        params: { soDienThoaiChinhXac: sdtDangGo, soDong: 1 },
      })).data.duLieu[0] ?? null,
    enabled: moForm && sdtDangGo.length >= 9,
  })

  // Sửa chính khách này thì số của họ không phải là "trùng".
  const canhBaoTrung = khachTrung && khachTrung.id !== dangSua?.id ? khachTrung : null

  const lamMoi = () => {
    void qc.invalidateQueries({ queryKey: ['khach-hang'] })
    void qc.invalidateQueries({ queryKey: ['khach-hang-ngan'] })
    // Tên khách hiện ở màn Doanh thu.
    void qc.invalidateQueries({ queryKey: ['doanh-thu'] })
  }

  const dong = () => {
    setMoForm(false)
    setDangSua(null)
    setMaLoi(null)
    setSdtDangGo('')
  }

  const luu = useMutation({
    mutationFn: async (fd: FormData) => {
      const than = {
        hoTen: String(fd.get('hoTen')).trim(),
        email: String(fd.get('email') ?? '').trim() || null,
        soDienThoai: String(fd.get('soDienThoai') ?? '').trim() || null,
        linkFacebook: String(fd.get('linkFacebook') ?? '').trim() || null,
        ghiChu: String(fd.get('ghiChu') ?? '').trim() || null,
        phuongThucThanhToan: phuongThuc,
        nguoiDungId: hocVienId,
      }
      if (dangSua) await api.put(`/khach-hang/${dangSua.id}`, { ...than, id: dangSua.id })
      else await api.post('/khach-hang', than)
    },
    onSuccess: () => {
      lamMoi()
      dong()
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const xoa = useMutation({
    mutationFn: (id: string) => api.delete(`/khach-hang/${id}`),
    onSuccess: lamMoi,
    onError: (e) => setMaLoiBang(layMaLoi(e)),
  })

  const moSua = (k: KhachHangDto) => {
    setDangSua(k)
    setPhuongThuc(k.phuongThucThanhToan)
    setHocVienId(k.nguoiDungId)
    setMaLoi(null)
    setMoForm(true)
  }

  /**
   * Số bộ lọc NÂNG CAO đang bật — hiện trên nút để không ai quên mình đang lọc.
   *
   * KHÔNG đếm `timKiem` và `locMua`: hai ô đó luôn hiện trên màn nên người dùng tự thấy.
   * Đếm chúng làm con số trên nút không khớp với panel mở ra.
   */
  const soLocDangBat = [phongBanId, nhanVienId, nguon, tuNgay || null, denNgay || null]
    .filter(Boolean).length

  const xoaLoc = () => {
    setPhongBanId(null)
    setNhanVienId(null)
    setNguon(null)
    setTuNgay('')
    setDenNgay('')
    setTrang(1)
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="flex flex-wrap items-end gap-3">
          <div className="w-64">
            <Label htmlFor="tim">{t('chung.timKiem')}</Label>
            <Input
              id="tim"
              value={timKiem}
              onChange={(e) => {
                setTimKiem(e.target.value)
                setTrang(1)
              }}
              placeholder={t('khachHang.timTheo')}
            />
          </div>
          <div className="w-44">
            <Label htmlFor="loc-mua">{t('khachHang.tinhTrang')}</Label>
            <SelectTimKiem
              id="loc-mua"
              luaChon={[
                { giaTri: 'true', nhan: t('khachHang.daMua') },
                { giaTri: 'false', nhan: t('khachHang.chuaMua') },
              ]}
              giaTri={locMua}
              onDoi={(v) => {
                setLocMua(v)
                setTrang(1)
              }}
              placeholder={t('chung.tatCa')}
            />
          </div>

          {/*
            Bộ lọc nâng cao ẩn sau một nút (nguyên tắc UI/UX mục 1: "tuỳ chọn nâng cao ẩn dưới
            Xem thêm"). Sáu ô lọc bày hết ra một hàng thì vỡ bố cục ở màn hẹp, và người chỉ cần
            tìm theo tên phải đọc qua cả sáu.

            Số bộ lọc đang bật hiện trên nút: đóng panel lại mà vẫn còn lọc thì bảng bên dưới
            ít dòng một cách không giải thích được — đây là chỗ người dùng dễ tưởng mất dữ liệu.
          */}
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

        {coQuyen('KhachHang', 'Them') && (
          <Button
            onClick={() => {
              setDangSua(null)
              setPhuongThuc('ChuyenKhoan')
              setHocVienId(null)
              setMaLoi(null)
              setMoForm(true)
            }}
          >
            <Plus className="h-4 w-4" />
            {t('khachHang.them')}
          </Button>
        )}
      </div>

      {moLocThem && (
        <div className="flex flex-wrap items-end gap-3 rounded-lg border border-border bg-muted/30 p-3">
          <LocDoiNhom
            phongBanId={phongBanId}
            nhanVienId={nhanVienId}
            onDoiPhongBan={(v) => { setPhongBanId(v); setTrang(1) }}
            onDoiNhanVien={(v) => { setNhanVienId(v); setTrang(1) }}
          />

          <div className="w-44">
            <Label htmlFor="loc-nguon">{t('crmLoc.nguon')}</Label>
            <SelectTimKiem
              id="loc-nguon"
              luaChon={[
                { giaTri: 'NhanVienTao', nhan: t('crmLoc.nguonNhanVien') },
                { giaTri: 'TuDangKy', nhan: t('crmLoc.nguonTuDangKy') },
              ]}
              giaTri={nguon}
              onDoi={(v) => { setNguon(v); setTrang(1) }}
              placeholder={t('chung.tatCa')}
            />
          </div>

          {/* Ngày TẠO HỒ SƠ, không phải ngày mua — nhãn nói rõ để không ai đọc nhầm thành
              doanh thu theo kỳ. */}
          <div className="w-40">
            <Label htmlFor="loc-tu">{t('crmLoc.taoTuNgay')}</Label>
            <Input
              id="loc-tu"
              type="date"
              value={tuNgay}
              onChange={(e) => { setTuNgay(e.target.value); setTrang(1) }}
            />
          </div>
          <div className="w-40">
            <Label htmlFor="loc-den">{t('crmLoc.denNgay')}</Label>
            <Input
              id="loc-den"
              type="date"
              value={denNgay}
              onChange={(e) => { setDenNgay(e.target.value); setTrang(1) }}
            />
          </div>
        </div>
      )}

      {maLoiBang && <CanhBaoLoi>{t(`loi.${maLoiBang}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      <Card>
        <CardContent className="pt-6">
          {isLoading ? (
            <TrangTrong thongDiep={t('chung.dangTai')} />
          ) : kq.duLieu.length === 0 ? (
            <TrangTrong thongDiep={t('khachHang.chuaCo')} />
          ) : (
            <>
              <Table>
                <thead>
                  <tr>
                    <Th>{t('khachHang.hoTen')}</Th>
                    <Th>{t('khachHang.lienHe')}</Th>
                    <Th>{t('khachHang.phuongThuc')}</Th>
                    <Th className="text-right">{t('khachHang.soKhoa')}</Th>
                    <Th className="text-right">{t('khachHang.tongMua')}</Th>
                    <Th>{t('khachHang.hocVien')}</Th>
                    <Th />
                  </tr>
                </thead>
                <tbody>
                  {kq.duLieu.map((k) => (
                    <tr
                      key={k.id}
                      className="cursor-pointer hover:bg-muted/40"
                      onClick={() => navigate(`/crm/khach-hang/${k.id}`)}
                    >
                      <Td>
                        <div className="font-medium">{k.hoTen}</div>
                        {k.ghiChu && (
                          <div
                            className="max-w-xs truncate text-xs text-muted-foreground"
                            title={k.ghiChu}
                          >
                            {k.ghiChu}
                          </div>
                        )}
                      </Td>
                      <Td className="text-muted-foreground">
                        <div className="flex flex-col gap-0.5 text-xs">
                          {k.soDienThoai && <span>{k.soDienThoai}</span>}
                          {k.email && <span className="truncate">{k.email}</span>}
                          {k.linkFacebook && (
                            <a
                              href={k.linkFacebook}
                              target="_blank"
                              rel="noreferrer noopener"
                              className="inline-flex items-center gap-1 text-primary hover:underline"
                            >
                              Facebook
                              <ExternalLink className="h-3 w-3" />
                            </a>
                          )}
                          {!k.soDienThoai && !k.email && !k.linkFacebook && '—'}
                        </div>
                      </Td>
                      <Td className="text-muted-foreground">
                        {t(`phuongThucThanhToan.${k.phuongThucThanhToan}`)}
                      </Td>
                      <Td className="text-right">
                        {k.soDangKy > 0 ? (
                          <Badge variant="ok">{k.soDangKy}</Badge>
                        ) : (
                          <span className="text-muted-foreground">0</span>
                        )}
                      </Td>
                      <Td className="text-right">
                        {k.soDangKy > 0 ? tien(k.tongMuaVnd) : '—'}
                      </Td>
                      <Td className="text-muted-foreground">
                        {k.tenHocVien ? (
                          <Badge variant="accent">{k.tenHocVien}</Badge>
                        ) : (
                          t('khachHang.chuaVaoHoc')
                        )}
                      </Td>
                      {/* Chặn nổi bọt: bấm menu không được đồng thời mở view chi tiết. */}
                      <Td onClick={(e) => e.stopPropagation()}>
                        <div className="flex justify-end">
                          <MenuThaoTac
                            nhanMo={t('chung.thaoTac')}
                            muc={[
                              {
                                nhan: t('chung.xemChiTiet'),
                                icon: Eye,
                                onChon: () => navigate(`/crm/khach-hang/${k.id}`),
                              },
                              {
                                nhan: t('chung.sua'),
                                icon: Pencil,
                                an: !coQuyen('KhachHang', 'Sua'),
                                onChon: () => moSua(k),
                              },
                              {
                                nhan: t('khachHang.capTaiKhoan'),
                                icon: KeyRound,
                                ngatNhom: true,
                                // Ẩn khi khách ĐÃ có hồ sơ học viên: nối rồi thì cấp tài khoản
                                // là việc của màn Quản trị › Tài khoản, không nhân đôi ở đây.
                                an:
                                  !!k.nguoiDungId
                                  || !coQuyen('TaiKhoan', 'Them')
                                  || !coQuyen('KhachHang', 'Sua'),
                                onChon: () => {
                                  setMaLoi(null)
                                  setCapTkCho(k)
                                },
                              },
                              {
                                nhan: t('chung.xoa'),
                                icon: Trash2,
                                nguyHiem: true,
                                ngatNhom: true,
                                // Khách đã mua không xoá được — đăng ký là dữ liệu tiền.
                                an: !coQuyen('KhachHang', 'Xoa') || k.soDangKy > 0,
                                onChon: () =>
                                  hoi({
                                    tieuDe: t('chung.xacNhanXoa'),
                                    thongDiep: t('khachHang.hoiXoa', { ten: k.hoTen }),
                                    nhanDongY: t('chung.xoa'),
                                    nguyHiem: true,
                                    onDongY: () => xoa.mutate(k.id),
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
            </>
          )}
        </CardContent>
      </Card>

      <Modal
        mo={moForm}
        onDong={dong}
        chanDoiKhiXuLy={luu.isPending}
        tieuDe={dangSua ? t('khachHang.sua') : t('khachHang.them')}
        moTa={dangSua?.hoTen}
        rong="md"
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
            <Input id="hoTen" name="hoTen" required defaultValue={dangSua?.hoTen ?? ''} autoFocus />
          </div>

          <div className="grid gap-3 sm:grid-cols-2">
            <div>
              <Label htmlFor="soDienThoai">{t('khachHang.soDienThoai')}</Label>
              <Input
                id="soDienThoai"
                name="soDienThoai"
                defaultValue={dangSua?.soDienThoai ?? ''}
                onChange={(e) => setSdtDangGo(e.target.value.trim())}
              />

              {/*
                Cảnh báo NGAY dưới ô, không đợi bấm Lưu. Kèm nút mở hồ sơ: người dùng đang định
                thêm một người mà hoá ra đã có — việc tiếp theo của họ luôn là xem người đó.
              */}
              {canhBaoTrung && (
                <div className="mt-1.5 rounded-md border border-status-cho/40 bg-status-cho/10 px-2.5 py-2 text-xs">
                  <div className="text-foreground">
                    {t('khachHang.sdtDaCo', { ten: canhBaoTrung.hoTen })}
                  </div>
                  <button
                    type="button"
                    className="mt-1 font-medium text-[hsl(var(--primary))] hover:underline"
                    onClick={() => {
                      dong()
                      navigate(`/crm/khach-hang/${canhBaoTrung.id}`)
                    }}
                  >
                    {t('khachHang.moHoSoNay')} →
                  </button>
                </div>
              )}
            </div>
            <div>
              <Label htmlFor="email">{t('khachHang.email')}</Label>
              <Input id="email" name="email" type="email" defaultValue={dangSua?.email ?? ''} />
            </div>
          </div>

          <div>
            <Label htmlFor="linkFacebook">{t('khachHang.linkFacebook')}</Label>
            <Input
              id="linkFacebook"
              name="linkFacebook"
              placeholder="https://facebook.com/..."
              defaultValue={dangSua?.linkFacebook ?? ''}
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
            <p className="mt-1 text-xs text-muted-foreground">{t('khachHang.phuongThucGoiY')}</p>
          </div>

          {/* Nối với hồ sơ học viên — chỉ hiện khi người dùng đọc được danh sách học viên. */}
          {coQuyen('TaiKhoan') && (
            <div>
              <Label htmlFor="hocVien">{t('khachHang.noiHocVien')}</Label>
              <SelectTimKiem
                id="hocVien"
                luaChon={hocViens.map((h) => ({ giaTri: h.id, nhan: h.hoTen }))}
                giaTri={hocVienId}
                onDoi={setHocVienId}
                placeholder={t('khachHang.chuaVaoHoc')}
              />
              <p className="mt-1 text-xs text-muted-foreground">{t('khachHang.noiHocVienGoiY')}</p>
            </div>
          )}

          <div>
            <Label htmlFor="ghiChu">{t('chung.ghiChu')}</Label>
            <Textarea id="ghiChu" name="ghiChu" rows={3} defaultValue={dangSua?.ghiChu ?? ''} />
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

      {/* ---------- Cấp tài khoản học viên (13/09/2026) ---------- */}

      <Modal

        mo={!!capTkCho}

        onDong={() => setCapTkCho(null)}

        tieuDe={t('khachHang.capTaiKhoan')}

      >

        <form

          /* `key` để form dựng lại theo từng khách — Modal giữ children khi đóng nên

             `defaultValue` chỉ áp lần mount đầu (lỗi đã gặp ở màn khoá online 13/09). */

          key={capTkCho?.id ?? 'none'}

          onSubmit={(e) => {
            e.preventDefault()
            const fd = new FormData(e.currentTarget)
            // Xác nhận như MỌI thao tác ghi khác (quy tắc từ 07/09/2026). Ở đây đáng giá hơn
            // bình thường: cấp tài khoản tạo một con người mới trong hệ thống và nối cứng nó
            // với khách hàng — gỡ ra không có đường nào trên giao diện.
            hoi({
              tieuDe: t('chung.xacNhanLuu'),
              thongDiep: t('khachHang.hoiCapTaiKhoan', { ten: capTkCho?.hoTen ?? '' }),
              onDongY: () => capTaiKhoan.mutate(fd),
            })
          }}

          className="grid gap-4"

        >

          {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

          <p className="text-sm text-muted-foreground">

            {t('khachHang.capTaiKhoanGoiY', { ten: capTkCho?.hoTen ?? '' })}

          </p>

          <div>

            <Label htmlFor="username">{t('taiKhoan.username')}</Label>

            <Input id="username" name="username" required maxLength={100} autoComplete="off" />

          </div>

          <div>

            <Label htmlFor="matKhau">{t('taiKhoan.matKhau')}</Label>

            <Input

              id="matKhau"

              name="matKhau"

              type="text"

              required

              minLength={DO_DAI_MAT_KHAU_TOI_THIEU}

              autoComplete="off"

            />

            <p className="mt-1 text-xs text-muted-foreground">

              {t('khachHang.matKhauGoiY')}

            </p>

          </div>

          <div className="flex justify-end gap-2">

            <Button type="button" variant="outline" onClick={() => setCapTkCho(null)}>

              {t('chung.huy')}

            </Button>

            <Button type="submit" disabled={capTaiKhoan.isPending}>

              {t('chung.luu')}

            </Button>

          </div>

        </form>

      </Modal>


      {hop}
    </div>
  )
}
