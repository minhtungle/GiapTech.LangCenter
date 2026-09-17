import { useTranslation } from 'react-i18next'
import { Badge } from '@/components/ui'
import {
  type TinhTrangBuoi,
  type TrangThaiBuoiHoc,
  tinhTrangHienTai,
} from '@/lib/tinhTrangBuoi'

/** Badge nào cho tình trạng nào — khai một chỗ để bảng và lịch không tô khác nhau. */
const BIEN_THE: Record<TinhTrangBuoi, 'muted' | 'ok' | 'loi' | 'cho' | 'dang' | 'doi'> = {
  ChuaBatDau: 'muted',
  DangDienRa: 'dang',
  ChuaChot: 'cho',
  DaXong: 'ok',
  ChuyenLich: 'doi',
  DaHuy: 'loi',
}

/**
 * Nhãn tình trạng buổi học (18/09/2026) — *"thêm màu sắc cho từng trạng thái ... để dễ nhận
 * biết đúng"*.
 *
 * Tự suy tình trạng từ GIỜ chứ không nhận sẵn `tinhTrang` của API: màn lịch mở cả buổi sáng,
 * buổi 9h phải tự chuyển "chưa bắt đầu" → "đang diễn ra" mà không chờ tải lại (xem
 * `lib/tinhTrangBuoi.ts`). Truyền `tinhTrang` để ghi đè khi đã có giá trị sẵn.
 */
export function NhanTinhTrangBuoi({
  trangThai,
  batDau,
  ketThuc,
  tinhTrang,
  className,
}: {
  trangThai: TrangThaiBuoiHoc
  batDau: string
  ketThuc: string
  tinhTrang?: TinhTrangBuoi
  className?: string
}) {
  const { t } = useTranslation()
  const tt = tinhTrang ?? tinhTrangHienTai(trangThai, batDau, ketThuc)

  return (
    <Badge variant={BIEN_THE[tt]} className={className}>
      {t(`tinhTrangBuoi.${tt}`)}
    </Badge>
  )
}
