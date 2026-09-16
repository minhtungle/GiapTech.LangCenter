import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import {
  AlertTriangle, CalendarDays, ChevronRight, ClipboardCheck, GraduationCap, MapPin, Settings,
  ShieldCheck, UserPlus, Users, Video,
} from 'lucide-react'
import { useQuery } from '@tanstack/react-query'
import { api } from '@/lib/api'
import {
  Badge, Card, CardContent, CardHeader, CardTitle, TrangTrong,
} from '@/components/ui'
import { useAuth } from '@/lib/auth'
import { useQuyen } from '@/lib/quyen'
import { gioVN } from '@/pages/dao-tao/buoiHocTypes'

/** FR-15 — số liệu màn Tổng quan, đã lọc theo phạm vi người đang đăng nhập. */
interface TongQuanDto {
  buoiHomNay: number
  buoiQuaHanChuaDiemDanh: number
  baiNopChuaCham: number
  /** 0 với người không có `LopHoc.Sua` — backend tự gác. */
  choXepLop: number
  lopDangHoatDong: number
  buoiSapToi: BuoiSapToi[]
}

/** Một buổi sắp tới — đủ thông tin để quyết định có mở hay không mà không phải bấm vào. */
interface BuoiSapToi {
  id: string
  lopHocId: string
  tenLopHoc: string
  thuTu: number
  batDau: string
  ketThuc: string
  phongHoc: string | null
  linkHoc: string | null
  laHocBu: boolean
}

/**
 * Màn Tổng quan — màn ĐẦU TIÊN người dùng thấy sau khi đăng nhập (FR-15).
 *
 * Hai nguyên tắc giữ từ bản base, rút ra từ dự án trước:
 *
 * 1. **Mỗi dòng việc phải bấm được để tới đúng chỗ xử lý.** Một con số không kèm đường đi tiếp
 *    chỉ làm người dùng biết có việc mà không biết làm ở đâu.
 * 2. **Không làm dải số thống kê chỉ để lấp chỗ trống.** Nên ở đây không có "tổng số học viên"
 *    hay "tổng số lớp" — đó là thông tin để ngắm, không phải việc để làm.
 *
 * **Một màn cho mọi vai trò**, không tách ba dashboard: `IPhamViLopHoc` ở backend đã lọc đúng
 * phạm vi từng người (admin thấy toàn trung tâm, giáo viên thấy lớp mình dạy, học viên thấy lớp
 * mình học), nên cùng một màn cho ra nội dung đúng với từng người. Ba bản sao là ba chỗ phải sửa
 * khi đổi.
 *
 * **Không có số tiền nào** (12/09/2026): LMS không hiển thị tiền học; chỉ CRM nắm số tiền.
 */
export default function TongQuan() {
  const { t } = useTranslation()
  const { phien } = useAuth()
  const { coQuyen } = useQuyen()

  const { data: tq, isLoading } = useQuery({
    queryKey: ['tong-quan'],
    queryFn: async () => (await api.get<TongQuanDto>('/toi/tong-quan')).data,
  })

  /**
   * Việc cần làm — chỉ hiện dòng có số > 0.
   *
   * Dòng "0 việc" không phải tin vui đáng chiếm chỗ: người dùng vào đây để biết **phải làm gì**,
   * và một danh sách toàn số 0 làm họ phải đọc để nhận ra không có gì. Hết việc thì hiện đúng
   * một câu.
   */
  const viec = [
    {
      hien: (tq?.buoiQuaHanChuaDiemDanh ?? 0) > 0,
      so: tq?.buoiQuaHanChuaDiemDanh,
      nhan: t('tongQuan.buoiQuaHan'),
      icon: AlertTriangle,
      to: '/lms/lop-hoc',
      canhBao: true,
    },
    {
      hien: (tq?.baiNopChuaCham ?? 0) > 0,
      so: tq?.baiNopChuaCham,
      nhan: t('tongQuan.baiChuaCham'),
      icon: ClipboardCheck,
      to: '/lms/lop-hoc',
    },
    {
      // Backend đã trả 0 cho người không có `LopHoc.Sua`; kiểm thêm ở đây để không hiện dòng
      // nhấp nháy trong lúc chờ API.
      hien: (tq?.choXepLop ?? 0) > 0 && coQuyen('LopHoc', 'Sua'),
      so: tq?.choXepLop,
      nhan: t('tongQuan.choXepLop'),
      icon: UserPlus,
      to: '/lms/lop-hoc?tab=cho-xep-lop',
    },
  ].filter((x) => x.hien)

  /** Bối cảnh — không phải việc cần làm, nên tách khỏi danh sách trên. */
  const boiCanh = [
    { so: tq?.buoiHomNay, nhan: t('tongQuan.buoiHomNay'), icon: CalendarDays },
    { so: tq?.lopDangHoatDong, nhan: t('tongQuan.lopDangHoatDong'), icon: GraduationCap },
  ]

  const loiVaoQuanTri = [
    { to: '/quan-tri/tai-khoan', nhan: t('menu.taiKhoan'), icon: Users, can: 'TaiKhoan' },
    { to: '/quan-tri/phan-quyen', nhan: t('menu.phanQuyen'), icon: ShieldCheck, can: 'PhanQuyen' },
    { to: '/quan-tri/thiet-lap', nhan: t('menu.thietLap'), icon: Settings, can: 'ThietLapChung' },
  ].filter((x) => coQuyen(x.can))

  return (
    <div className="grid max-w-3xl gap-4">
      <Card>
        <CardHeader>
          <CardTitle>{phien?.tenTrungTam || phien?.maTrungTam || t('menu.tongQuan')}</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 pt-0">
          {isLoading ? (
            <p className="text-sm text-muted-foreground">{t('chung.dangTai')}</p>
          ) : (
            <>
              {/* Bối cảnh hôm nay — hai số, đọc lướt. */}
              <div className="grid gap-3 sm:grid-cols-2">
                {boiCanh.map(({ so, nhan, icon: Icon }) => (
                  <div
                    key={nhan}
                    className="flex items-center gap-3 rounded-lg border border-border p-3"
                  >
                    <Icon className="h-5 w-5 shrink-0 text-muted-foreground" />
                    <div>
                      <div className="text-xl font-semibold">{so ?? 0}</div>
                      <div className="text-xs text-muted-foreground">{nhan}</div>
                    </div>
                  </div>
                ))}
              </div>

              {/*
                BUỔI SẮP TỚI (17/09/2026) — đủ tên lớp, số buổi, thời gian, và bấm được tới
                thẳng chi tiết buổi.

                Trước đó Tổng quan chỉ có con số "buổi học hôm nay": không nói được lớp nào,
                mấy giờ, và không bấm được — người dùng biết có việc mà vẫn phải đi tìm.

                Ẩn hẳn khi không có buổi nào, không hiện khối rỗng: Tổng quan chỉ nêu việc CÓ
                THẬT (nguyên tắc của màn này), khối trống chỉ làm loãng.
              */}
              {(tq?.buoiSapToi.length ?? 0) > 0 && (
                <div className="grid gap-1.5">
                  <h3 className="text-sm font-semibold">{t('tongQuan.buoiSapToi')}</h3>
                  <ul className="divide-y divide-border">
                    {tq!.buoiSapToi.map((b) => (
                      <li key={b.id}>
                        {/* Cả dòng là link tới CHI TIẾT BUỔI, không phải màn lớp. */}
                        <Link
                          to={`/lms/buoi-hoc/${b.id}`}
                          className="flex items-center gap-3 py-2.5 text-sm hover:text-[hsl(var(--primary))]"
                        >
                          <CalendarDays className="h-4 w-4 shrink-0 text-muted-foreground" />
                          <div className="min-w-0 flex-1">
                            <div className="flex flex-wrap items-baseline gap-x-2">
                              <span className="truncate font-medium">{b.tenLopHoc}</span>
                              <span className="text-xs text-muted-foreground">
                                {t('tongQuan.buoiThu', { so: b.thuTu })}
                              </span>
                              {b.laHocBu && (
                                <Badge variant="accent">{t('buoiHoc.hocBu')}</Badge>
                              )}
                            </div>
                            <div className="flex flex-wrap items-center gap-x-3 text-xs text-muted-foreground">
                              <span>{gioVN(b.batDau)}</span>
                              {/* Phòng/link: thứ người dùng cần NGAY trước giờ học. */}
                              {b.phongHoc && (
                                <span className="inline-flex items-center gap-1">
                                  <MapPin className="h-3 w-3" />
                                  {b.phongHoc}
                                </span>
                              )}
                              {b.linkHoc && (
                                <span className="inline-flex items-center gap-1">
                                  <Video className="h-3 w-3" />
                                  {t('buoiHoc.hocOnline')}
                                </span>
                              )}
                            </div>
                          </div>
                          <ChevronRight className="h-4 w-4 shrink-0 text-muted-foreground" />
                        </Link>
                      </li>
                    ))}
                  </ul>
                </div>
              )}

              {viec.length === 0 ? (
                <TrangTrong thongDiep={t('tongQuan.khongCoViec')} />
              ) : (
                <div className="grid gap-1.5">
                  <h3 className="text-sm font-semibold">{t('tongQuan.viecCanLam')}</h3>
                  <ul className="divide-y divide-border">
                    {viec.map(({ so, nhan, icon: Icon, to, canhBao }) => (
                      <li key={nhan}>
                        {/* Cả dòng là link — nguyên tắc "mỗi việc bấm được tới chỗ xử lý". */}
                        <Link
                          to={to}
                          className="flex items-center gap-3 py-2.5 text-sm hover:text-[hsl(var(--primary))]"
                        >
                          <Icon
                            className={
                              'h-4 w-4 shrink-0 ' +
                              (canhBao ? 'text-status-cho' : 'text-muted-foreground')
                            }
                          />
                          <span className="flex-1">{nhan}</span>
                          <span className="font-semibold">{so}</span>
                          <ChevronRight className="h-4 w-4 shrink-0 text-muted-foreground" />
                        </Link>
                      </li>
                    ))}
                  </ul>
                </div>
              )}
            </>
          )}
        </CardContent>
      </Card>

      {/* Ẩn hẳn khi không có mục nào — khung rỗng trông như giao diện hỏng. */}
      {loiVaoQuanTri.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">{t('menu.quanTri')}</CardTitle>
          </CardHeader>
          <CardContent className="pt-0">
            <ul className="divide-y divide-border">
              {loiVaoQuanTri.map(({ to, nhan, icon: Icon }) => (
                <li key={to}>
                  <Link
                    to={to}
                    className="flex items-center gap-3 py-2.5 text-sm hover:text-[hsl(var(--primary))]"
                  >
                    <Icon className="h-4 w-4 shrink-0 text-muted-foreground" />
                    <span className="flex-1">{nhan}</span>
                    <ChevronRight className="h-4 w-4 shrink-0 text-muted-foreground" />
                  </Link>
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      )}
    </div>
  )
}
