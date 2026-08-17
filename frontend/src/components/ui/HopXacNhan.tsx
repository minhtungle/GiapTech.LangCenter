import { useTranslation } from 'react-i18next'
import { AlertTriangle } from 'lucide-react'
import { Button } from '@/components/ui'
import { Modal, ModalChan } from '@/components/ui/Modal'

/**
 * Hộp xác nhận cho thao tác **phá huỷ** — xoá hết sơ đồ, chép đè hiệp, áp mẫu.
 *
 * Dùng thay `confirm()` của trình duyệt vì hai lẽ: `confirm()` không nói được **cụ thể mất
 * cái gì** ("Xoá 11 cầu thủ khỏi sơ đồ hiệp 2"), và nó chặn cả luồng JS nên không dịch được
 * theo ngôn ngữ đang chọn.
 *
 * Chỉ dùng cho việc **ghi đè/xoá hàng loạt không hoàn tác được**. Thao tác một quân (nháy đúp
 * bỏ một người khỏi sân) thì không hỏi — hỏi mọi thứ thì người dùng bấm Đồng ý theo phản xạ
 * và hộp thoại mất hết tác dụng.
 */
export function HopXacNhan({
  mo,
  tieuDe,
  thongDiep,
  nhanDongY,
  onDongY,
  onHuy,
}: {
  mo: boolean
  tieuDe: string
  /** Nói rõ mất gì, kèm số lượng. "Bạn có chắc không?" là câu vô nghĩa. */
  thongDiep: string
  nhanDongY?: string
  onDongY: () => void
  onHuy: () => void
}) {
  const { t } = useTranslation()

  return (
    <Modal mo={mo} onDong={onHuy} tieuDe={tieuDe} rong="sm">
      <div className="flex gap-3">
        <AlertTriangle className="mt-0.5 h-5 w-5 shrink-0 text-[hsl(var(--status-draw))]" />
        <p className="text-sm text-muted-foreground">{thongDiep}</p>
      </div>

      <ModalChan>
        {/* autoFocus vào nút HUỶ, không phải Đồng ý: gõ Enter theo quán tính thì phải rơi vào
            hành động an toàn, không phải hành động xoá. */}
        <Button type="button" variant="outline" autoFocus onClick={onHuy}>
          {t('chung.huy')}
        </Button>
        <Button type="button" onClick={onDongY}>
          {nhanDongY ?? t('chung.dongY')}
        </Button>
      </ModalChan>
    </Modal>
  )
}
