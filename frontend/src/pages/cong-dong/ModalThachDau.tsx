import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, CanhBaoLoi, Input, Label, Textarea } from '@/components/ui'
import { Modal, ModalChan } from '@/components/ui/Modal'

/**
 * Hộp gửi lời mời thách đấu — dùng chung ở danh sách Cộng đồng và trang chi tiết CLB.
 *
 * Tách ra file riêng thay vì lặp: hai chỗ gửi cùng một lời mời, mà chỉ cần một chỗ quên sửa
 * (thêm trường, đổi nhãn) là hai màn hình lệch nhau và người dùng thấy hai form khác nhau cho
 * cùng một việc.
 */
export function ModalThachDau({
  tenDoi,
  sanNhaGoiY,
  maLoi,
  dangGui,
  onDong,
  onGui,
}: {
  tenDoi: string
  /** Sân nhà của đối phương — điền sẵn vào ô địa điểm, họ chủ nhà là mặc định hợp lý. */
  sanNhaGoiY: string | null
  maLoi: string | null
  dangGui: boolean
  onDong: () => void
  onGui: (form: {
    thoiGianDeXuat: string | null
    diaDiem: string | null
    loiNhan: string | null
  }) => void
}) {
  const { t } = useTranslation()
  const [thoiGian, setThoiGian] = useState('')
  const [diaDiem, setDiaDiem] = useState(sanNhaGoiY ?? '')
  const [loiNhan, setLoiNhan] = useState('')

  return (
    <Modal mo tieuDe={t('congDong.moiTieuDe', { ten: tenDoi })} onDong={onDong}>
      <form
        onSubmit={(e) => {
          e.preventDefault()
          onGui({
            // `datetime-local` trả chuỗi không có múi giờ; `new Date()` hiểu theo giờ máy rồi
            // `toISOString()` đổi sang UTC — backend chuẩn hoá UTC tập trung ở AppDbContext.
            thoiGianDeXuat: thoiGian ? new Date(thoiGian).toISOString() : null,
            diaDiem: diaDiem.trim() || null,
            loiNhan: loiNhan.trim() || null,
          })
        }}
        className="flex flex-col gap-3"
      >
        <div>
          <Label htmlFor="moiThoiGian">{t('congDong.thoiGianDeXuat')}</Label>
          <Input
            id="moiThoiGian"
            type="datetime-local"
            value={thoiGian}
            onChange={(e) => setThoiGian(e.target.value)}
          />
          <p className="mt-1 text-xs text-muted-foreground">{t('congDong.thoiGianTuyChon')}</p>
        </div>

        <div>
          <Label htmlFor="moiDiaDiem">{t('congDong.diaDiem')}</Label>
          <Input
            id="moiDiaDiem"
            value={diaDiem}
            onChange={(e) => setDiaDiem(e.target.value)}
            placeholder={sanNhaGoiY ?? t('congDong.diaDiemGoiY')}
          />
        </div>

        <div>
          <Label htmlFor="moiLoiNhan">{t('congDong.loiNhan')}</Label>
          <Textarea
            id="moiLoiNhan"
            rows={3}
            value={loiNhan}
            onChange={(e) => setLoiNhan(e.target.value)}
            placeholder={t('congDong.loiNhanGoiY')}
          />
        </div>

        {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}

        <ModalChan>
          <Button type="button" variant="outline" onClick={onDong}>
            {t('chung.huy')}
          </Button>
          <Button type="submit" disabled={dangGui}>
            {dangGui ? t('chung.dangTai') : t('congDong.gui')}
          </Button>
        </ModalChan>
      </form>
    </Modal>
  )
}
