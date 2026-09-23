import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { ArrowLeft, Download, Eye, FileText, Pencil, Trash2, Upload } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import {
  Badge, Button, CanhBaoLoi, Card, CardContent, Input, Label, TrangTrong,
} from '@/components/ui'
import { Modal, ModalChan } from '@/components/ui/Modal'
import { useXacNhan } from '@/lib/xacNhan'
import { useQuyen } from '@/lib/quyen'
import type { NguoiDungDto } from '@/pages/quan-tri/NguoiDung'
import { locale } from '@/lib/ngon-ngu/dinhDang'

/**
 * Mã tab (nằm trong URL) đi kèm khoá i18n — **không ghép chuỗi động**.
 *
 * Mã tab dùng gạch ngang cho URL đẹp, khoá i18n dùng camelCase; ghép động sinh ra
 * `tab_thong-tin` không khớp khoá nào và i18next trả về chính chuỗi khoá cho người dùng thấy
 * (lỗi đã xảy ra thật 07/09/2026 ở view chi tiết lớp học).
 */
const CAC_TAB = [
  { ma: 'thong-tin', khoa: 'chiTietNhanSu.tabThongTin' },
  { ma: 'tep', khoa: 'chiTietNhanSu.tabTep' },
] as const

type Tab = (typeof CAC_TAB)[number]['ma']

const ngayVN = (iso: string | null) => (iso ? new Date(iso).toLocaleDateString(locale()) : '—')

/**
 * Định dạng cho phép — PHẢI khớp `LoaiTepHoSo.ChoPhep` ở backend (10/09/2026).
 *
 * Đây chỉ là **tiện lợi**, không phải bảo mật: `accept` của `<input type=file>` lọc hộp thoại
 * chọn tệp và người dùng vẫn đổi được sang "All files". Chốt thật nằm ở handler — hai tầng này
 * cố tình trùng nhau, và test backend canh tầng thật.
 *
 * Ghi cả đuôi lẫn MIME trong `accept`: một số hệ điều hành báo MIME rỗng hoặc sai cho tệp
 * Office cũ (`.doc`, `.xls`), chỉ ghi MIME thì hộp thoại làm mờ đúng tệp cần chọn.
 */
const DINH_DANG_CHO_PHEP =
  '.pdf,.doc,.docx,.xls,.xlsx,' +
  'application/pdf,application/msword,' +
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document,' +
  'application/vnd.ms-excel,' +
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'

/** 20 MB — khớp `MinioLuuTruTep.KichThuocToiDa`. */
const KICH_THUOC_TOI_DA = 20 * 1024 * 1024

/**
 * Tối đa 10 tệp mỗi hồ sơ — khớp `TepHoSo.SoTepToiDa` ở backend (16/09/2026).
 *
 * Lặp hằng số ở hai tầng là có chủ ý, cùng lý do như `DINH_DANG_CHO_PHEP`: backend mới là chốt
 * (có test canh), còn ở đây chỉ để **khoá nút trước** thay vì để người dùng chọn xong tệp rồi
 * mới nhận lỗi.
 */
const SO_TEP_TOI_DA = 10

/**
 * Trình duyệt chỉ render được PDF. Word/Excel thì **không** — mở trong iframe chỉ ra khung
 * trắng hoặc bật hộp thoại tải về, nên UI phải biết trước để hiện nút "Tải về" thay vì mở modal
 * xem rồi trống trơn.
 */
const xemDuocTrenTrinhDuyet = (loaiNoiDung: string) =>
  loaiNoiDung.toLowerCase() === 'application/pdf'

/**
 * Bỏ phần mở rộng để gợi ý vào ô đặt tên — backend tự ghép lại đuôi thật của tệp.
 *
 * Chỉ cắt đuôi đã biết: tên như "Hợp đồng 2026.v2" không có đuôi hợp lệ nào, cắt mù quáng từ
 * dấu chấm cuối sẽ ăn mất ".v2" của người dùng.
 */
const boDuoi = (ten: string) => ten.replace(/\.(pdf|docx?|xlsx?)$/i, '')

/** Suy từ DTO chứ không khai lại: thêm/đổi trường ở `NguoiDungDto` là tự động theo. */
type TepHoSo = NguoiDungDto['tepHoSos'][number]

/**
 * FR-03/FR-23 — view chi tiết hồ sơ nhân sự.
 *
 * Bấm một dòng ở màn Hồ sơ nhân sự thì mở view này (yêu cầu 09/09/2026). Chỉ ĐỌC, sửa qua modal
 * ở màn danh sách — cùng quy ước với view chi tiết khách hàng và lớp học
 * (`docs/04-frontend/ui-ux-nguyen-tac.md`): đọc thông tin là việc thường xuyên hơn sửa.
 *
 * Gọi `/nhan-su/{id}` chứ không tra trong danh sách đã tải: mở link trực tiếp (bookmark, link
 * đồng nghiệp gửi) thì không có danh sách nào để tra.
 */
export default function ChiTietNhanSu() {
  const { t } = useTranslation()
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const [sp, setSp] = useSearchParams()
  const { coQuyen } = useQuyen()
  const qc = useQueryClient()
  const { hoi, hop } = useXacNhan()
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [dangXem, setDangXem] = useState<{ tep: TepHoSo; url: string } | null>(null)

  /**
   * Tệp vừa chọn, đang chờ người dùng đặt tên (16/09/2026).
   *
   * Hỏi tên TRƯỚC khi tải lên chứ không tải xong rồi mới hỏi: tải xong mới hỏi thì người dùng
   * bấm Huỷ sẽ để lại một tệp tên máy quét trong hồ sơ — đúng thứ tính năng này muốn tránh.
   */
  const [choDatTen, setChoDatTen] = useState<File | null>(null)
  /** Tệp đang đổi tên (null = không mở hộp thoại). */
  const [dangDoiTen, setDangDoiTen] = useState<TepHoSo | null>(null)
  /** Ô nhập tên, dùng chung cho cả hai hộp thoại trên. */
  const [tenNhap, setTenNhap] = useState('')
  /**
   * Lỗi của HỘP THOẠI đặt tên — tách khỏi `maLoi` của trang.
   *
   * Dùng chung một state thì lỗi hiện ở **cả hai** chỗ, vì `<Modal>` luôn nằm trong DOM nên
   * điều kiện bao ngoài không ngăn được khối lỗi bên trong. Đúng lỗi đã gặp ở `CoCauToChuc.tsx`
   * (16/09/2026) và E2E bắt lại lần nữa ở đây: `strict mode violation: resolved to 2 elements`.
   */
  const [maLoiTen, setMaLoiTen] = useState<string | null>(null)

  const tabQuery = sp.get('tab') as Tab | null
  const tab: Tab = tabQuery && CAC_TAB.some((x) => x.ma === tabQuery) ? tabQuery : 'thong-tin'
  // `replace` để bấm qua lại giữa hai tab không sinh một mục lịch sử mỗi lần — nút Back phải
  // về danh sách nhân sự, không lùi từng tab.
  const doiTab = (x: Tab) => setSp(x === 'thong-tin' ? {} : { tab: x }, { replace: true })

  const { data: u, isLoading, isError } = useQuery({
    queryKey: ['nhan-su', id],
    queryFn: async () => (await api.get<NguoiDungDto>(`/nhan-su/${id}`)).data,
    enabled: !!id,
  })

  const lamMoi = () => void qc.invalidateQueries({ queryKey: ['nhan-su'] })

  const taiTep = useMutation({
    mutationFn: async ({ tep, ten }: { tep: File; ten: string }) => {
      const fd = new FormData()
      fd.append('tep', tep)
      // Chỉ gửi khi có tên: chuỗi rỗng bị model binder đổi thành null, nên gửi cũng vô nghĩa —
      // và không gửi thì backend giữ tên gốc, đúng ý người dùng bỏ trống.
      if (ten.trim()) fd.append('tenHienThi', ten.trim())
      await api.post(`/nhan-su/${id}/tep`, fd)
    },
    // Lỗi báo TRONG hộp thoại: người dùng đang ở đó, và hộp thoại phải mở tiếp để họ sửa tên.
    onSuccess: () => { lamMoi(); dongDatTen() },
    onError: (e) => setMaLoiTen(layMaLoi(e)),
  })

  const doiTenTep = useMutation({
    mutationFn: ({ tepId, ten }: { tepId: string; ten: string }) =>
      api.put(`/nhan-su/tep/${tepId}/ten`, { tenMoi: ten.trim() }),
    onSuccess: () => { lamMoi(); dongDatTen() },
    onError: (e) => setMaLoiTen(layMaLoi(e)),
  })

  const xoaTep = useMutation({
    mutationFn: (tepId: string) => api.delete(`/nhan-su/tep/${tepId}`),
    onSuccess: () => { lamMoi(); setMaLoi(null) },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  /**
   * Chọn tệp: kiểm phía client TRƯỚC khi gửi.
   *
   * Không phải để bảo mật (backend mới là chốt) mà để không bắt người dùng chờ tải xong 20 MB
   * rồi mới bị từ chối — và để thông điệp chỉ đúng vào cái sai.
   */
  const chonTep = (f: File) => {
    if (f.size === 0) return setMaLoi('TEP_RONG')
    if (f.size > KICH_THUOC_TOI_DA) return setMaLoi('TEP_QUA_LON')

    // `f.type` rỗng với vài tệp Office cũ trên Windows → lúc đó tin phần mở rộng và để
    // backend phán quyết, thay vì chặn oan một tệp .doc hợp lệ.
    const duoiHopLe = /\.(pdf|docx?|xlsx?)$/i.test(f.name)
    if (f.type && !DINH_DANG_CHO_PHEP.includes(f.type.toLowerCase()) && !duoiHopLe)
      return setMaLoi('LOAI_TEP_HO_SO_KHONG_HO_TRO')

    // Mở hộp thoại đặt tên; tải lên xảy ra khi người dùng bấm Lưu.
    setMaLoi(null)
    setMaLoiTen(null)
    setTenNhap(boDuoi(f.name))
    setChoDatTen(f)
  }

  const dongDatTen = () =>
    { setChoDatTen(null); setDangDoiTen(null); setTenNhap(''); setMaLoiTen(null) }

  /** Đã chạm hạn mức — khoá ô chọn tệp thay vì để người dùng bấm rồi nhận lỗi. */
  const daDayTep = (u?.tepHoSos.length ?? 0) >= SO_TEP_TOI_DA

  /**
   * Gợi ý sẵn tên hiện tại **đã bỏ đuôi** vào ô nhập.
   *
   * Người dùng thường sửa nhẹ tên có sẵn hơn là gõ lại từ đầu. Bỏ đuôi vì backend tự ghép đuôi
   * thật của tệp — để nguyên thì họ thấy "hop-dong.pdf" rồi sửa thành "Hợp đồng.pdf", vẫn ra
   * đúng nhưng ô nhập nói sai về thứ cần gõ.
   */
  const luuTen = () => {
    if (!tenNhap.trim()) return setMaLoiTen('TEN_TEP_KHONG_HOP_LE')
    if (dangDoiTen) doiTenTep.mutate({ tepId: dangDoiTen.id, ten: tenNhap })
    else if (choDatTen) taiTep.mutate({ tep: choDatTen, ten: tenNhap })
  }

  /**
   * Xem/tải tệp qua **blob**, không mở thẳng URL: endpoint cần header `Authorization`, mà
   * `<a href>` và `<iframe src>` không gửi được nó (cùng lý do như `ChonTep.tsx` và `Anh.tsx`).
   */
  const moTep = async (tep: TepHoSo, taiVe: boolean) => {
    try {
      const res = await api.get(`/nhan-su/tep/${tep.id}`, {
        params: taiVe ? { taiVe: true } : undefined,
        responseType: 'blob',
      })
      const url = URL.createObjectURL(res.data as Blob)

      if (taiVe) {
        const a = document.createElement('a')
        a.href = url
        a.download = tep.tenGoc
        a.click()
        URL.revokeObjectURL(url)
        return
      }

      // Xem online: giữ URL cho iframe dùng, thu hồi khi đóng modal (xem `dongXem`).
      //
      // Thanh công cụ của bộ đọc PDF hiện GUID của blob, không phải tên tệp — thử thêm
      // `#tên-tệp` vào URL (10/09) KHÔNG có tác dụng, Chrome lấy tiêu đề từ chính blob chứ
      // không từ fragment. Vì vậy tên gốc hiện ở TIÊU ĐỀ MODAL ngay phía trên khung xem.
      setDangXem({ tep, url })
      setMaLoi(null)
    } catch (e) {
      setMaLoi(layMaLoi(e))
    }
  }

  // `revokeObjectURL` lúc đóng chứ không ngay sau khi gán: thu hồi sớm thì iframe mất nguồn
  // và hiện khung trắng. Không thu hồi thì blob nằm trong bộ nhớ tới lúc rời trang.
  const dongXem = () => {
    if (dangXem) URL.revokeObjectURL(dangXem.url)
    setDangXem(null)
  }

  if (isLoading) return <TrangTrong thongDiep={t('chung.dangTai')} />
  if (isError || !u) return <TrangTrong thongDiep={t('loi.KHONG_TIM_THAY')} />

  const laGiaoVien = u.loaiNguoiDung === 'GiaoVien' || u.loaiNguoiDung === 'TroGiang'

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-3">
        <Link
          to="/hrm/nhan-su"
          className="inline-flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="h-4 w-4" />
          {t('menu.nhanSuNguoiDung')}
        </Link>
        <h2 className="text-lg font-semibold">{u.hoTen}</h2>
        <Badge variant="muted">{t(`loaiNguoiDung.${u.loaiNguoiDung}`)}</Badge>
        <Badge variant={u.trangThaiNhanSu === 'DangLamViec' ? 'ok' : 'muted'}>
          {t(`nguoiDung.${u.trangThaiNhanSu}`)}
        </Badge>

        {/* Sửa ở màn danh sách: form sửa dùng chung cho cả tạo mới nên không nhân bản ở đây. */}
        {coQuyen('NhanSu', 'Sua') && (
          <Button
            variant="outline"
            className="ml-auto"
            onClick={() => navigate('/hrm/nhan-su')}
          >
            <Pencil className="h-4 w-4" />
            {t('chung.sua')}
          </Button>
        )}
      </div>

      {/*
        Hai tab thay vì ba thẻ xếp dọc (10/09/2026). Thẻ tệp đẩy phần thông tin lên cao và với
        người có nhiều tệp thì phải cuộn mới thấy hết — mà thông tin chung là thứ người ta vào
        đây để xem trước.

        Số tệp hiện ngay trên nhãn tab để biết có gì bên đó mà không cần bấm.
      */}
      <div className="flex flex-wrap gap-1 rounded-lg border border-border p-1">
        {CAC_TAB.map((x) => (
          <button
            key={x.ma}
            type="button"
            onClick={() => doiTab(x.ma)}
            className={
              'inline-flex items-center gap-2 rounded-md px-3 py-1.5 text-sm font-medium transition-colors ' +
              (tab === x.ma
                ? 'bg-primary text-primary-foreground'
                : 'text-muted-foreground hover:bg-muted')
            }
          >
            {t(x.khoa)}
            {x.ma === 'tep' && u.tepHoSos.length > 0 && (
              <Badge variant="muted">{u.tepHoSos.length}</Badge>
            )}
          </button>
        ))}
      </div>

      {tab === 'thong-tin' && (
        <>
          <Card>
            <CardContent className="grid gap-3 pt-6 sm:grid-cols-2">
              <Dong nhan={t('taiKhoan.email')} giaTri={u.email ?? '—'} />
              <Dong nhan={t('taiKhoan.soDienThoai')} giaTri={u.soDienThoai ?? '—'} />
              <Dong nhan={t('nguoiDung.ngaySinh')} giaTri={ngayVN(u.ngaySinh)} />
              <Dong nhan={t('nguoiDung.chucVu')} giaTri={u.tenChucVu ?? '—'} />
              <Dong nhan={t('nguoiDung.phongBan')} giaTri={u.tenPhongBan ?? t('coCau.chuaXep')} />
              <Dong
                nhan={t('nguoiDung.taiKhoan')}
                giaTri={
                  u.username ? (
                    <span className="flex items-center gap-2">
                      {u.username}
                      {u.trangThaiTaiKhoan === 'VoHieuHoa' && (
                        <Badge variant="loi">{t('taiKhoan.VoHieuHoa')}</Badge>
                      )}
                    </span>
                  ) : (
                    t('nguoiDung.chuaCoTaiKhoan')
                  )
                }
              />
              <Dong nhan={t('nguoiDung.cccd')} giaTri={u.cccd ?? '—'} />
              <Dong
                nhan={t('nguoiDung.soTaiKhoan')}
                giaTri={
                  u.soTaiKhoan
                    ? `${u.soTaiKhoan}${u.tenNganHang ? ` · ${u.tenNganHang}` : ''}`
                    : '—'
                }
              />
              <div className="sm:col-span-2">
                <Dong nhan={t('nguoiDung.diaChi')} giaTri={u.diaChi ?? '—'} />
              </div>
              <div className="sm:col-span-2">
                <Dong
                  nhan={t('nguoiDung.lienKetMxh')}
                  giaTri={
                    u.lienKetMxhs.length === 0 ? (
                      '—'
                    ) : (
                      <span className="flex flex-wrap gap-2">
                        {u.lienKetMxhs.map((m) => (
                          <a
                            key={m.id}
                            // Zalo thường là số điện thoại, không phải URL — chỉ mở tab mới khi nó
                            // thật sự là link, còn lại hiện dạng chữ để không tạo link hỏng.
                            href={/^https?:\/\//.test(m.duongDan) ? m.duongDan : undefined}
                            target="_blank"
                            rel="noreferrer noopener"
                            className={
                              /^https?:\/\//.test(m.duongDan)
                                ? 'inline-flex items-center gap-1 text-primary hover:underline'
                                : 'inline-flex items-center gap-1'
                            }
                          >
                            <Badge variant="muted">{t(`loaiMxh.${m.loai}`)}</Badge>
                            {m.duongDan}
                          </a>
                        ))}
                      </span>
                    )
                  }
                />
              </div>
              {u.ghiChu && (
                <div className="sm:col-span-2">
                  <Dong nhan={t('chung.ghiChu')} giaTri={u.ghiChu} />
                </div>
              )}
            </CardContent>
          </Card>

          {/* Hồ sơ theo vai trò — chỉ hiện khối của vai trò hiện tại. Hồ sơ vai trò CŨ vẫn còn trong
              DB (đổi vai trò không xoá) nhưng hiện ra sẽ gây nhầm "người này vừa dạy vừa học". */}
          {laGiaoVien && u.hoSoGiaoVien && (
            <Card>
              <CardContent className="grid gap-3 pt-6 sm:grid-cols-3">
                <Dong nhan={t('nguoiDung.bangCap')} giaTri={u.hoSoGiaoVien.bangCap ?? '—'} />
                <Dong nhan={t('nguoiDung.chuyenMon')} giaTri={u.hoSoGiaoVien.chuyenMon ?? '—'} />
                <Dong
                  nhan={t('nguoiDung.ngayVaoLam')}
                  giaTri={ngayVN(u.hoSoGiaoVien.ngayVaoLam)}
                />
              </CardContent>
            </Card>
          )}
        </>
      )}

      {/*
        TỆP HỒ SƠ (FR-23) — hợp đồng, bằng cấp scan, CCCD scan.

        Tải lên ở ĐÂY chứ không trong modal sửa: tệp là thao tác từng cái một, không thuộc luồng
        "sửa rồi lưu" của form. Ghép vào form sẽ phải giữ tệp trong bộ nhớ tới lúc bấm Lưu.
      */}
      {tab === 'tep' && (
        <Card>
          <CardContent className="grid gap-3 pt-6">
            <div className="flex flex-wrap items-center gap-2">
              {/* Số tệp đã hiện trên nhãn tab — không đếm lại lần thứ hai ngay dưới nó. */}
              <h3 className="font-semibold">{t('nguoiDung.tepHoSo')}</h3>
              {coQuyen('NhanSu', 'Sua') && (
                /*
                  Đủ 10 tệp thì KHOÁ hẳn ô chọn tệp, không để bấm rồi mới báo lỗi. `<label>` bọc
                  input không có thuộc tính `disabled`, nên phải bỏ luôn input và đổi lớp CSS —
                  để nguyên mà chỉ làm mờ thì vẫn bấm được.
                */
                <label className={`ml-auto ${daDayTep ? 'pointer-events-none' : ''}`}>
                  {!daDayTep && (
                    <input
                      type="file"
                      className="hidden"
                      accept={DINH_DANG_CHO_PHEP}
                      onChange={(e) => {
                        const f = e.target.files?.[0]
                        if (f) chonTep(f)
                        // Reset để chọn lại CÙNG một tệp vẫn kích hoạt onChange.
                        e.target.value = ''
                      }}
                    />
                  )}
                  <span
                    className={
                      'inline-flex h-9 items-center gap-2 rounded-md border border-input '
                      + 'bg-background px-4 text-sm font-medium '
                      + (daDayTep
                        ? 'cursor-not-allowed opacity-50'
                        : 'cursor-pointer hover:bg-muted')
                    }
                  >
                    <Upload className="h-4 w-4" />
                    {t('nguoiDung.taiTep')}
                  </span>
                </label>
              )}
            </div>

            {/* Nói trước giới hạn thay vì để người dùng chọn xong mới bị từ chối. */}
            {coQuyen('NhanSu', 'Sua') && (
              <p className="text-xs text-muted-foreground">
                {t('nguoiDung.gioiHanTep')}
                {' · '}
                {t('nguoiDung.soTepDaDung', {
                  so: u.tepHoSos.length, toiDa: SO_TEP_TOI_DA,
                })}
              </p>
            )}

            {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

            {u.tepHoSos.length === 0 ? (
              <p className="text-sm text-muted-foreground">{t('nguoiDung.chuaCoTep')}</p>
            ) : (
              <ul className="grid gap-1.5">
                {u.tepHoSos.map((tep) => (
                  <li
                    key={tep.id}
                    className="flex flex-wrap items-center gap-2 rounded-md border border-border p-2 text-sm"
                  >
                    <FileText className="h-4 w-4 shrink-0 text-muted-foreground" />
                    <span className="min-w-0 truncate font-medium">{tep.tenGoc}</span>
                    <span className="text-xs text-muted-foreground">
                      {(tep.kichThuoc / 1024).toFixed(0)} KB · {ngayVN(tep.ngayTao)}
                    </span>

                    {/*
                      Chỉ hiện "Xem" cho loại trình duyệt render được. Word/Excel mà cũng cho
                      bấm Xem thì modal mở ra trắng trơn — tệ hơn là không có nút.
                    */}
                    {xemDuocTrenTrinhDuyet(tep.loaiNoiDung) && (
                      <Button
                        size="sm"
                        variant="ghost"
                        className="ml-auto"
                        title={t('chung.xem')}
                        onClick={() => void moTep(tep, false)}
                      >
                        <Eye className="h-4 w-4" />
                      </Button>
                    )}

                    <Button
                      size="sm"
                      variant="ghost"
                      className={xemDuocTrenTrinhDuyet(tep.loaiNoiDung) ? '' : 'ml-auto'}
                      title={t('chung.taiVe')}
                      onClick={() => void moTep(tep, true)}
                    >
                      <Download className="h-4 w-4" />
                    </Button>

                    {/* Đổi tên tệp đã có — phần lớn hồ sơ cũ mang tên máy quét. */}
                    {coQuyen('NhanSu', 'Sua') && (
                      <Button
                        size="sm"
                        variant="ghost"
                        title={t('nguoiDung.doiTenTep')}
                        onClick={() => {
                          setMaLoiTen(null)
                          setDangDoiTen(tep)
                          setTenNhap(boDuoi(tep.tenGoc))
                        }}
                      >
                        <Pencil className="h-4 w-4" />
                      </Button>
                    )}

                    {coQuyen('NhanSu', 'Sua') && (
                      <Button
                        size="sm"
                        variant="ghost"
                        onClick={() =>
                          hoi({
                            tieuDe: t('chung.xacNhanXoa'),
                            thongDiep: t('nguoiDung.hoiXoaTep', { ten: tep.tenGoc }),
                            nhanDongY: t('chung.xoa'),
                            nguyHiem: true,
                            onDongY: () => xoaTep.mutate(tep.id),
                          })
                        }
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    )}
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
      )}

      {/*
        Xem tệp online. `rong="xl"` vì PDF khổ A4 trong khung hẹp thì chữ nhỏ tới mức phải zoom.

        Nội dung là `<iframe src={blob:}>` — trình duyệt tự dùng bộ đọc PDF sẵn có, không cần
        kéo thêm thư viện (pdf.js ~300 KB) cho một việc nó vốn làm được.
      */}
      <Modal
        mo={!!dangXem}
        onDong={dongXem}
        tieuDe={dangXem?.tep.tenGoc ?? ''}
        rong="xl"
      >
        {dangXem && (
          <div className="grid gap-3">
            <iframe
              src={dangXem.url}
              title={dangXem.tep.tenGoc}
              className="h-[70vh] w-full rounded-md border border-border bg-muted"
            />
            <div className="flex justify-end">
              <Button variant="outline" onClick={() => void moTep(dangXem.tep, true)}>
                <Download className="h-4 w-4" />
                {t('chung.taiVe')}
              </Button>
            </div>
          </div>
        )}
      </Modal>

      {/*
        Hộp thoại đặt tên — dùng CHUNG cho "đặt tên lúc tải lên" và "đổi tên tệp đã có".

        Một hộp thoại chứ không hai: hai việc chỉ khác nhau ở chỗ có tệp mới hay không, còn ô
        nhập, luật hợp lệ và thông điệp lỗi đều giống hệt. Tách đôi là hai chỗ để quên sửa.
      */}
      <Modal
        mo={!!choDatTen || !!dangDoiTen}
        onDong={dongDatTen}
        tieuDe={t(dangDoiTen ? 'nguoiDung.doiTenTep' : 'nguoiDung.datTenTep')}
        moTa={choDatTen?.name ?? dangDoiTen?.tenGoc}
      >
        <div className="grid gap-3">
          <div className="grid gap-1.5">
            <Label htmlFor="ten-tep">{t('nguoiDung.tenHienThi')}</Label>
            <Input
              id="ten-tep"
              value={tenNhap}
              autoFocus
              maxLength={190}
              placeholder={t('nguoiDung.viDuTenTep')}
              onChange={(e) => setTenNhap(e.target.value)}
              // Enter để lưu: hộp thoại một ô nhập mà bắt rê chuột xuống nút là phiền.
              onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); luuTen() } }}
            />
            {/* Đuôi tệp do hệ thống giữ — nói rõ để không ai gõ ".pdf" vào đây. */}
            <p className="text-xs text-muted-foreground">{t('nguoiDung.giuDuoiTep')}</p>
          </div>

          {maLoiTen && <CanhBaoLoi>{t(`loi.${maLoiTen}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

          <ModalChan>
            <Button variant="outline" onClick={dongDatTen}>{t('chung.huy')}</Button>
            <Button
              onClick={luuTen}
              disabled={!tenNhap.trim() || taiTep.isPending || doiTenTep.isPending}
            >
              {taiTep.isPending || doiTenTep.isPending ? t('chung.dangTai') : t('chung.luu')}
            </Button>
          </ModalChan>
        </div>
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
