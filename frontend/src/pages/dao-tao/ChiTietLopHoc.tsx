import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { ArrowLeft, CheckCircle2 } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import { Badge, Card, CardContent, TrangTrong } from '@/components/ui'
import { LichVaDiemDanh } from './LichVaDiemDanh'
import { BaiTapCuaLop } from './BaiTapCuaLop'
import { HocVienCuaLop } from './HocVienCuaLop'
import { FormLopHoc, type DuLieuLopHoc } from './FormLopHoc'
import TaiLieu from './TaiLieu'
import HocPhi from './HocPhi'
import {
  tienVN, ngayVN, mauTrangThai,
  type LopHocDto, type NguoiDungNgan,
} from './lopHocTypes'
import type { KetQuaTrang } from '@/lib/api'

/**
 * Mã tab (nằm trong URL) đi kèm khoá i18n của nó.
 *
 * Không ghép chuỗi kiểu `t(`lopHoc.tab_${x}`)`: mã tab dùng gạch NGANG cho URL đẹp còn khoá
 * i18n dùng camelCase, nên ghép động sinh ra `tab_tong-quan` — không khớp khoá nào và
 * i18next trả về nguyên chuỗi khoá cho người dùng nhìn thấy. Lỗi này đã xảy ra thật.
 * Khai tường minh thì TypeScript bắt được ngay khi thêm tab mới.
 */
const CAC_TAB = [
  { ma: 'tong-quan', khoa: 'lopHoc.tabTongQuan' },
  { ma: 'hoc-vien', khoa: 'lopHoc.tabHocVien' },
  { ma: 'lich', khoa: 'lopHoc.tabLich' },
  { ma: 'bai-tap', khoa: 'lopHoc.tabBaiTap' },
  { ma: 'hoc-phi', khoa: 'lopHoc.tabHocPhi' },
  { ma: 'tai-lieu', khoa: 'lopHoc.tabTaiLieu' },
] as const

type Tab = (typeof CAC_TAB)[number]['ma']

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
  const tab: Tab =
    tabQuery && CAC_TAB.some((x) => x.ma === tabQuery) ? tabQuery : 'tong-quan'

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

      {tab === 'tong-quan' && <TongQuanLop lop={lop} nguoiDungs={nguoiDungs ?? []} />}

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

/**
 * Tab Tổng quan: bốn số liệu hay phải tra nhất + form sửa thông tin lớp ngay tại chỗ.
 *
 * Sửa tại chỗ chứ không mở modal: người dùng đã ở trang của đúng lớp này rồi, bắt họ mở thêm
 * một lớp giao diện nữa chỉ để đổi một ô là thừa. Modal ở màn danh sách vẫn giữ vì ở đó chưa
 * chọn lớp nào.
 */
function TongQuanLop({
  lop,
  nguoiDungs,
}: {
  lop: LopHocDto
  nguoiDungs: NguoiDungNgan[]
}) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [maLoi, setMaLoi] = useState<string | null>(null)
  const [daLuu, setDaLuu] = useState(false)

  const capNhat = useMutation({
    mutationFn: (du: DuLieuLopHoc) => api.put(`/lop-hoc/${lop.id}`, { ...du, id: lop.id }),
    onSuccess: () => {
      // Làm mới cả chi tiết lẫn danh sách: tên lớp vừa đổi phải hiện đúng ở cả hai chỗ.
      void qc.invalidateQueries({ queryKey: ['lop-hoc'] })
      setMaLoi(null)
      setDaLuu(true)
      window.setTimeout(() => setDaLuu(false), 2500)
    },
    onError: (e) => {
      setMaLoi(layMaLoi(e))
      setDaLuu(false)
    },
  })

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
            <Dong nhan={t('lopHoc.ghiChu')} giaTri={lop.ghiChu ?? '—'} />
          </dl>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="space-y-3 pt-4">
          <div className="flex items-center justify-between">
            <h3 className="text-sm font-semibold">{t('lopHoc.suaThongTin')}</h3>
            {daLuu && (
              <span className="flex items-center gap-1 text-sm text-status-win">
                <CheckCircle2 className="h-4 w-4" />
                {t('chung.daLuu')}
              </span>
            )}
          </div>

          <FormLopHoc
            // Key đổi khi dữ liệu server đổi, để form nạp lại giá trị mới sau khi lưu —
            // các ô dùng `defaultValue` nên không tự cập nhật. Ghép từ chính các trường form
            // ghi đè: sửa xong query làm mới, key đổi, form remount với dữ liệu mới.
            key={[
              lop.id, lop.ten, lop.giaoVienChinhId, lop.hinhThuc,
              lop.phongHoc, lop.linkHoc, lop.hocPhi, lop.sucChuaToiDa, lop.ghiChu,
              lop.troGiangIds.join(','),
            ].join('|')}
            lop={lop}
            nguoiDungs={nguoiDungs}
            dangLuu={capNhat.isPending}
            maLoi={maLoi}
            nhanLuu={t('lopHoc.luuThongTin')}
            onLuu={(du) => capNhat.mutate(du)}
          />
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
