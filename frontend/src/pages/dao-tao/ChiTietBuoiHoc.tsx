import type { ReactNode } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { ArrowLeft, ChevronLeft, ChevronRight, ExternalLink } from 'lucide-react'
import { api } from '@/lib/api'
import { useQuyen } from '@/lib/quyen'
import { Badge, Button, Card, CardContent, TrangTrong } from '@/components/ui'
import { BangDiemDanh } from './BangDiemDanh'
import { NhanXetBuoiHoc } from './NhanXetBuoiHoc'
import { BaiTapCuaLop } from './BaiTapCuaLop'
import TaiLieu from './TaiLieu'
import { type BuoiHocDto, gioVN } from './buoiHocTypes'
import { NhanTinhTrangBuoi } from '@/components/dao-tao/NhanTinhTrangBuoi'

/** Xem chú thích `CAC_TAB` trong `ChiTietLopHoc.tsx` về việc KHÔNG ghép khoá i18n động. */
const CAC_TAB = [
  { ma: 'thong-tin', khoa: 'chiTietBuoi.tabThongTin', can: undefined },
  { ma: 'diem-danh', khoa: 'chiTietBuoi.tabDiemDanh', can: 'DiemDanh' },
  { ma: 'nhan-xet', khoa: 'chiTietBuoi.tabNhanXet', can: undefined },
  { ma: 'bai-tap', khoa: 'chiTietBuoi.tabBaiTap', can: 'BaiTap' },
  { ma: 'tai-lieu', khoa: 'chiTietBuoi.tabTaiLieu', can: 'TaiLieu' },
] as const

type Tab = (typeof CAC_TAB)[number]['ma']

/**
 * FR-09 — view chi tiết một buổi học.
 *
 * Trước đây điểm danh mở dạng modal từ bảng lịch: muốn xem bài tập của buổi đó phải đóng
 * modal, sang tab khác của lớp, rồi tự lọc bằng mắt. Nay là một trang có tab, và **chuyển
 * sang buổi khác không cần về lịch** — nút trước/sau cho việc chấm lần lượt cả khoá, danh
 * sách thả xuống cho việc nhảy tới một buổi cụ thể.
 *
 * Chuyển buổi giữ nguyên tab đang xem (`?tab=` không đổi khi `id` đổi): người đang điểm danh
 * lần lượt 20 buổi không phải bấm lại tab Điểm danh 20 lần.
 */
export default function ChiTietBuoiHoc() {
  const { t } = useTranslation()
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const [sp, setSp] = useSearchParams()
  const { coQuyen, dangTai: dangTaiQuyen } = useQuyen()

  const { data: buoi, isLoading, isError } = useQuery({
    queryKey: ['buoi-hoc', id],
    queryFn: async () => (await api.get<BuoiHocDto>(`/buoi-hoc/${id}`)).data,
    enabled: !!id,
  })

  /**
   * Danh sách buổi cùng lớp, để dựng nút trước/sau và ô thả xuống.
   *
   * Dùng chung `queryKey` với tab Lịch của view lớp — mở lịch rồi bấm vào một buổi thì danh
   * sách đã có trong cache, không có nhịp tải lại.
   */
  const { data: dsBuoi = [] } = useQuery({
    queryKey: ['lop-hoc', buoi?.lopHocId, 'buoi-hoc'],
    queryFn: async () =>
      (await api.get<BuoiHocDto[]>(`/lop-hoc/${buoi!.lopHocId}/buoi-hoc`)).data,
    enabled: !!buoi?.lopHocId,
  })

  const tabHienThi = dangTaiQuyen ? CAC_TAB : CAC_TAB.filter((x) => !x.can || coQuyen(x.can))
  const tabQuery = sp.get('tab') as Tab | null
  const tab: Tab = tabQuery && tabHienThi.some((x) => x.ma === tabQuery) ? tabQuery : 'thong-tin'
  const doiTab = (x: Tab) => setSp(x === 'thong-tin' ? {} : { tab: x }, { replace: true })

  // Giữ tab khi nhảy buổi. `replace: false` — chuyển buổi LÀ một bước điều hướng, Back phải
  // quay về buổi vừa xem (khác với đổi tab, xem `doiTab`).
  const sangBuoi = (buoiId: string) =>
    navigate(`/lms/buoi-hoc/${buoiId}${tab === 'thong-tin' ? '' : `?tab=${tab}`}`)

  if (isLoading) return <TrangTrong thongDiep={t('chung.dangTai')} />
  if (isError || !buoi) return <TrangTrong thongDiep={t('chiTietBuoi.khongTimThayBuoi')} />

  const viTri = dsBuoi.findIndex((b) => b.id === buoi.id)
  const truoc = viTri > 0 ? dsBuoi[viTri - 1] : null
  const sau = viTri >= 0 && viTri < dsBuoi.length - 1 ? dsBuoi[viTri + 1] : null

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-3">
        <Link
          to={`/lms/lop-hoc/${buoi.lopHocId}?tab=lich`}
          className="inline-flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="h-4 w-4" />
          {t('chiTietBuoi.quayLaiLich')}
        </Link>
        <h2 className="text-lg font-semibold">
          {buoi.tenLopHoc} — {t('buoiHoc.thuTuNgan', { n: buoi.thuTu })}
        </h2>
        {/* Tình trạng SUY TỪ GIỜ, cùng một nhãn với bảng lịch — hiện `trangThai` thô ở đây
            thì buổi đã qua chưa chốt mang nhãn "Đã lên lịch" trong khi bảng nói "Chưa chốt",
            hai chỗ nói khác nhau về cùng một buổi (18/09/2026). */}
        <NhanTinhTrangBuoi
          trangThai={buoi.trangThai}
          batDau={buoi.batDau}
          ketThuc={buoi.ketThuc}
        />
        {buoi.laHocBu && <Badge variant="accent">{t('buoiHoc.hocBu')}</Badge>}

        <div className="ml-auto flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            disabled={!truoc}
            onClick={() => truoc && sangBuoi(truoc.id)}
            aria-label={t('chiTietBuoi.buoiTruoc')}
          >
            <ChevronLeft className="h-4 w-4" />
          </Button>
          <select
            aria-label={t('chiTietBuoi.chonBuoi')}
            value={buoi.id}
            onChange={(e) => sangBuoi(e.target.value)}
            className="h-9 max-w-[18rem] rounded-md border border-input bg-background px-2 text-sm"
          >
            {dsBuoi.map((b) => (
              <option key={b.id} value={b.id}>
                {t('buoiHoc.thuTuNgan', { n: b.thuTu })} · {gioVN(b.batDau)}
              </option>
            ))}
          </select>
          <Button
            variant="outline"
            size="sm"
            disabled={!sau}
            onClick={() => sau && sangBuoi(sau.id)}
            aria-label={t('chiTietBuoi.buoiSau')}
          >
            <ChevronRight className="h-4 w-4" />
          </Button>
        </div>
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

      {tab === 'thong-tin' && <ThongTinBuoi buoi={buoi} />}

      {tab === 'diem-danh' && (
        <BangDiemDanh
          nhung
          buoi={buoi}
          onDong={() => navigate(`/lms/lop-hoc/${buoi.lopHocId}?tab=lich`)}
          onXong={() => {}}
        />
      )}

      {tab === 'nhan-xet' && (
        <NhanXetBuoiHoc buoiHocId={buoi.id} toiLaHocVien={buoi.toiLaHocVien} />
      )}

      {tab === 'bai-tap' && (
        <BaiTapCuaLop
          nhung
          buoiHocId={buoi.id}
          lopHocId={buoi.lopHocId}
          tenLop={buoi.tenLopHoc}
          onDong={() => navigate(`/lms/lop-hoc/${buoi.lopHocId}?tab=lich`)}
        />
      )}

      {tab === 'tai-lieu' && (
        <div className="grid gap-2">
          {/* Tài liệu chỉ có FK tới LỚP, không tới buổi — nói rõ để người dùng không tưởng
              mình đang thấy tài liệu riêng của buổi này. */}
          <p className="text-sm text-muted-foreground">{t('chiTietBuoi.taiLieuTheoLop')}</p>
          <TaiLieu lopHocId={buoi.lopHocId} />
        </div>
      )}
    </div>
  )
}

function ThongTinBuoi({ buoi }: { buoi: BuoiHocDto }) {
  const { t } = useTranslation()

  return (
    <Card>
      <CardContent className="grid gap-3 pt-6 sm:grid-cols-2">
        <Dong nhan={t('buoiHoc.thoiGian')} giaTri={`${gioVN(buoi.batDau)} → ${gioVN(buoi.ketThuc)}`} />
        <Dong
          nhan={t('buoiHoc.giaoVien')}
          giaTri={
            <>
              {buoi.tenGiaoVien}
              {buoi.giaoVienRieng && (
                <Badge variant="accent" className="ml-2">
                  {t('buoiHoc.giaoVienRieng')}
                </Badge>
              )}
            </>
          }
        />
        <Dong
          nhan={t('buoiHoc.troGiang')}
          giaTri={
            buoi.tenTroGiangs.length > 0 ? (
              <>
                {buoi.tenTroGiangs.join(', ')}
                {/* Nói rõ trợ giảng lấy theo LỚP, để người dùng không tưởng buổi này được
                    phân công riêng — hiện chưa có bảng phân công theo buổi. */}
                <span className="ml-2 text-xs text-muted-foreground">
                  {t('buoiHoc.troGiangTheoLop')}
                </span>
              </>
            ) : (
              <span className="text-muted-foreground">{t('buoiHoc.khongCoTroGiang')}</span>
            )
          }
        />
        <Dong
          nhan={t('buoiHoc.phongHoc')}
          giaTri={
            buoi.phongHocHieuLuc ? (
              <>
                {buoi.phongHocHieuLuc}
                {!buoi.diaDiemRieng && (
                  <span className="ml-2 text-xs text-muted-foreground">
                    {t('buoiHoc.theoLop')}
                  </span>
                )}
              </>
            ) : (
              '—'
            )
          }
        />
        <Dong
          nhan={t('buoiHoc.linkHoc')}
          giaTri={
            buoi.linkHocHieuLuc ? (
              <a
                href={buoi.linkHocHieuLuc}
                target="_blank"
                rel="noreferrer noopener"
                className="inline-flex items-center gap-1 text-primary hover:underline"
              >
                {buoi.linkHocHieuLuc}
                <ExternalLink className="h-3.5 w-3.5" />
              </a>
            ) : (
              '—'
            )
          }
        />
        <Dong
          nhan={t('buoiHoc.diemDanh')}
          giaTri={`${buoi.soDaDiemDanh}/${buoi.soHocVien}`}
        />
        <Dong
          nhan={t('lopHoc.tieuDe')}
          giaTri={
            <Link to={`/lms/lop-hoc/${buoi.lopHocId}`} className="text-primary hover:underline">
              {buoi.tenLopHoc}
            </Link>
          }
        />
        <div className="sm:col-span-2">
          <Dong nhan={t('buoiHoc.ghiChu')} giaTri={buoi.ghiChu ?? '—'} />
        </div>
      </CardContent>
    </Card>
  )
}

function Dong({ nhan, giaTri }: { nhan: string; giaTri: ReactNode }) {
  return (
    <div className="grid gap-0.5">
      <span className="text-xs uppercase tracking-wide text-muted-foreground">{nhan}</span>
      <span className="text-sm">{giaTri}</span>
    </div>
  )
}
