import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { ArrowLeft } from 'lucide-react'
import { api } from '@/lib/api'
import { Badge, Card, CardContent, TrangTrong } from '@/components/ui'
import { LichVaDiemDanh } from './LichVaDiemDanh'
import { BaiTapCuaLop } from './BaiTapCuaLop'
import { HocVienCuaLop } from './HocVienCuaLop'
import TaiLieu from './TaiLieu'
import HocPhi from './HocPhi'
import {
  tienVN, ngayVN, mauTrangThai,
  type LopHocDto, type NguoiDungNgan,
} from './lopHocTypes'
import type { KetQuaTrang } from '@/lib/api'

const CAC_TAB = ['tong-quan', 'hoc-vien', 'lich', 'bai-tap', 'hoc-phi', 'tai-lieu'] as const
type Tab = (typeof CAC_TAB)[number]

/**
 * FR-07 — view chi tiết một lớp học.
 *
 * Trước đây Lịch, Bài tập và Học viên mở dạng modal từ bảng danh sách: mỗi lần muốn xem thứ
 * khác của cùng một lớp phải đóng modal, tìm lại dòng, mở modal khác. Nay là một trang có tab.
 *
 * **Tab lưu ở query `?tab=`** chứ không ở state: gửi link cho đồng nghiệp thì họ mở đúng tab,
 * F5 không mất chỗ, và nút Back của trình duyệt hoạt động đúng như người dùng mong đợi.
 */
export default function ChiTietLopHoc() {
  const { t } = useTranslation()
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const [sp, setSp] = useSearchParams()

  const tabQuery = sp.get('tab') as Tab | null
  const tab: Tab = tabQuery && CAC_TAB.includes(tabQuery) ? tabQuery : 'tong-quan'

  const doiTab = (x: Tab) => {
    // `replace` để 6 lần bấm tab không sinh 6 mục lịch sử — Back phải quay về danh sách lớp.
    setSp(x === 'tong-quan' ? {} : { tab: x }, { replace: true })
  }

  const { data: lop, isLoading, isError } = useQuery({
    queryKey: ['lop-hoc', id],
    queryFn: async () => (await api.get<LopHocDto>(`/lop-hoc/${id}`)).data,
    enabled: !!id,
  })

  // Danh sách người dùng để tab Học viên chọn người thêm vào lớp.
  const { data: nguoiDungs } = useQuery({
    queryKey: ['nguoi-dung-ngan'],
    queryFn: async () =>
      (await api.get<KetQuaTrang<NguoiDungNgan>>('/nguoi-dung', {
        params: { soDong: 200, trangThaiNhanSu: 'DangLamViec' },
      })).data.duLieu,
  })

  if (isLoading) return <TrangTrong thongDiep={t('chung.dangTai')} />
  if (isError || !lop) return <TrangTrong thongDiep={t('loi.KHONG_TIM_THAY')} />

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-3">
        <Link
          to="/lop-hoc"
          className="inline-flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="h-4 w-4" />
          {t('lopHoc.tieuDe')}
        </Link>
        <h2 className="text-lg font-semibold">{lop.ten}</h2>
        <Badge variant={mauTrangThai(lop.trangThai)}>
          {t(`trangThaiLopHoc.${lop.trangThai}`)}
        </Badge>
      </div>

      <div className="flex flex-wrap gap-1 rounded-lg border border-border p-1">
        {CAC_TAB.map((x) => (
          <button
            key={x}
            type="button"
            onClick={() => doiTab(x)}
            className={
              'rounded-md px-3 py-1.5 text-sm font-medium transition-colors ' +
              (tab === x
                ? 'bg-primary text-primary-foreground'
                : 'text-muted-foreground hover:bg-muted')
            }
          >
            {t(`lopHoc.tab_${x}`)}
          </button>
        ))}
      </div>

      {tab === 'tong-quan' && <TongQuanLop lop={lop} />}

      {tab === 'hoc-vien' && (
        <HocVienCuaLop
          nhung
          lop={lop}
          nguoiDungs={nguoiDungs ?? []}
          onDong={() => navigate('/lop-hoc')}
        />
      )}

      {tab === 'lich' && (
        <LichVaDiemDanh nhung lopHocId={lop.id} tenLop={lop.ten} onDong={() => navigate('/lop-hoc')} />
      )}

      {tab === 'bai-tap' && (
        <BaiTapCuaLop nhung lopHocId={lop.id} tenLop={lop.ten} onDong={() => navigate('/lop-hoc')} />
      )}

      {tab === 'hoc-phi' && <HocPhi lopHocId={lop.id} />}

      {tab === 'tai-lieu' && <TaiLieu lopHocId={lop.id} />}
    </div>
  )
}

/** Tab Tổng quan: thông tin lớp + bốn số liệu hay phải tra nhất. */
function TongQuanLop({ lop }: { lop: LopHocDto }) {
  const { t } = useTranslation()

  const { data: buoiHocs = [] } = useQuery({
    queryKey: ['lop-hoc', lop.id, 'buoi-hoc'],
    queryFn: async () =>
      (await api.get<{ trangThai: string }[]>(`/lop-hoc/${lop.id}/buoi-hoc`)).data,
  })

  const { data: congNo = [] } = useQuery({
    queryKey: ['cong-no', lop.id, false],
    queryFn: async () =>
      (await api.get<{ hocPhiApDung: number; daThu: number; conNo: number }[]>(
        '/hoc-phi/cong-no',
        { params: { lopHocId: lop.id, chiConNo: false } },
      )).data,
    // Học viên xem lớp của mình sẽ nhận 403 ở đây — không phải lỗi, chỉ là họ không xem sổ
    // toàn lớp được. `retry: false` để không thử lại ba lần một cách vô ích.
    retry: false,
  })

  const daHoc = buoiHocs.filter((b) => b.trangThai === 'DaHoanThanh').length
  const phaiThu = congNo.reduce((s, x) => s + x.hocPhiApDung, 0)
  const daThu = congNo.reduce((s, x) => s + x.daThu, 0)

  return (
    <div className="space-y-4">
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <ThongSo
          nhan={t('lopHoc.siSo')}
          giaTri={
            lop.sucChuaToiDa === null
              ? String(lop.soHocVien)
              : `${lop.soHocVien}/${lop.sucChuaToiDa}`
          }
        />
        <ThongSo
          nhan={t('lopHoc.buoiDaHoc')}
          giaTri={buoiHocs.length === 0 ? '—' : `${daHoc}/${buoiHocs.length}`}
        />
        <ThongSo nhan={t('hocPhi.daThu')} giaTri={tienVN(daThu)} />
        <ThongSo
          nhan={t('hocPhi.conNo')}
          giaTri={tienVN(phaiThu - daThu)}
          canhBao={phaiThu - daThu > 0}
        />
      </div>

      <Card>
        <CardContent className="pt-4">
          <dl className="grid gap-x-6 gap-y-3 sm:grid-cols-2">
            <Dong nhan={t('lopHoc.giaoVienChinh')} giaTri={lop.tenGiaoVienChinh} />
            <Dong
              nhan={t('lopHoc.troGiang')}
              giaTri={lop.tenTroGiangs.length > 0 ? lop.tenTroGiangs.join(', ') : '—'}
            />
            <Dong nhan={t('lopHoc.hinhThuc')} giaTri={t(`hinhThucHoc.${lop.hinhThuc}`)} />
            <Dong
              nhan={lop.hinhThuc === 'Online' ? t('lopHoc.linkHoc') : t('lopHoc.phongHoc')}
              giaTri={(lop.hinhThuc === 'Online' ? lop.linkHoc : lop.phongHoc) ?? '—'}
            />
            <Dong nhan={t('lopHoc.hocPhi')} giaTri={tienVN(lop.hocPhi)} />
            <Dong
              nhan={t('lopHoc.thoiGian')}
              giaTri={`${ngayVN(lop.ngayKhaiGiang)} → ${ngayVN(lop.ngayKetThuc)}`}
            />
            {lop.ghiChu && (
              <div className="sm:col-span-2">
                <dt className="text-sm text-muted-foreground">{t('lopHoc.ghiChu')}</dt>
                <dd className="whitespace-pre-wrap text-sm">{lop.ghiChu}</dd>
              </div>
            )}
          </dl>
        </CardContent>
      </Card>
    </div>
  )
}

function ThongSo({
  nhan,
  giaTri,
  canhBao,
}: {
  nhan: string
  giaTri: string
  canhBao?: boolean
}) {
  return (
    <Card>
      <CardContent className="pt-4">
        <p className="text-xs text-muted-foreground">{nhan}</p>
        <p
          className={
            'mt-1 text-xl font-semibold tabular-nums ' + (canhBao ? 'text-destructive' : '')
          }
        >
          {giaTri}
        </p>
      </CardContent>
    </Card>
  )
}

function Dong({ nhan, giaTri }: { nhan: string; giaTri: string }) {
  return (
    <div>
      <dt className="text-sm text-muted-foreground">{nhan}</dt>
      <dd className="text-sm font-medium">{giaTri}</dd>
    </div>
  )
}
