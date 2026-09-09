import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { Plus, Trash2, Pencil } from 'lucide-react'
import { api, layMaLoi, trangRong, type KetQuaTrang } from '@/lib/api'
import { useQuyen } from '@/lib/quyen'
import { useXacNhan } from '@/lib/xacNhan'
import {
  Badge, Button, CanhBaoLoi, Input, Label, Table, Td, Textarea, Th, TrangTrong,
} from '@/components/ui'
import { Modal, ModalChan } from '@/components/ui/Modal'
import { PhanTrang } from '@/components/ui/PhanTrang'
import { MenuThaoTac } from '@/components/ui/MenuThaoTac'
import { SelectTimKiem, SelectTimKiemNhieu } from '@/components/ui/SelectTimKiem'

/** FR-22 — một node của cây cơ cấu tổ chức. */
export interface PhongBanNode {
  id: string
  ten: string
  phongBanChaId: string | null
  nguoiQuanLyId: string | null
  tenNguoiQuanLy: string | null
  moTa: string | null
  thuTu: number
  /** Số nhân sự thuộc CHÍNH phòng này, không gồm phòng con. */
  soNhanSu: number
  /** Số nhân sự cả nhánh (phòng này + mọi cấp dưới). */
  soNhanSuCaNhanh: number
  phongBanCons: PhongBanNode[]
}

/** FR-23 — loại liên kết mạng xã hội. */
export type LoaiMxh = 'Facebook' | 'Zalo' | 'LinkedIn' | 'Telegram' | 'Khac'

export const CAC_LOAI_MXH: LoaiMxh[] = ['Facebook', 'Zalo', 'LinkedIn', 'Telegram', 'Khac']

export type LoaiNguoiDung = 'NhanVien' | 'GiaoVien' | 'TroGiang' | 'HocVien'
export type TrangThaiNhanSu = 'DangLamViec' | 'DaNghi'


interface HoSoGiaoVien {
  bangCap: string | null
  chuyenMon: string | null
  ngayVaoLam: string | null
}
interface HoSoHocVien {
  truongLop: string | null
  tenPhuHuynh: string | null
  soDienThoaiPhuHuynh: string | null
}
export interface NguoiDungDto {
  id: string
  hoTen: string
  email: string | null
  soDienThoai: string | null
  diaChi: string | null
  ngaySinh: string | null
  anhDaiDienUrl: string | null
  loaiNguoiDung: LoaiNguoiDung
  trangThaiNhanSu: TrangThaiNhanSu
  hoSoGiaoVien: HoSoGiaoVien | null
  hoSoHocVien: HoSoHocVien | null
  /** FR-22 — null = chưa xếp vào cơ cấu. */
  phongBanId: string | null
  tenPhongBan: string | null
  /** FR-24 — null = chưa gán chức vụ. */
  chucVuId: string | null
  tenChucVu: string | null
  // FR-23
  cccd: string | null
  soTaiKhoan: string | null
  tenNganHang: string | null
  ghiChu: string | null
  lienKetMxhs: { id: string; loai: LoaiMxh; duongDan: string; ghiChu: string | null }[]
  tepHoSos: {
    id: string; tenGoc: string; khoaLuuTru: string; loaiNoiDung: string
    kichThuoc: number; ngayTao: string
  }[]
  username: string | null
  trangThaiTaiKhoan: 'HoatDong' | 'VoHieuHoa' | null
}

interface QuyenNgan {
  id: string
  tenQuyen: string
}

/** Trợ giảng dùng chung hồ sơ giáo viên — cùng loại thông tin, không đáng tách bảng. */
const laGiaoVien = (l: LoaiNguoiDung) => l === 'GiaoVien' || l === 'TroGiang'

const ngayChoInput = (iso: string | null) => (iso ? iso.slice(0, 10) : '')

/**
 * Phạm vi của một màn hồ sơ con người: nó quản vai trò nào, gọi endpoint nào, gác quyền nào.
 *
 * Một component dùng cho HAI màn (08/09/2026): Nhân sự bên HRM và Học viên bên LMS. Chép thành
 * hai file là chép ~600 dòng form ba loại hồ sơ, và từ đó hai bản sẽ trôi khỏi nhau — sửa lỗi
 * một bên quên bên kia.
 */
export interface PhamViNguoiDung {
  /** Endpoint gốc, ví dụ `/nhan-su` hoặc `/hoc-vien`. */
  duong: string
  /** Vai trò màn này quản. Một phần tử thì ẩn luôn ô lọc và ô chọn vai trò. */
  vaiTro: LoaiNguoiDung[]
  /** Chức năng phân quyền gác màn này — `GiaoVienNhanSu` (HRM) hoặc `LopHoc` (LMS). */
  can: string
  /** Khoá i18n của tiêu đề, dùng cho thông báo xác nhận xoá. */
  khoaTieuDe: string
  /**
   * Đường dẫn view chi tiết, ví dụ `/hrm/nhan-su`. Bỏ trống = dòng không bấm được.
   *
   * Tách khỏi `duong` (endpoint API) vì hai thứ khác nhau: `/nhan-su` là API, `/hrm/nhan-su`
   * là route frontend.
   */
  duongChiTiet?: string
}

/**
 * FR-03 — người dùng (hồ sơ con người).
 *
 * Tách khỏi tài khoản (07/09/2026): vô hiệu hoá tài khoản không đụng tới dữ liệu người dùng,
 * nên giáo viên đã nghỉ vẫn giữ nguyên tên trong lịch sử lớp và vẫn phân công được vào lớp cũ.
 *
 * Chia hai màn theo hệ thống (08/09/2026): nhân sự → HRM, học viên → LMS. Xem
 * {@link PhamViNguoiDung}.
 */
export default function NguoiDung({ phamVi }: { phamVi: PhamViNguoiDung }) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const { coQuyen } = useQuyen()
  const { hoi, hop } = useXacNhan()

  const [trang, setTrang] = useState(1)
  const [soDong, setSoDong] = useState(20)
  const [timKiem, setTimKiem] = useState('')
  const [locVaiTro, setLocVaiTro] = useState<string | null>(null)
  const [locNhanSu, setLocNhanSu] = useState<string | null>(null)

  const [moForm, setMoForm] = useState(false)
  const [dangSua, setDangSua] = useState<NguoiDungDto | null>(null)
  const [loai, setLoai] = useState<LoaiNguoiDung>(phamVi.vaiTro[0])
  const [nhanSu, setNhanSu] = useState<TrangThaiNhanSu>('DangLamViec')
  const [taoTaiKhoan, setTaoTaiKhoan] = useState(false)
  const [quyenChon, setQuyenChon] = useState<string[]>([])
  const [buocDoiMk, setBuocDoiMk] = useState(true)
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [maLoiBang, setMaLoiBang] = useState<string | null>(null)
  /** Phòng ban đang chọn trên form (FR-22) — null = chưa xếp vào cơ cấu. */
  const [phongBan, setPhongBan] = useState<string | null>(null)
  /** Chức vụ đang chọn (FR-24) — null = chưa gán. */
  const [chucVu, setChucVu] = useState<string | null>(null)
  /** Liên kết MXH đang nhập (FR-23) — danh sách này THAY THẾ toàn bộ khi lưu. */
  const [mxhs, setMxhs] = useState<{ loai: LoaiMxh; duongDan: string }[]>([])

  const doiMxh = (i: number, thayDoi: Partial<{ loai: LoaiMxh; duongDan: string }>) =>
    setMxhs((cu) => cu.map((m, k) => (k === i ? { ...m, ...thayDoi } : m)))

  const { data: kq = trangRong<NguoiDungDto>(), isLoading } = useQuery({
    queryKey: [phamVi.duong, timKiem, locVaiTro, locNhanSu, trang, soDong],
    queryFn: async () =>
      (await api.get<KetQuaTrang<NguoiDungDto>>(phamVi.duong, {
        params: {
          timKiem: timKiem || undefined,
          loaiNguoiDung: locVaiTro || undefined,
          trangThaiNhanSu: locNhanSu || undefined,
          trang,
          soDong,
        },
      })).data,
  })

  const { data: quyens } = useQuery({
    queryKey: ['quyen'],
    queryFn: async () => (await api.get<QuyenNgan[]>('/quyen')).data,
  })

  /**
   * Cây phòng ban, làm phẳng để đưa vào select (FR-22).
   *
   * Nhãn mang cả đường dẫn cha ("Ban giám đốc / Đào tạo / Bộ môn Anh") vì tên lá hay trùng nhau
   * giữa các nhánh — "Bộ môn Anh" dưới hai chi nhánh là hợp lệ, hiện tên trần thì không phân
   * biệt được.
   *
   * Không tải khi form đóng: người chỉ xem danh sách không cần thêm một request.
   */
  const { data: cayPhongBan = [] } = useQuery({
    queryKey: ['phong-ban'],
    queryFn: async () => (await api.get<PhongBanNode[]>('/phong-ban')).data,
    enabled: moForm,
  })

  /** Danh mục chức vụ (FR-24) — chỉ lấy chức vụ CÒN DÙNG cho form chọn. */
  const { data: chucVus = [] } = useQuery({
    queryKey: ['chuc-vu', 'dang-dung'],
    queryFn: async () =>
      (await api.get<{ id: string; ten: string }[]>('/chuc-vu', {
        params: { chiDangDung: true },
      })).data,
    enabled: moForm,
  })

  const phongBanPhang = (() => {
    const ra: { giaTri: string; nhan: string }[] = []
    const di = (ns: PhongBanNode[], duong: string[]) => {
      for (const n of ns) {
        const duongMoi = [...duong, n.ten]
        ra.push({ giaTri: n.id, nhan: duongMoi.join(' / ') })
        di(n.phongBanCons, duongMoi)
      }
    }
    di(cayPhongBan, [])
    return ra
  })()


  /** Đổi người dùng thì danh sách chọn giáo viên/học viên ở màn Lớp học cũng phải mới. */
  const lamMoi = () => {
    // Vô hiệu hoá cache của CẢ hai màn (`/nhan-su`, `/hoc-vien`) và của danh sách rút gọn
    // dùng ở màn Lớp học: sửa tên một giáo viên phải thấy ngay ở ô chọn giáo viên chính.
    void qc.invalidateQueries({ queryKey: [phamVi.duong] })
    void qc.invalidateQueries({ queryKey: ['nguoi-dung'] })
    void qc.invalidateQueries({ queryKey: ['tai-khoan'] })
    void qc.invalidateQueries({ queryKey: ['nguoi-dung-ngan'] })
  }

  const dong = () => {
    setMoForm(false)
    setDangSua(null)
    setTaoTaiKhoan(false)
    setQuyenChon([])
    setMaLoi(null)
  }

  const luu = useMutation({
    mutationFn: async (fd: FormData) => {
      const s = (k: string) => (fd.get(k) as string)?.trim() || null

      // Mọi trường mà lệnh cập nhật ghi đè đều đọc TỪ FORM. Gửi cứng null sẽ xoá dữ liệu
      // người dùng chưa từng đụng tới — đúng lỗi 16/08 với ô địa chỉ (quy tắc #1).
      const than: Record<string, unknown> = {
        hoTen: String(fd.get('hoTen')),
        email: s('email'),
        soDienThoai: s('soDienThoai'),
        diaChi: s('diaChi'),
        ngaySinh: s('ngaySinh'),
        loaiNguoiDung: loai,
        // Ba khối hồ sơ: chỉ gửi khối của vai trò đang chọn. Gửi cả ba sẽ ghi rỗng đè lên
        // hồ sơ vai trò cũ mà form không hiển thị.
        hoSoGiaoVien: laGiaoVien(loai)
          ? {
              bangCap: s('bangCap'),
              chuyenMon: s('chuyenMon'),
              ngayVaoLam: s('ngayVaoLam'),
            }
          : null,
        hoSoHocVien:
          loai === 'HocVien'
            ? {
                truongLop: s('truongLop'),
                tenPhuHuynh: s('tenPhuHuynh'),
                soDienThoaiPhuHuynh: s('soDienThoaiPhuHuynh'),
              }
            : null,
        // Học viên không có chức vụ; các vai trò nhân sự gửi chức vụ đang chọn.
        chucVuId: loai === 'HocVien' ? null : chucVu,
        // FR-23 — học viên không có các trường này.
        cccd: loai === 'HocVien' ? null : s('cccd'),
        soTaiKhoan: loai === 'HocVien' ? null : s('soTaiKhoan'),
        tenNganHang: loai === 'HocVien' ? null : s('tenNganHang'),
        ghiChu: loai === 'HocVien' ? null : s('ghiChuNs'),
        // Bỏ dòng trống trước khi gửi — backend cũng bỏ, nhưng lọc ở đây thì payload gọn hơn.
        lienKetMxhs: loai === 'HocVien'
          ? []
          : mxhs.filter((m) => m.duongDan.trim()).map((m) => ({
              loai: m.loai,
              duongDan: m.duongDan.trim(),
            })),
        // Học viên không vào cơ cấu; các vai trò nhân sự thì gửi phòng ban đang chọn.
        phongBanId: loai === 'HocVien' ? null : phongBan,
      }

      if (dangSua) {
        than.trangThaiNhanSu = nhanSu
        // Cờ bắt buộc khi SỬA: `Guid?` không phân biệt "không gửi" với "gỡ ra", nên backend chỉ
        // ghi phòng ban khi client nói rõ là muốn đổi. Form này LUÔN có ô phòng ban (trừ học
        // viên) nên luôn gửi true.
        than.doiPhongBan = true
        than.doiChucVu = true
        await api.put(`${phamVi.duong}/${dangSua.id}`, { ...than, id: dangSua.id })
      } else {
        if (taoTaiKhoan) {
          than.taiKhoan = {
            username: String(fd.get('username')).trim(),
            matKhau: String(fd.get('matKhau')),
            quyenIds: quyenChon,
            phaiDoiMatKhau: buocDoiMk,
          }
        }
        await api.post(phamVi.duong, than)
      }
    },
    onSuccess: () => {
      lamMoi()
      dong()
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const xoa = useMutation({
    mutationFn: async (id: string) => api.delete(`${phamVi.duong}/${id}`),
    onSuccess: lamMoi,
    onError: (e) => setMaLoiBang(layMaLoi(e)),
  })

  const moSua = (u: NguoiDungDto) => {
    setDangSua(u)
    setLoai(u.loaiNguoiDung)
    setPhongBan(u.phongBanId ?? null)
    setChucVu(u.chucVuId ?? null)
    setMxhs(u.lienKetMxhs.map((m) => ({ loai: m.loai, duongDan: m.duongDan })))
    setNhanSu(u.trangThaiNhanSu)
    setMaLoi(null)
    setMoForm(true)
  }

  return (
    <div className="space-y-4">
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
              placeholder={t('nguoiDung.timTheo')}
            />
          </div>
          {/* Màn chỉ có MỘT vai trò (Học viên) thì ô lọc này luôn ra cùng kết quả — ẩn đi. */}
          {phamVi.vaiTro.length > 1 && (
            <div className="w-44">
              <Label htmlFor="loc-vai-tro">{t('taiKhoan.loaiNguoiDung')}</Label>
              <SelectTimKiem
                id="loc-vai-tro"
                luaChon={phamVi.vaiTro.map((l) => ({
                  giaTri: l,
                  nhan: t(`loaiNguoiDung.${l}`),
                }))}
                giaTri={locVaiTro}
                onDoi={(v) => {
                  setLocVaiTro(v)
                  setTrang(1)
                }}
                placeholder={t('chung.tatCa')}
              />
            </div>
          )}
          <div className="w-44">
            <Label htmlFor="loc-nhan-su">{t('nguoiDung.trangThaiNhanSu')}</Label>
            <SelectTimKiem
              id="loc-nhan-su"
              luaChon={[
                { giaTri: 'DangLamViec', nhan: t('nguoiDung.DangLamViec') },
                { giaTri: 'DaNghi', nhan: t('nguoiDung.DaNghi') },
              ]}
              giaTri={locNhanSu}
              onDoi={(v) => {
                setLocNhanSu(v)
                setTrang(1)
              }}
              placeholder={t('chung.tatCa')}
            />
          </div>
        </div>

        {coQuyen(phamVi.can, 'Them') && (
          <Button
            onClick={() => {
              setDangSua(null)
              setLoai(phamVi.vaiTro[0])
              setNhanSu('DangLamViec')
              setTaoTaiKhoan(false)
              setQuyenChon([])
              setBuocDoiMk(true)
              setMaLoi(null)
              setMoForm(true)
            }}
          >
            <Plus className="h-4 w-4" />
            {t('chung.them')}
          </Button>
        )}
      </div>

      {maLoiBang && <CanhBaoLoi>{t(`loi.${maLoiBang}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

      {isLoading ? (
        <TrangTrong thongDiep={t('chung.dangTai')} />
      ) : kq.duLieu.length === 0 ? (
        <TrangTrong thongDiep={t('chung.khongCoDuLieu')} />
      ) : (
        <>
          <Table>
            <thead>
              <tr>
                <Th>{t('taiKhoan.hoTen')}</Th>
                <Th>{t('taiKhoan.loaiNguoiDung')}</Th>
                <Th>{t('nguoiDung.hoSo')}</Th>
                <Th>{t('taiKhoan.email')}</Th>
                <Th>{t('nguoiDung.taiKhoan')}</Th>
                <Th>{t('nguoiDung.trangThaiNhanSu')}</Th>
                <Th />
              </tr>
            </thead>
            <tbody>
              {kq.duLieu.map((u) => (
                <tr
                  key={u.id}
                  // Bấm vào dòng mở view chi tiết (yêu cầu 09/09/2026). Chỉ ở màn có
                  // `duongChiTiet` — màn Học viên (LMS) dùng chung component này nhưng chưa có
                  // view riêng, nên ở đó dòng vẫn không bấm được.
                  onClick={
                    phamVi.duongChiTiet ? () => navigate(`${phamVi.duongChiTiet}/${u.id}`) : undefined
                  }
                  className={phamVi.duongChiTiet ? 'cursor-pointer hover:bg-muted/40' : undefined}
                >
                  <Td className="font-medium">{u.hoTen}</Td>
                  <Td>{t(`loaiNguoiDung.${u.loaiNguoiDung}`)}</Td>
                  <Td className="text-muted-foreground">
                    {laGiaoVien(u.loaiNguoiDung)
                      ? u.hoSoGiaoVien?.chuyenMon ?? '—'
                      : u.loaiNguoiDung === 'HocVien'
                        ? u.hoSoHocVien?.tenPhuHuynh ?? '—'
                        : u.tenChucVu ?? '—'}
                  </Td>
                  <Td className="text-muted-foreground">{u.email ?? '—'}</Td>
                  <Td>
                    {u.username ? (
                      <span className="flex items-center gap-2">
                        <span className="text-muted-foreground">{u.username}</span>
                        {u.trangThaiTaiKhoan === 'VoHieuHoa' && (
                          <Badge variant="loi">{t('taiKhoan.VoHieuHoa')}</Badge>
                        )}
                      </span>
                    ) : (
                      <span className="text-muted-foreground">{t('nguoiDung.chuaCoTaiKhoan')}</span>
                    )}
                  </Td>
                  <Td>
                    <Badge variant={u.trangThaiNhanSu === 'DangLamViec' ? 'ok' : 'muted'}>
                      {t(`nguoiDung.${u.trangThaiNhanSu}`)}
                    </Badge>
                  </Td>
                  <Td>
                    {/* Chặn nổi bọt: bấm "Sửa"/"Xoá" trong dòng bấm-được sẽ vừa mở modal vừa
                        điều hướng sang view chi tiết. */}
                    <div className="flex justify-end" onClick={(e) => e.stopPropagation()}>
                      <MenuThaoTac
                        nhanMo={t('chung.thaoTac')}
                        muc={[
                          {
                            nhan: t('chung.sua'),
                            icon: Pencil,
                            an: !coQuyen(phamVi.can, 'Sua'),
                            onChon: () => moSua(u),
                          },
                          {
                            nhan: t('chung.xoa'),
                            icon: Trash2,
                            nguyHiem: true,
                            ngatNhom: true,
                            an: !coQuyen(phamVi.can, 'Xoa'),
                            onChon: () =>
                              hoi({
                                tieuDe: t('chung.xacNhanXoa'),
                                thongDiep: t('nguoiDung.hoiXoa', { ten: u.hoTen }),
                                nhanDongY: t('chung.xoa'),
                                nguyHiem: true,
                                onDongY: () => xoa.mutate(u.id),
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
            trang={trang}
            soDong={soDong}
            tongSoDong={kq.tongSoDong}
            tongSoTrang={Math.max(1, Math.ceil(kq.tongSoDong / soDong))}
            onDoiTrang={setTrang}
            onDoiSoDong={(n) => {
              setSoDong(n)
              setTrang(1)
            }}
          />
        </>
      )}

      <Modal
        mo={moForm}
        onDong={dong}
        tieuDe={dangSua ? t('nguoiDung.suaNguoiDung') : t('nguoiDung.themNguoiDung')}
        rong="lg"
      >
        <form
          key={dangSua?.id ?? 'moi'}
          onSubmit={(e) => {
            e.preventDefault()
            const fd = new FormData(e.currentTarget)
            const ten = String(fd.get('hoTen'))
            hoi({
              tieuDe: dangSua ? t('chung.xacNhanLuu') : t('chung.xacNhanThem'),
              thongDiep: dangSua
                ? t('chung.hoiLuu', { ten })
                : t('chung.hoiThem', { ten }),
              onDongY: () => luu.mutate(fd),
            })
          }}
          className="space-y-4"
        >
          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <Label htmlFor="hoTen">{t('taiKhoan.hoTen')} *</Label>
              <Input id="hoTen" name="hoTen" required defaultValue={dangSua?.hoTen ?? ''} />
            </div>
            {/*
              Chỉ chọn được vai trò TRONG phạm vi màn hình. Để cả bốn thì màn Nhân sự tạo
              được học viên, và backend sẽ chặn bằng `KHONG_PHAI_NHAN_SU` — người dùng điền
              xong form mới nhận lỗi, đúng kiểu lỗi "mời làm việc chắc chắn thất bại" đã gặp
              với form nhận xét buổi học.

              Phạm vi một vai trò (Học viên) thì không có gì để chọn — ẩn hẳn.
            */}
            {phamVi.vaiTro.length > 1 && (
              <div>
                <Label htmlFor="loai">{t('taiKhoan.loaiNguoiDung')} *</Label>
                <SelectTimKiem
                  id="loai"
                  luaChon={phamVi.vaiTro.map((l) => ({
                    giaTri: l,
                    nhan: t(`loaiNguoiDung.${l}`),
                  }))}
                  giaTri={loai}
                  onDoi={(v) => setLoai((v as LoaiNguoiDung) ?? phamVi.vaiTro[0])}
                  choPhepXoa={false}
                />
              </div>
            )}
            <div>
              <Label htmlFor="ngaySinh">{t('taiKhoan.ngaySinh')}</Label>
              <Input
                id="ngaySinh"
                name="ngaySinh"
                type="date"
                defaultValue={ngayChoInput(dangSua?.ngaySinh ?? null)}
              />
            </div>
            <div>
              <Label htmlFor="soDienThoai">{t('taiKhoan.soDienThoai')}</Label>
              <Input
                id="soDienThoai"
                name="soDienThoai"
                defaultValue={dangSua?.soDienThoai ?? ''}
              />
            </div>
            <div>
              <Label htmlFor="email">{t('taiKhoan.email')}</Label>
              <Input id="email" name="email" type="email" defaultValue={dangSua?.email ?? ''} />
            </div>
            {dangSua && (
              <div>
                <Label htmlFor="nhanSu">{t('nguoiDung.trangThaiNhanSu')}</Label>
                <SelectTimKiem
                  id="nhanSu"
                  luaChon={[
                    { giaTri: 'DangLamViec', nhan: t('nguoiDung.DangLamViec') },
                    { giaTri: 'DaNghi', nhan: t('nguoiDung.DaNghi') },
                  ]}
                  giaTri={nhanSu}
                  onDoi={(v) => setNhanSu((v as TrangThaiNhanSu) ?? 'DangLamViec')}
                  choPhepXoa={false}
                />
              </div>
            )}
            <div className="sm:col-span-2">
              <Label htmlFor="diaChi">{t('taiKhoan.diaChi')}</Label>
              <Input id="diaChi" name="diaChi" defaultValue={dangSua?.diaChi ?? ''} />
            </div>
          </div>

          {/* Hồ sơ riêng theo vai trò — chỉ hiện khối của vai trò đang chọn. */}
          <fieldset className="space-y-3 rounded-lg border border-border p-3">
            <legend className="px-1 text-sm font-medium">
              {t(`nguoiDung.hoSo_${laGiaoVien(loai) ? 'GiaoVien' : loai}`)}
            </legend>

            {laGiaoVien(loai) && (
              <div className="grid gap-4 sm:grid-cols-3">
                <div>
                  <Label htmlFor="bangCap">{t('nguoiDung.bangCap')}</Label>
                  <Input
                    id="bangCap"
                    name="bangCap"
                    defaultValue={dangSua?.hoSoGiaoVien?.bangCap ?? ''}
                  />
                </div>
                <div>
                  <Label htmlFor="chuyenMon">{t('nguoiDung.chuyenMon')}</Label>
                  <Input
                    id="chuyenMon"
                    name="chuyenMon"
                    defaultValue={dangSua?.hoSoGiaoVien?.chuyenMon ?? ''}
                  />
                </div>
                <div>
                  <Label htmlFor="ngayVaoLam">{t('nguoiDung.ngayVaoLam')}</Label>
                  <Input
                    id="ngayVaoLam"
                    name="ngayVaoLam"
                    type="date"
                    defaultValue={ngayChoInput(dangSua?.hoSoGiaoVien?.ngayVaoLam ?? null)}
                  />
                </div>
              </div>
            )}

            {loai === 'HocVien' && (
              <div className="grid gap-4 sm:grid-cols-3">
                <div>
                  <Label htmlFor="truongLop">{t('nguoiDung.truongLop')}</Label>
                  <Input
                    id="truongLop"
                    name="truongLop"
                    defaultValue={dangSua?.hoSoHocVien?.truongLop ?? ''}
                  />
                </div>
                <div>
                  <Label htmlFor="tenPhuHuynh">{t('nguoiDung.tenPhuHuynh')}</Label>
                  <Input
                    id="tenPhuHuynh"
                    name="tenPhuHuynh"
                    defaultValue={dangSua?.hoSoHocVien?.tenPhuHuynh ?? ''}
                  />
                </div>
                <div>
                  <Label htmlFor="soDienThoaiPhuHuynh">{t('nguoiDung.sdtPhuHuynh')}</Label>
                  <Input
                    id="soDienThoaiPhuHuynh"
                    name="soDienThoaiPhuHuynh"
                    defaultValue={dangSua?.hoSoHocVien?.soDienThoaiPhuHuynh ?? ''}
                  />
                </div>
              </div>
            )}

            {/*
              CHỨC VỤ (FR-24) — select từ danh mục, hiện cho MỌI vai trò nhân sự.

              Trước 09/09/2026 là ô chuỗi tự do và chỉ hiện cho `NhanVien`. Đổi vì hai lý do:
              chuỗi tự do thì "Trưởng phòng" và "trưởng phòng" là hai chức vụ khác nhau; và
              giáo viên cũng làm trưởng bộ môn nên không có lý do giới hạn theo vai trò.

              **Chức vụ KHÁC `LoaiNguoiDung`**: đây là chức danh ("Ban quản lý"), còn
              `LoaiNguoiDung` là loại nghiệp vụ quyết định ai gán được vào lớp.
            */}
            {loai !== 'HocVien' && (
              <div>
                <Label htmlFor="chucVu">{t('nguoiDung.chucVu')}</Label>
                <SelectTimKiem
                  id="chucVu"
                  luaChon={chucVus.map((c) => ({ giaTri: c.id, nhan: c.ten }))}
                  giaTri={chucVu}
                  onDoi={setChucVu}
                  placeholder={t('nguoiDung.chuaGanChucVu')}
                  placeholderTimKiem={t('nguoiDung.chucVu')}
                />
              </div>
            )}

            {/*
              FR-23 — hồ sơ mở rộng. Hiện cho MỌI vai trò nhân sự (học viên không có): CCCD và
              số tài khoản là thứ trung tâm cần cho hợp đồng và trả lương, áp cho cả giáo viên.
            */}
            {loai !== 'HocVien' && (
              <>
                <div className="grid gap-4 sm:grid-cols-2">
                  <div>
                    <Label htmlFor="cccd">{t('nguoiDung.cccd')}</Label>
                    <Input id="cccd" name="cccd" defaultValue={dangSua?.cccd ?? ''} />
                  </div>
                  <div>
                    <Label htmlFor="soTaiKhoan">{t('nguoiDung.soTaiKhoan')}</Label>
                    <Input
                      id="soTaiKhoan"
                      name="soTaiKhoan"
                      defaultValue={dangSua?.soTaiKhoan ?? ''}
                    />
                  </div>
                </div>
                <div>
                  <Label htmlFor="tenNganHang">{t('nguoiDung.tenNganHang')}</Label>
                  <Input
                    id="tenNganHang"
                    name="tenNganHang"
                    defaultValue={dangSua?.tenNganHang ?? ''}
                  />
                </div>

                {/* LIÊN KẾT MXH — nhiều dòng. Danh sách này THAY THẾ toàn bộ khi lưu, nên phải
                    nạp đủ liên kết hiện có vào state lúc mở form sửa. */}
                <div className="grid gap-2">
                  <Label>{t('nguoiDung.lienKetMxh')}</Label>
                  {mxhs.map((m, i) => (
                    <div key={i} className="flex flex-wrap items-end gap-2">
                      <div className="w-36">
                        <SelectTimKiem
                          id={`mxh-loai-${i}`}
                          luaChon={CAC_LOAI_MXH.map((l) => ({
                            giaTri: l,
                            nhan: t(`loaiMxh.${l}`),
                          }))}
                          giaTri={m.loai}
                          onDoi={(v) => doiMxh(i, { loai: (v as LoaiMxh) ?? 'Facebook' })}
                          choPhepXoa={false}
                        />
                      </div>
                      <Input
                        className="min-w-48 flex-1"
                        placeholder={t('nguoiDung.duongDanMxh')}
                        value={m.duongDan}
                        onChange={(e) => doiMxh(i, { duongDan: e.target.value })}
                      />
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        onClick={() => setMxhs(mxhs.filter((_, k) => k !== i))}
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </div>
                  ))}
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    className="justify-self-start"
                    onClick={() => setMxhs([...mxhs, { loai: 'Facebook', duongDan: '' }])}
                  >
                    <Plus className="h-4 w-4" />
                    {t('nguoiDung.themMxh')}
                  </Button>
                </div>

                <div>
                  <Label htmlFor="ghiChuNs">{t('chung.ghiChu')}</Label>
                  <Textarea
                    id="ghiChuNs"
                    name="ghiChuNs"
                    rows={2}
                    defaultValue={dangSua?.ghiChu ?? ''}
                  />
                </div>
              </>
            )}

            {/*
              PHÒNG BAN (FR-22) — hiện cho MỌI vai trò nhân sự, không riêng NhanVien: chốt
              09/09/2026 "giáo viên cũng là nhân viên", nên "Bộ môn Anh" gồm giáo viên là cách
              dùng cơ cấu tự nhiên nhất.

              Học viên KHÔNG có ô này (backend cũng chặn) — họ là khách, không phải nhân sự.

              Đây là **cách 1** của FR-22; cách 2 là vào cây cơ cấu chọn người đã có.
            */}
            {loai !== 'HocVien' && (
              <div>
                <Label htmlFor="phongBan">{t('nguoiDung.phongBan')}</Label>
                <SelectTimKiem
                  id="phongBan"
                  luaChon={phongBanPhang}
                  giaTri={phongBan}
                  onDoi={setPhongBan}
                  placeholder={t('nguoiDung.chuaXepPhongBan')}
                  placeholderTimKiem={t('nguoiDung.phongBan')}
                />
              </div>
            )}
          </fieldset>

          {/* Tạo tài khoản kèm theo — chỉ khi thêm mới. Sửa tài khoản làm ở tab bên cạnh. */}
          {!dangSua && (
            <fieldset className="space-y-3 rounded-lg border border-border p-3">
              <legend className="px-1">
                <label className="flex items-center gap-2 text-sm font-medium">
                  <input
                    type="checkbox"
                    checked={taoTaiKhoan}
                    onChange={(e) => setTaoTaiKhoan(e.target.checked)}
                    className="h-4 w-4 rounded border-input"
                  />
                  {t('nguoiDung.taoTaiKhoanDangNhap')}
                </label>
              </legend>

              {taoTaiKhoan ? (
                <div className="grid gap-4 sm:grid-cols-2">
                  <div>
                    <Label htmlFor="username">{t('taiKhoan.username')} *</Label>
                    <Input id="username" name="username" required={taoTaiKhoan} />
                  </div>
                  <div>
                    <Label htmlFor="matKhau">{t('taiKhoan.matKhau')} *</Label>
                    <Input
                      id="matKhau"
                      name="matKhau"
                      type="password"
                      minLength={6}
                      required={taoTaiKhoan}
                    />
                  </div>
                  <div className="sm:col-span-2">
                    <Label htmlFor="quyenIds">{t('taiKhoan.quyen')}</Label>
                    <SelectTimKiemNhieu
                      id="quyenIds"
                      luaChon={(quyens ?? []).map((q) => ({ giaTri: q.id, nhan: q.tenQuyen }))}
                      giaTri={quyenChon}
                      onDoi={setQuyenChon}
                    />
                  </div>
                  <label className="flex items-center gap-2 text-sm sm:col-span-2">
                    <input
                      type="checkbox"
                      checked={buocDoiMk}
                      onChange={(e) => setBuocDoiMk(e.target.checked)}
                      className="h-4 w-4 rounded border-input"
                    />
                    {t('taiKhoan.buocDoiMatKhau')}
                  </label>
                </div>
              ) : (
                <p className="text-sm text-muted-foreground">
                  {t('nguoiDung.khongCanDangNhap')}
                </p>
              )}
            </fieldset>
          )}

          {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

          <ModalChan>
            <Button type="button" variant="outline" onClick={dong}>
              {t('chung.huy')}
            </Button>
            <Button type="submit" disabled={luu.isPending}>
              {t('chung.luu')}
            </Button>
          </ModalChan>
        </form>
      </Modal>
      {hop}
    </div>
  )
}
