import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ImagePlus, Trash2, Upload } from 'lucide-react'
import { api, layMaLoi } from '@/lib/api'
import { Button, CanhBaoLoi } from '@/components/ui'
import { Anh, xoaCacheAnh } from '@/components/ui/Anh'
import { cn } from '@/lib/utils'

/**
 * Chọn và tải ảnh lên (FR-04 avatar cầu thủ, FR-06 logo / ảnh bìa CLB).
 *
 * **Tải ngay khi chọn tệp**, không chờ bấm Lưu của form bao quanh: ảnh đi qua endpoint riêng
 * (multipart) chứ không nằm trong JSON của form, gộp vào một nút Lưu sẽ phải gửi hai request
 * và xử lý trường hợp một cái thành công một cái hỏng.
 */

const LOAI_CHO_PHEP = ['image/jpeg', 'image/png', 'image/webp', 'image/gif']
const KICH_THUOC_TOI_DA = 5 * 1024 * 1024

export function ChonAnh({
  khoa,
  duongDanTai,
  duongDanXoa,
  hinhTron = false,
  onXong,
}: {
  /** Khoá ảnh hiện tại, null nếu chưa có. */
  khoa: string | null
  /** Endpoint POST nhận multipart. */
  duongDanTai: string
  /** Endpoint DELETE. */
  duongDanXoa: string
  /** Avatar cầu thủ hiển thị tròn; logo và ảnh bìa hiển thị chữ nhật. */
  hinhTron?: boolean
  onXong: (khoaMoi: string | null) => void
}) {
  const { t } = useTranslation()
  const oTep = useRef<HTMLInputElement>(null)
  const [dangTai, setDangTai] = useState(false)
  const [maLoi, setMaLoi] = useState<string | null>(null)

  const chon = async (tep: File | undefined) => {
    if (!tep) return
    setMaLoi(null)

    // Kiểm ở client TRƯỚC khi gửi: backend cũng chặn, nhưng bắt người dùng chờ tải xong 20MB
    // rồi mới báo quá lớn là phí thời gian của họ.
    if (!LOAI_CHO_PHEP.includes(tep.type)) {
      setMaLoi('LOAI_ANH_KHONG_HO_TRO')
      return
    }
    if (tep.size > KICH_THUOC_TOI_DA) {
      setMaLoi('ANH_QUA_LON')
      return
    }

    const fd = new FormData()
    fd.append('tep', tep)

    setDangTai(true)
    try {
      const { data } = await api.post<string>(duongDanTai, fd)
      // Ảnh cũ không còn tồn tại trên server — gỡ khỏi cache để thẻ Anh khác không hiện lại nó.
      xoaCacheAnh(khoa)
      onXong(data)
    } catch (e) {
      setMaLoi(layMaLoi(e))
    } finally {
      setDangTai(false)
      // Xoá giá trị input: chọn lại đúng tệp vừa chọn sẽ không kích hoạt onChange nếu
      // giá trị không đổi — người dùng thử lại sau lỗi thì tưởng nút hỏng.
      if (oTep.current) oTep.current.value = ''
    }
  }

  const xoa = async () => {
    setMaLoi(null)
    setDangTai(true)
    try {
      await api.delete(duongDanXoa)
      xoaCacheAnh(khoa)
      onXong(null)
    } catch (e) {
      setMaLoi(layMaLoi(e))
    } finally {
      setDangTai(false)
    }
  }

  return (
    <div className="flex flex-col gap-2">
      <div className="flex items-center gap-3">
        <div
          className={cn(
            'flex shrink-0 items-center justify-center overflow-hidden border border-border bg-muted',
            hinhTron ? 'h-20 w-20 rounded-full' : 'h-20 w-32 rounded-md',
          )}
        >
          <Anh
            khoa={khoa}
            className="h-full w-full object-cover"
            thayThe={<ImagePlus className="h-6 w-6 text-muted-foreground" />}
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <input
            ref={oTep}
            type="file"
            accept={LOAI_CHO_PHEP.join(',')}
            className="hidden"
            onChange={(e) => void chon(e.target.files?.[0])}
          />
          <div className="flex gap-1.5">
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={dangTai}
              onClick={() => oTep.current?.click()}
            >
              <Upload className="h-4 w-4" />
              {dangTai ? t('chung.dangTai') : khoa ? t('anh.doiAnh') : t('anh.chonAnh')}
            </Button>
            {khoa && (
              <Button
                type="button"
                variant="ghost"
                size="sm"
                disabled={dangTai}
                title={t('anh.goAnh')}
                onClick={() => void xoa()}
              >
                <Trash2 className="h-4 w-4 text-destructive" />
              </Button>
            )}
          </div>
          <p className="text-xs text-muted-foreground">{t('anh.goiY')}</p>
        </div>
      </div>

      {maLoi && <CanhBaoLoi>{t(`loi.${maLoi}`, t('loi.LOI_HE_THONG'))}</CanhBaoLoi>}
    </div>
  )
}
