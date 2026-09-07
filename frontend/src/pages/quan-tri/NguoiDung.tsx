import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Plus, Trash2, Pencil } from 'lucide-react'
import { api, layMaLoi, trangRong, type KetQuaTrang } from '@/lib/api'
import { useQuyen } from '@/lib/quyen'
import { useXacNhan } from '@/lib/xacNhan'
import {
  Badge, Button, CanhBaoLoi, Input, Label, Table, Td, Th, TrangTrong,
} from '@/components/ui'
import { Modal, ModalChan } from '@/components/ui/Modal'
import { PhanTrang } from '@/components/ui/PhanTrang'
import { MenuThaoTac } from '@/components/ui/MenuThaoTac'
import { SelectTimKiem, SelectTimKiemNhieu } from '@/components/ui/SelectTimKiem'

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
interface HoSoNhanVien {
  chucVu: string | null
  phongBan: string | null
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
  hoSoNhanVien: HoSoNhanVien | null
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
        hoSoNhanVien:
          loai === 'NhanVien'
            ? { chucVu: s('chucVu'), phongBan: s('phongBan') }
            : null,
      }

      if (dangSua) {
        than.trangThaiNhanSu = nhanSu
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
                <tr key={u.id}>
                  <Td className="font-medium">{u.hoTen}</Td>
                  <Td>{t(`loaiNguoiDung.${u.loaiNguoiDung}`)}</Td>
                  <Td className="text-muted-foreground">
                    {laGiaoVien(u.loaiNguoiDung)
                      ? u.hoSoGiaoVien?.chuyenMon ?? '—'
                      : u.loaiNguoiDung === 'HocVien'
                        ? u.hoSoHocVien?.tenPhuHuynh ?? '—'
                        : u.hoSoNhanVien?.chucVu ?? '—'}
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
                    <div className="flex justify-end">
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

            {loai === 'NhanVien' && (
              <div className="grid gap-4 sm:grid-cols-2">
                <div>
                  <Label htmlFor="chucVu">{t('nguoiDung.chucVu')}</Label>
                  <Input
                    id="chucVu"
                    name="chucVu"
                    defaultValue={dangSua?.hoSoNhanVien?.chucVu ?? ''}
                  />
                </div>
                <div>
                  <Label htmlFor="phongBan">{t('nguoiDung.phongBan')}</Label>
                  <Input
                    id="phongBan"
                    name="phongBan"
                    defaultValue={dangSua?.hoSoNhanVien?.phongBan ?? ''}
                  />
                </div>
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
