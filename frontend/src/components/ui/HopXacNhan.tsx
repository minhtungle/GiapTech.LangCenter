import { useTranslation } from 'react-i18next'
import { AlertTriangle, HelpCircle } from 'lucide-react'
import { Button } from '@/components/ui'
import { Modal, ModalChan } from '@/components/ui/Modal'

/**
 * Hộp xác nhận trước thao tác ghi dữ liệu.
 *
 * Dùng thay `confirm()` của trình duyệt vì hai lẽ: `confirm()` không nói được **cụ thể mất
 * cái gì** ("Xoá 3 nhóm quyền"), và nó chặn cả luồng JS nên không dịch được theo ngôn ngữ
 * đang chọn.
 *
 * Phần lớn trường hợp nên gọi qua hook `useXacNhan()` (`src/lib/xacNhan.tsx`) thay vì dựng
 * state riêng — một chỗ duy nhất thì lời văn và hành vi nhất quán ở mọi màn.
 *
 * **Lời văn phải nói cụ thể mất gì hoặc đổi gì.** Từ 07/09/2026 hệ thống hỏi trước mọi thao
 * tác thêm/sửa/xoá, nên rủi ro lớn nhất là người dùng bấm Đồng ý theo phản xạ; câu "Bạn có
 * chắc không?" làm điều đó chắc chắn xảy ra.
 */
export function HopXacNhan({
  mo,
  tieuDe,
  thongDiep,
  nhanDongY,
  nguyHiem,
  onDongY,
  onHuy,
}: {
  mo: boolean
  tieuDe: string
  /** Nói rõ mất gì, kèm số lượng. "Bạn có chắc không?" là câu vô nghĩa. */
  thongDiep: string
  nhanDongY?: string
  /**
   * true = nút đồng ý màu đỏ. Chỉ dùng cho thao tác PHÁ HUỶ — tô đỏ cả nút Lưu thì màu đỏ
   * mất nghĩa cảnh báo.
   */
  nguyHiem?: boolean
  onDongY: () => void
  onHuy: () => void
}) {
  const { t } = useTranslation()

  return (
    <Modal mo={mo} onDong={onHuy} tieuDe={tieuDe} rong="sm">
      <div className="flex gap-3">
        {/* Tam giác cảnh báo chỉ hiện với thao tác phá huỷ. Hiện ở mọi hộp — kể cả xác
            nhận Lưu — thì biểu tượng cảnh báo mất nghĩa. */}
        {nguyHiem ? (
          <AlertTriangle className="mt-0.5 h-5 w-5 shrink-0 text-destructive" />
        ) : (
          <HelpCircle className="mt-0.5 h-5 w-5 shrink-0 text-muted-foreground" />
        )}
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
