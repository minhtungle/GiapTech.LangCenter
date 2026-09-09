import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ArrowLeft, Pencil } from 'lucide-react'
import { api } from '@/lib/api'
import { Badge, Button, Card, CardContent, TrangTrong } from '@/components/ui'
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

  const { data: u, isLoading, isError } = useQuery({
    queryKey: ['nhan-su', id],
    queryFn: async () => (await api.get<NguoiDungDto>(`/nhan-su/${id}`)).data,
    enabled: !!id,
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
          <div className="sm:col-span-2">
            <Dong nhan={t('nguoiDung.diaChi')} giaTri={u.diaChi ?? '—'} />
          </div>
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
