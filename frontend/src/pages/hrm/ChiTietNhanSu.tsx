import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ArrowLeft, FileText, Pencil, Trash2, Upload } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import { Badge, Button, CanhBaoLoi, Card, CardContent, TrangTrong } from '@/components/ui'
import { useXacNhan } from '@/lib/xacNhan'
import { useQuyen } from '@/lib/quyen'
import type { NguoiDungDto } from '@/pages/quan-tri/NguoiDung'

const ngayVN = (iso: string | null) => (iso ? new Date(iso).toLocaleDateString('vi-VN') : '—')

/**
 * FR-03/FR-23 — view chi tiết hồ sơ nhân sự.
 *
 * Bấm một dòng ở màn Hồ sơ nhân sự thì mở view này (yêu cầu 09/09/2026). Chỉ ĐỌC, sửa qua modal
 * ở màn danh sách — cùng quy ước với view chi tiết khách hàng và lớp học
 * (`docs/frontend/ui-ux-nguyen-tac.md`): đọc thông tin là việc thường xuyên hơn sửa.
 *
 * Gọi `/nhan-su/{id}` chứ không tra trong danh sách đã tải: mở link trực tiếp (bookmark, link
 * đồng nghiệp gửi) thì không có danh sách nào để tra.
 */
export default function ChiTietNhanSu() {
  const { t } = useTranslation()
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const { coQuyen } = useQuyen()
  const qc = useQueryClient()
  const { hoi, hop } = useXacNhan()
  const [maLoi, setMaLoi] = useState<string | null>(null)

  const { data: u, isLoading, isError } = useQuery({
    queryKey: ['nhan-su', id],
    queryFn: async () => (await api.get<NguoiDungDto>(`/nhan-su/${id}`)).data,
    enabled: !!id,
  })

  const lamMoi = () => void qc.invalidateQueries({ queryKey: ['nhan-su'] })

  const taiTep = useMutation({
    mutationFn: async (tep: File) => {
      const fd = new FormData()
      fd.append('tep', tep)
      await api.post(`/nhan-su/${id}/tep`, fd)
    },
    onSuccess: () => { lamMoi(); setMaLoi(null) },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

  const xoaTep = useMutation({
    mutationFn: (tepId: string) => api.delete(`/nhan-su/tep/${tepId}`),
    onSuccess: () => { lamMoi(); setMaLoi(null) },
    onError: (e) => setMaLoi(layMaLoi(e)),
  })

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

      {/*
        TỆP HỒ SƠ (FR-23) — hợp đồng, bằng cấp scan, CCCD scan.

        Tải lên ở ĐÂY chứ không trong modal sửa: tệp là thao tác từng cái một, không thuộc luồng
        "sửa rồi lưu" của form. Ghép vào form sẽ phải giữ tệp trong bộ nhớ tới lúc bấm Lưu.
      */}
      <Card>
        <CardContent className="grid gap-3 pt-6">
          <div className="flex flex-wrap items-center gap-2">
            <h3 className="font-semibold">{t('nguoiDung.tepHoSo')}</h3>
            <Badge variant="muted">{u.tepHoSos.length}</Badge>
            {coQuyen('NhanSu', 'Sua') && (
              <label className="ml-auto">
                <input
                  type="file"
                  className="hidden"
                  onChange={(e) => {
                    const f = e.target.files?.[0]
                    if (f) taiTep.mutate(f)
                    // Reset để chọn lại CÙNG một tệp vẫn kích hoạt onChange.
                    e.target.value = ''
                  }}
                />
                <span className="inline-flex h-9 cursor-pointer items-center gap-2 rounded-md border border-input bg-background px-4 text-sm font-medium hover:bg-muted">
                  <Upload className="h-4 w-4" />
                  {t('nguoiDung.taiTep')}
                </span>
              </label>
            )}
          </div>

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
                  {coQuyen('NhanSu', 'Sua') && (
                    <Button
                      size="sm"
                      variant="ghost"
                      className="ml-auto"
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
