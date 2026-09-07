import { useCallback, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { HopXacNhan } from '@/components/ui/HopXacNhan'

interface YeuCau {
  tieuDe: string
  thongDiep: string
  nhanDongY?: string
  /** true = tô đỏ nút đồng ý (xoá, huỷ, gỡ). */
  nguyHiem?: boolean
  onDongY: () => void
}

/**
 * Hỏi xác nhận trước thao tác ghi dữ liệu.
 *
 * Vì sao là hook chứ không phải state ở từng màn: 11 màn × nhiều thao tác mỗi màn = hàng chục
 * cặp `useState` + `<HopXacNhan>` gần giống hệt nhau. Một chỗ duy nhất thì lời văn, hành vi
 * focus và cách tô màu nhất quán ở mọi nơi.
 *
 * ```tsx
 * const { hoi, hop } = useXacNhan()
 * <Button onClick={() => hoi({
 *   tieuDe: t('chung.xacNhanLuu'), thongDiep: t('chung.hoiLuu', { ten }),
 *   onDongY: () => luu.mutate(du),
 * })}>Lưu</Button>
 * {hop}
 * ```
 *
 * **Lưu ý về việc hỏi quá nhiều.** Chủ dự án chọn hỏi trước MỌI thao tác thêm/sửa/xoá, kể cả
 * bấm Lưu trong form. Đánh đổi đã được nêu và chấp nhận: người dùng có thể bấm Đồng ý theo
 * phản xạ, làm hộp thoại mất tác dụng đúng lúc cần nhất. Để giảm rủi ro đó, lời văn phải nói
 * **cụ thể mất gì hoặc đổi gì** — "Bạn có chắc không?" là câu vô nghĩa.
 */
export function useXacNhan() {
  const { t } = useTranslation()
  const [cho, setCho] = useState<YeuCau | null>(null)

  const hoi = useCallback((y: YeuCau) => setCho(y), [])

  const hop = (
    <HopXacNhan
      mo={cho !== null}
      tieuDe={cho?.tieuDe ?? ''}
      thongDiep={cho?.thongDiep ?? ''}
      nhanDongY={cho?.nhanDongY ?? t('chung.dongY')}
      nguyHiem={cho?.nguyHiem}
      onHuy={() => setCho(null)}
      onDongY={() => {
        // Đóng TRƯỚC khi chạy: nếu hành động mở modal khác (ví dụ form), hai lớp modal chồng
        // nhau và người dùng không biết bấm vào đâu.
        const y = cho
        setCho(null)
        y?.onDongY()
      }}
    />
  )

  return { hoi, hop }
}
