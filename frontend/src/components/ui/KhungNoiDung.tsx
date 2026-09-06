import * as React from 'react'
import { Modal } from './Modal'

/**
 * Bọc nội dung trong Modal, hoặc render thẳng khi nhúng vào một tab.
 *
 * Vì sao cần: `LichVaDiemDanh`, `BaiTapCuaLop`, `HocVienCuaLop` viết ra để mở dạng modal từ
 * bảng lớp học. Khi chuyển sang view chi tiết có tab, chúng phải render thẳng — nhưng viết
 * lại cả ba là chép hàng trăm dòng và từ đó hai bản sẽ trôi khỏi nhau.
 *
 * Component này giữ đúng một nguồn: mỗi file chỉ đổi thẻ ngoài cùng, phần thân không đụng tới.
 */
export function KhungNoiDung({
  nhung,
  tieuDe,
  onDong,
  rong,
  children,
}: {
  /** true = render thẳng (đang là tab), false = bọc Modal (mở từ bảng). */
  nhung?: boolean
  tieuDe: string
  onDong: () => void
  rong?: 'sm' | 'md' | 'lg' | 'xl'
  children: React.ReactNode
}) {
  if (nhung) return <>{children}</>

  return (
    <Modal mo onDong={onDong} tieuDe={tieuDe} rong={rong}>
      {children}
    </Modal>
  )
}
