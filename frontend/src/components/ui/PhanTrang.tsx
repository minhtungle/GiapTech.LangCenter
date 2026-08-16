import { useTranslation } from 'react-i18next'
import { ChevronLeft, ChevronRight } from 'lucide-react'
import { Button } from './index'
import { cn } from '@/lib/utils'

/** Số dòng mỗi trang người dùng chọn được. */
export const CAC_MUC_SO_DONG = [10, 20, 50, 100] as const

/**
 * Thanh phân trang dùng chung cho mọi bảng.
 *
 * Hiển thị cả **tổng số dòng**, không chỉ số trang: người dùng cần biết "có bao nhiêu cầu thủ"
 * mà không phải nhân số trang với số dòng.
 */
export function PhanTrang({
  trang,
  soDong,
  tongSoDong,
  tongSoTrang,
  onDoiTrang,
  onDoiSoDong,
}: {
  trang: number
  soDong: number
  tongSoDong: number
  tongSoTrang: number
  onDoiTrang: (trang: number) => void
  onDoiSoDong: (soDong: number) => void
}) {
  const { t } = useTranslation()

  // Không có gì để phân trang thì không chiếm chỗ.
  if (tongSoDong === 0) return null

  const tu = (trang - 1) * soDong + 1
  const den = Math.min(trang * soDong, tongSoDong)

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 text-sm">
      <span className="text-muted-foreground">
        {t('phanTrang.dangXem', { tu, den, tong: tongSoDong })}
      </span>

      <div className="flex items-center gap-3">
        <label className="flex items-center gap-1.5 text-muted-foreground">
          {t('phanTrang.soDong')}
          <select
            value={soDong}
            onChange={(e) => onDoiSoDong(Number(e.target.value))}
            className="h-8 rounded-md border border-input bg-background px-2 text-sm text-foreground"
          >
            {CAC_MUC_SO_DONG.map((n) => (
              <option key={n} value={n}>
                {n}
              </option>
            ))}
          </select>
        </label>

        <div className="flex items-center gap-1">
          <Button
            variant="outline"
            size="sm"
            disabled={trang <= 1}
            onClick={() => onDoiTrang(trang - 1)}
            aria-label={t('phanTrang.trangTruoc')}
          >
            <ChevronLeft className="h-4 w-4" />
          </Button>

          <span className={cn('min-w-20 text-center text-muted-foreground')}>
            {trang} / {tongSoTrang}
          </span>

          <Button
            variant="outline"
            size="sm"
            disabled={trang >= tongSoTrang}
            onClick={() => onDoiTrang(trang + 1)}
            aria-label={t('phanTrang.trangSau')}
          >
            <ChevronRight className="h-4 w-4" />
          </Button>
        </div>
      </div>
    </div>
  )
}
