import { useState } from 'react'
import { DO_DAI_MAT_KHAU_TOI_THIEU } from '@/lib/chinhSachMatKhau'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Plus, KeyRound, Trash2, Pencil } from 'lucide-react'
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
import type { NguoiDungDto } from './NguoiDung'

interface TaiKhoanDto {
  id: string
  username: string
  nguoiDungId: string | null
  hoTenNguoiDung: string | null
  phaiDoiMatKhau: boolean
  trangThai: 'HoatDong' | 'VoHieuHoa'
  quyenIds: string[]
  tenQuyens: string[]
}

interface QuyenNgan {
  id: string
  tenQuyen: string
}

/**
 * FR-04 — tài khoản đăng nhập.
 *
 * Chỉ thông tin để vào hệ thống. Họ tên, ngày sinh, hồ sơ vai trò nằm ở tab Người dùng —
 * tách từ 07/09/2026 để vô hiệu hoá tài khoản không đụng tới dữ liệu người dùng.
 */
/**
 * Giá trị của mục "không nối đuôi" trong ô chọn.
 *
 * Chuỗi chứ không `''`: `SelectTimKiem` coi chuỗi rỗng là "chưa chọn gì" và sẽ hiện
 * placeholder, nên người dùng không thấy mình đã chủ động chọn không nối.
 */
const KHONG_NOI_DUOI = 'khong-noi'

export default function TaiKhoan() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const { coQuyen } = useQuyen()
  const { hoi, hop } = useXacNhan()

  const [trang, setTrang] = useState(1)
  const [soDong, setSoDong] = useState(20)
  const [timKiem, setTimKiem] = useState('')
  // Bộ lọc danh sách (08/10/2026). Chuỗi rỗng = không lọc, để một ô Select rỗng không phải
  // mang nghĩa "lọc những cái rỗng".
  const [locTrangThai, setLocTrangThai] = useState('')
  const [locQuyen, setLocQuyen] = useState('')
  const [locCoNguoi, setLocCoNguoi] = useState('')
  const [locDoiMk, setLocDoiMk] = useState('')

  const [moForm, setMoForm] = useState(false)
  const [dangSua, setDangSua] = useState<TaiKhoanDto | null>(null)
  /**
   * Đuôi tên đăng nhập người tạo chọn: 1, 2, 3 — hoặc `null` = không nối.
   *
   * Mặc định đuôi 1 vì trung tâm nào khai đuôi cũng khai ô đầu trước, và nối đuôi là ý định
   * thường gặp; tài khoản không đuôi (kỹ thuật) là ngoại lệ. Trung tâm chưa khai đuôi nào
   * thì ô chọn không hiện và giá trị này không đi tới đâu — `GhepAsync` vẫn trả tên ngắn vì
   * ô tương ứng rỗng.
   */
  const [duoiSo, setDuoiSo] = useState<number | null>(1)
  const [nguoiChon, setNguoiChon] = useState<string | null>(null)
  const [quyenChon, setQuyenChon] = useState<string[]>([])
  const [trangThai, setTrangThai] = useState<'HoatDong' | 'VoHieuHoa'>('HoatDong')
  // Mặc định BẬT: tài khoản do người khác tạo hộ thì mật khẩu ban đầu người tạo cũng biết.
  const [buocDoiMk, setBuocDoiMk] = useState(true)
  const [guiEmail, setGuiEmail] = useState(false)
  const [datLaiCho, setDatLaiCho] = useState<TaiKhoanDto | null>(null)
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [maLoiBang, setMaLoiBang] = useState<string | null>(null)

  const { data: kq = trangRong<TaiKhoanDto>(), isLoading } = useQuery({
    queryKey: ['tai-khoan', timKiem, locTrangThai, locQuyen, locCoNguoi, locDoiMk, trang, soDong],
    queryFn: async () =>
      (await api.get<KetQuaTrang<TaiKhoanDto>>('/tai-khoan', {
        params: {
          timKiem: timKiem || undefined,
          trangThai: locTrangThai || undefined,
          quyenId: locQuyen || undefined,
          coNguoiDung: locCoNguoi || undefined,
          phaiDoiMatKhau: locDoiMk || undefined,
          trang, soDong,
        },
      })).data,
  })

  const { data: quyens } = useQuery({
    queryKey: ['quyen'],
    queryFn: async () => (await api.get<QuyenNgan[]>('/quyen')).data,
  })

  /**
   * Thiết lập trung tâm — chỉ cần ba đuôi để dựng ô chọn.
   *
   * Dùng CHUNG `queryKey` với màn Thiết lập: hai màn đọc một endpoint, khoá khác nhau thì
   * đổi đuôi ở Thiết lập xong sang đây vẫn thấy giá trị cũ cho tới khi tải lại trang.
   */
  const { data: thietLap } = useQuery({
    queryKey: ['thiet-lap'],
    queryFn: async () =>
      (await api.get<{
        duoiTenDangNhap: string | null
        duoiTenDangNhap2: string | null
        duoiTenDangNhap3: string | null
        matKhauMacDinh: string | null
      }>('/thiet-lap')).data,
  })

  /**
   * Các đuôi trung tâm ĐÃ khai, kèm số thứ tự gốc.
   *
   * Giữ số gốc chứ không đánh lại từ 1: backend chọn cột theo số này, nên khai ô 1 và ô 3 mà
   * đánh lại thành 1-2 sẽ nối nhầm đuôi — và nhầm im lặng, không lỗi nào báo.
   */
  const duoiDaKhai = [
    thietLap?.duoiTenDangNhap,
    thietLap?.duoiTenDangNhap2,
    thietLap?.duoiTenDangNhap3,
  ]
    .map((duoi, i) => ({ so: i + 1, duoi }))
    .filter((x): x is { so: number; duoi: string } => !!x.duoi)

  /**
   * Người dùng để gán tài khoản — lấy nhiều để đủ chọn; danh sách này cũng dùng ở màn Lớp học.
   *
   * Vẫn gọi `/nguoi-dung` (gác bằng `TaiKhoan`) chứ không `/nhan-su` hay `/hoc-vien`: gán tài
   * khoản áp dụng cho **cả bốn vai trò**, kể cả học viên. Đây là màn quản trị dùng chung, nên
   * nó cần thấy mọi con người — khác hai màn hồ sơ đã tách theo hệ thống (08/09/2026).
   */
  const { data: nguoiDungs } = useQuery({
    queryKey: ['nguoi-dung-ngan'],
    queryFn: async () =>
      (await api.get<KetQuaTrang<NguoiDungDto>>('/nguoi-dung', { params: { soDong: 200 } }))
        .data.duLieu,
  })

  const lamMoi = () => {
    void qc.invalidateQueries({ queryKey: ['tai-khoan'] })
    // Cột "tài khoản" ở hai màn hồ sơ (Nhân sự bên HRM, Học viên bên LMS) đổi theo.
    void qc.invalidateQueries({ queryKey: ['nguoi-dung'] })
    void qc.invalidateQueries({ queryKey: ['/nhan-su'] })
    void qc.invalidateQueries({ queryKey: ['/hoc-vien'] })
  }

  const dong = () => {
    setMoForm(false)
    setDangSua(null)
    setNguoiChon(null)
    setQuyenChon([])
    setMaLoi(null)
  }

  const luu = useMutation({
    mutationFn: async (fd: FormData) => {
      if (dangSua) {
        const tenMoi = String(fd.get('username') ?? '').trim()
        await api.put(`/tai-khoan/${dangSua.id}`, {
          id: dangSua.id,
          nguoiDungId: nguoiChon,
          quyenIds: quyenChon,
          trangThai,
          // Chỉ gửi khi THẬT SỰ đổi: backend hiểu `null` = giữ nguyên, nên gửi lại tên cũ
          // mỗi lần lưu sẽ đá phiên của người đó dù không ai đổi gì.
          username: tenMoi && tenMoi !== dangSua.username ? tenMoi : null,
        })
      } else {
        await api.post('/tai-khoan', {
          username: String(fd.get('username')).trim(),
          matKhau: String(fd.get('matKhau')),
          nguoiDungId: nguoiChon,
          quyenIds: quyenChon,
          phaiDoiMatKhau: buocDoiMk,
          duoiSo,
          // Chỉ gửi khi người được chọn CÓ email thật — xem ghi chú ở màn Người dùng.
          guiEmailThongBao: guiEmail && !!emailNguoiChon,
        })
      }
    },
    onSuccess: () => {
      lamMoi()
      dong()
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const datLaiMk = useMutation({
    mutationFn: async ({ id, mk }: { id: string; mk: string }) =>
      api.post(`/tai-khoan/${id}/dat-lai-mat-khau`, { matKhauMoi: mk }),
    onSuccess: () => {
      lamMoi()
      setDatLaiCho(null)
      setMaLoi(null)
    },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const xoa = useMutation({
    mutationFn: async (id: string) => api.delete(`/tai-khoan/${id}`),
    onSuccess: lamMoi,
    onError: (e) => setMaLoiBang(layMaLoi(e)),
  })

  // Email của người đang chọn — quyết định có gửi thư được không. Lấy từ danh sách đã tải
  // sẵn, không gọi thêm API.
  const emailNguoiChon =
    (nguoiDungs ?? []).find((n) => n.id === nguoiChon)?.email?.trim() || null

  const luaChonNguoi = (nguoiDungs ?? []).map((n) => ({
    giaTri: n.id,
    nhan: n.hoTen,
    phu: t(`loaiNguoiDung.${n.loaiNguoiDung}`),
  }))

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="w-56">
          <Label htmlFor="tim-tk">{t('chung.timKiem')}</Label>
          <Input
            id="tim-tk"
            value={timKiem}
            onChange={(e) => {
              setTimKiem(e.target.value)
              setTrang(1)
            }}
            placeholder={t('taiKhoan.username')}
          />
        </div>

        {/* Bốn bộ lọc. Mỗi ô có mục "tất cả" ở đầu vì bỏ trống một Select trông giống lỗi
            tải dữ liệu hơn là "không lọc". */}
        <div className="w-44">
          <Label htmlFor="loc-tt">{t('taiKhoan.trangThai')}</Label>
          <SelectTimKiem
            id="loc-tt"
            luaChon={[
              { giaTri: '', nhan: t('taiKhoan.locTatCa') },
              { giaTri: 'HoatDong', nhan: t('taiKhoan.HoatDong') },
              { giaTri: 'VoHieuHoa', nhan: t('taiKhoan.VoHieuHoa') },
            ]}
            giaTri={locTrangThai}
            onDoi={(v) => { setLocTrangThai(v ?? ''); setTrang(1) }}
            choPhepXoa={false}
          />
        </div>

        <div className="w-48">
          <Label htmlFor="loc-quyen">{t('taiKhoan.nhomQuyen')}</Label>
          <SelectTimKiem
            id="loc-quyen"
            luaChon={[
              { giaTri: '', nhan: t('taiKhoan.locTatCa') },
              ...(quyens ?? []).map((q) => ({ giaTri: q.id, nhan: q.tenQuyen })),
            ]}
            giaTri={locQuyen}
            onDoi={(v) => { setLocQuyen(v ?? ''); setTrang(1) }}
            choPhepXoa={false}
          />
        </div>

        <div className="w-44">
          <Label htmlFor="loc-nguoi">{t('nguoiDung.nguoiSoHuu')}</Label>
          <SelectTimKiem
            id="loc-nguoi"
            luaChon={[
              { giaTri: '', nhan: t('taiKhoan.locTatCa') },
              { giaTri: 'true', nhan: t('taiKhoan.locCoGanNguoi') },
              { giaTri: 'false', nhan: t('taiKhoan.locKhongGanAi') },
            ]}
            giaTri={locCoNguoi}
            onDoi={(v) => { setLocCoNguoi(v ?? ''); setTrang(1) }}
            choPhepXoa={false}
          />
        </div>

        <div className="w-48">
          <Label htmlFor="loc-mk">{t('taiKhoan.locDoiMatKhau')}</Label>
          <SelectTimKiem
            id="loc-mk"
            luaChon={[
              { giaTri: '', nhan: t('taiKhoan.locTatCa') },
              { giaTri: 'true', nhan: t('taiKhoan.locChuaDangNhap') },
              { giaTri: 'false', nhan: t('taiKhoan.locDaDangNhap') },
            ]}
            giaTri={locDoiMk}
            onDoi={(v) => { setLocDoiMk(v ?? ''); setTrang(1) }}
            choPhepXoa={false}
          />
        </div>

        {coQuyen('TaiKhoan', 'Them') && (
          <Button
            onClick={() => {
              setDangSua(null)
              setNguoiChon(null)
              setQuyenChon([])
              setTrangThai('HoatDong')
              setBuocDoiMk(true)
              setDuoiSo(1)
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
                <Th>{t('taiKhoan.username')}</Th>
                <Th>{t('nguoiDung.nguoiSoHuu')}</Th>
                <Th>{t('taiKhoan.quyen')}</Th>
                <Th>{t('taiKhoan.trangThai')}</Th>
                <Th />
              </tr>
            </thead>
            <tbody>
              {kq.duLieu.map((u) => (
                <tr key={u.id}>
                  <Td className="font-medium">{u.username}</Td>
                  <Td>
                    {u.hoTenNguoiDung ?? (
                      <span className="text-muted-foreground">{t('nguoiDung.khongGanAi')}</span>
                    )}
                  </Td>
                  <Td>
                    <div className="flex flex-wrap gap-1">
                      {u.tenQuyens.length === 0 ? (
                        <span className="text-muted-foreground">—</span>
                      ) : (
                        u.tenQuyens.map((q) => (
                          <Badge key={q} variant="muted">
                            {q}
                          </Badge>
                        ))
                      )}
                    </div>
                  </Td>
                  <Td>
                    <div className="flex flex-wrap gap-1">
                      <Badge variant={u.trangThai === 'HoatDong' ? 'ok' : 'loi'}>
                        {t(`taiKhoan.${u.trangThai}`)}
                      </Badge>
                      {u.phaiDoiMatKhau && (
                        <Badge variant="cho">{t('taiKhoan.phaiDoiMatKhau')}</Badge>
                      )}
                    </div>
                  </Td>
                  <Td>
                    <div className="flex justify-end">
                      <MenuThaoTac
                        nhanMo={t('chung.thaoTac')}
                        muc={[
                          {
                            nhan: t('chung.sua'),
                            icon: Pencil,
                            an: !coQuyen('TaiKhoan', 'Sua'),
                            onChon: () => {
                              setDangSua(u)
                              setNguoiChon(u.nguoiDungId)
                              setQuyenChon(u.quyenIds)
                              setTrangThai(u.trangThai)
                              setMaLoi(null)
                              setMoForm(true)
                            },
                          },
                          {
                            nhan: t('taiKhoan.datLaiMatKhau'),
                            icon: KeyRound,
                            an: !coQuyen('DoiMatKhauNguoiKhac', 'Sua'),
                            onChon: () => {
                              setDatLaiCho(u)
                              setMaLoi(null)
                            },
                          },
                          {
                            nhan: t('chung.xoa'),
                            icon: Trash2,
                            nguyHiem: true,
                            ngatNhom: true,
                            an: !coQuyen('TaiKhoan', 'Xoa'),
                            onChon: () =>
                              hoi({
                                tieuDe: t('chung.xacNhanXoa'),
                                thongDiep: t('taiKhoan.hoiXoa', { ten: u.username }),
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
        tieuDe={dangSua ? t('taiKhoan.suaTaiKhoan') : t('taiKhoan.themTaiKhoan')}
        rong="md"
      >
        <form
          key={dangSua?.id ?? 'moi'}
          onSubmit={(e) => {
            e.preventDefault()
            const fd = new FormData(e.currentTarget)
            // Tên LẤY TỪ FORM, kể cả khi sửa: hộp xác nhận phải nói đúng thứ sắp lưu, không
            // phải tên cũ — người dùng vừa đổi tên mà hộp hỏi "lưu <tên cũ>?" là nói sai.
            const ten = String(fd.get('username') ?? '').trim() || dangSua?.username || ''
            // Đổi tên đăng nhập đá phiên đang mở, nên hỏi rõ hệ quả thay vì câu "lưu?" chung.
            const doiTen = !!dangSua && ten !== dangSua.username
            hoi({
              tieuDe: dangSua ? t('chung.xacNhanLuu') : t('chung.xacNhanThem'),
              thongDiep: doiTen
                ? t('taiKhoan.hoiDoiUsername', { cu: dangSua!.username, moi: ten })
                : dangSua
                  ? t('chung.hoiLuu', { ten })
                  : t('chung.hoiThem', { ten }),
              onDongY: () => luu.mutate(fd),
            })
          }}
          className="space-y-4"
        >
          {dangSua ? (
            <div>
              <Label htmlFor="username">{t('taiKhoan.username')}</Label>
              <Input
                id="username"
                name="username"
                defaultValue={dangSua.username}
                maxLength={100}
                required
              />
              <p className="mt-1 text-xs text-status-cho">
                {t('taiKhoan.doiUsernameLuuY')}
              </p>
            </div>
          ) : (
            <div className="grid gap-4 sm:grid-cols-2">
              <div>
                <Label htmlFor="username">{t('taiKhoan.username')} *</Label>
                <Input id="username" name="username" required />
                {/* Chỉ hiện khi trung tâm đã khai ít nhất một đuôi ở Thiết lập chung —
                    không khai thì ô này vô nghĩa và chỉ làm form rối. */}
                {duoiDaKhai.length > 0 && (
                  <div className="mt-1.5">
                    <Label htmlFor="duoiSo">{t('taiKhoan.duoiTenDangNhap')}</Label>
                    <SelectTimKiem
                      id="duoiSo"
                      luaChon={[
                        ...duoiDaKhai.map((x) => ({ giaTri: String(x.so), nhan: x.duoi })),
                        // Mục "không nối" nằm trong CÙNG ô chọn chứ không là ô tick riêng:
                        // chỉ có một quyết định ở đây, nên chỉ nên có một chỗ để quyết.
                        { giaTri: KHONG_NOI_DUOI, nhan: t('taiKhoan.khongNoiDuoi') },
                      ]}
                      giaTri={duoiSo === null ? KHONG_NOI_DUOI : String(duoiSo)}
                      onDoi={(v) =>
                        setDuoiSo(v === KHONG_NOI_DUOI || !v ? null : Number(v))
                      }
                      choPhepXoa={false}
                    />
                    <p className="mt-1 text-xs text-muted-foreground">
                      {t('taiKhoan.noiDuoiGoiY')}
                    </p>
                  </div>
                )}
              </div>
              <div>
                <Label htmlFor="matKhau">{t('taiKhoan.matKhau')} *</Label>
                {/*
                  Điền sẵn mật khẩu mặc định của trung tâm (09/10/2026), vẫn sửa được.

                  `key` buộc React dựng lại ô khi mật khẩu mặc định tải xong: `defaultValue`
                  chỉ đọc ở lần dựng đầu, mà query `/thiet-lap` thường về SAU khi form đã mở
                  — không có `key` thì ô mãi trống dù đã khai mặc định.

                  `type="text"` khi có mặc định: người tạo cần ĐỌC được mật khẩu để đọc cho
                  người dùng mới. Che đi thì họ phải sang màn Thiết lập xem lại.
                */}
                <Input
                  key={thietLap?.matKhauMacDinh ?? 'trong'}
                  id="matKhau"
                  name="matKhau"
                  type={thietLap?.matKhauMacDinh ? 'text' : 'password'}
                  defaultValue={thietLap?.matKhauMacDinh ?? ''}
                  minLength={DO_DAI_MAT_KHAU_TOI_THIEU}
                  autoComplete="off"
                  required
                />
                {thietLap?.matKhauMacDinh && (
                  <p className="mt-1 text-xs text-muted-foreground">
                    {t('taiKhoan.dungMatKhauMacDinh')}
                  </p>
                )}
              </div>
            </div>
          )}

          <div>
            <Label htmlFor="nguoiDungId">{t('nguoiDung.nguoiSoHuu')}</Label>
            <SelectTimKiem
              id="nguoiDungId"
              luaChon={luaChonNguoi}
              giaTri={nguoiChon}
              onDoi={setNguoiChon}
              placeholder={t('nguoiDung.khongGanAi')}
            />
            <p className="mt-1 text-xs text-muted-foreground">
              {t('nguoiDung.giaiThichGanNguoi')}
            </p>
          </div>

          <div>
            <Label htmlFor="quyenIds">{t('taiKhoan.quyen')}</Label>
            <SelectTimKiemNhieu
              id="quyenIds"
              luaChon={(quyens ?? []).map((q) => ({ giaTri: q.id, nhan: q.tenQuyen }))}
              giaTri={quyenChon}
              onDoi={setQuyenChon}
            />
          </div>

          {dangSua ? (
            <div>
              <Label htmlFor="trangThai">{t('taiKhoan.trangThai')}</Label>
              <SelectTimKiem
                id="trangThai"
                luaChon={[
                  { giaTri: 'HoatDong', nhan: t('taiKhoan.HoatDong') },
                  { giaTri: 'VoHieuHoa', nhan: t('taiKhoan.VoHieuHoa') },
                ]}
                giaTri={trangThai}
                onDoi={(v) => setTrangThai((v as 'HoatDong' | 'VoHieuHoa') ?? 'HoatDong')}
                choPhepXoa={false}
              />
              <p className="mt-1 text-xs text-muted-foreground">
                {t('nguoiDung.giaiThichVoHieuHoa')}
              </p>
            </div>
          ) : (
            <div className="space-y-3">
              <label className="flex items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={buocDoiMk}
                  onChange={(e) => setBuocDoiMk(e.target.checked)}
                  className="h-4 w-4 rounded border-input"
                />
                {t('taiKhoan.buocDoiMatKhau')}
              </label>

              <div>
                <label className="flex items-center gap-2 text-sm">
                  <input
                    type="checkbox"
                    checked={guiEmail && !!emailNguoiChon}
                    disabled={!emailNguoiChon}
                    onChange={(e) => setGuiEmail(e.target.checked)}
                    className="h-4 w-4 rounded border-input disabled:opacity-50"
                  />
                  <span className={emailNguoiChon ? undefined : 'text-muted-foreground'}>
                    {t('taiKhoan.guiEmailThongBao')}
                  </span>
                </label>
                {/* Hiện ĐỊA CHỈ sẽ nhận thư, không chỉ nói "sẽ gửi": mật khẩu tạm gửi nhầm
                    chỗ là không thu hồi được, nên người bấm phải thấy nó đi đâu. */}
                <p className="mt-1 text-xs text-muted-foreground">
                  {emailNguoiChon
                    ? `${t('taiKhoan.guiEmailThongBaoMoTa')} → ${emailNguoiChon}`
                    : t('taiKhoan.guiEmailCanHoSoCoEmail')}
                </p>
              </div>
            </div>
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

      <Modal
        mo={!!datLaiCho}
        onDong={() => setDatLaiCho(null)}
        tieuDe={t('taiKhoan.datLaiMatKhau')}
        rong="sm"
      >
        <form
          onSubmit={(e) => {
            e.preventDefault()
            const fd = new FormData(e.currentTarget)
            const mk = String(fd.get('mkMoi'))
            hoi({
              tieuDe: t('taiKhoan.datLaiMatKhau'),
              thongDiep: t('taiKhoan.hoiDatLaiMatKhau', { ten: datLaiCho!.username }),
              nhanDongY: t('taiKhoan.datLaiMatKhau'),
              nguyHiem: true,
              onDongY: () => datLaiMk.mutate({ id: datLaiCho!.id, mk }),
            })
          }}
          className="space-y-4"
        >
          <p className="text-sm text-muted-foreground">{datLaiCho?.username}</p>
          <div>
            <Label htmlFor="mkMoi">{t('taiKhoan.matKhauMoi')} *</Label>
            <Input id="mkMoi" name="mkMoi" type="password" minLength={DO_DAI_MAT_KHAU_TOI_THIEU} required />
          </div>

          {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

          <ModalChan>
            <Button type="button" variant="outline" onClick={() => setDatLaiCho(null)}>
              {t('chung.huy')}
            </Button>
            <Button type="submit" disabled={datLaiMk.isPending}>
              {t('chung.luu')}
            </Button>
          </ModalChan>
        </form>
      </Modal>
      {hop}
    </div>
  )
}
