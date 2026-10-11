import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { SlidersHorizontal } from 'lucide-react'
import { Button } from '@/components/ui'
import { Modal, ModalChan } from '@/components/ui/Modal'

/**
 * Nút "Lọc thêm" mở **modal** chứa các bộ lọc phụ (11/10/2026).
 *
 * ## Vì sao modal, không phải panel mở xuống
 *
 * Panel cũ đẩy bảng xuống dưới màn hình: bật lọc xong phải cuộn mới thấy kết quả, mà kết quả
 * mới là thứ người dùng đang chờ. Modal mở đè lên, đóng lại là bảng vẫn ở nguyên chỗ cũ.
 *
 * Chủ sản phẩm yêu cầu 11/10/2026 để tiết kiệm diện tích màn.
 *
 * ## Vì sao vẫn giữ con số trên nút
 *
 * Đóng modal rồi thì không còn gì cho thấy đang lọc, mà bảng bên dưới lại ít dòng một cách
 * không giải thích được — đây là chỗ người dùng dễ tưởng mất dữ liệu.
 */
export function NutLocThem({
  soLocDangBat,
  onXoaLoc,
  children,
}: {
  /** Số bộ lọc đang bật. 0 = ẩn cả huy hiệu lẫn nút xoá lọc. */
  soLocDangBat: number
  onXoaLoc: () => void
  /** Các ô lọc. Đặt trong modal nên tha hồ xuống dòng, không phải nhét vừa một hàng. */
  children: React.ReactNode
}) {
  const { t } = useTranslation()
  const [mo, setMo] = useState(false)

  return (
    <>
      <Button variant="outline" onClick={() => setMo(true)}>
        <SlidersHorizontal className="h-4 w-4" />
        {t('crmLoc.locThem')}
        {soLocDangBat > 0 && (
          <span className="ml-1 rounded-full bg-primary px-1.5 text-xs text-primary-foreground">
            {soLocDangBat}
          </span>
        )}
      </Button>

      {soLocDangBat > 0 && (
        <Button variant="ghost" onClick={onXoaLoc}>
          {t('crmLoc.xoaLoc')}
        </Button>
      )}

      <Modal mo={mo} onDong={() => setMo(false)} tieuDe={t('crmLoc.locThem')} rong="md">
        {/*
          Không có nút "Áp dụng": mỗi ô lọc đổi là bảng lọc ngay, nên người dùng thấy kết quả
          mà không phải đóng modal lại. Nút dưới chỉ để đóng.
        */}
        <div className="grid gap-3 sm:grid-cols-2">{children}</div>

        <ModalChan>
          {soLocDangBat > 0 && (
            <Button variant="outline" onClick={onXoaLoc}>
              {t('crmLoc.xoaLoc')}
            </Button>
          )}
          <Button onClick={() => setMo(false)}>{t('chung.dong')}</Button>
        </ModalChan>
      </Modal>
    </>
  )
}
