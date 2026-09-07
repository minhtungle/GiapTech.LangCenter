import { useTranslation } from 'react-i18next'
import { Construction } from 'lucide-react'
import { Card, CardContent } from '@/components/ui'

/**
 * Khung trống cho module đã có QUYỀN nhưng chưa có nghiệp vụ.
 *
 * Vì sao có màn này thay vì chưa thêm mục vào sidebar: cấu trúc ba hệ thống (HRM · CRM · LMS)
 * chỉ kiểm chứng được khi bấm vào thật — bộ chuyển, sidebar lọc theo hệ thống, ma trận phân
 * quyền theo tab. Mục dẫn tới trang 404 thì trông như lỗi; nói rõ "đang phát triển" thì người
 * dùng biết chỗ này sẽ có gì.
 *
 * Xoá dần khi từng module có nghiệp vụ thật.
 */
export function DangPhatTrien({
  tieuDe,
  moTa,
}: {
  tieuDe: string
  /** Một câu nói module này sẽ làm gì — để người dùng biết mình đang chờ cái gì. */
  moTa?: string
}) {
  const { t } = useTranslation()

  return (
    <Card>
      <CardContent className="flex flex-col items-center gap-3 py-14 text-center">
        <Construction className="h-8 w-8 text-muted-foreground" />
        <div>
          <p className="font-semibold">{tieuDe}</p>
          <p className="mt-1 text-sm text-muted-foreground">
            {moTa ?? t('chung.dangPhatTrien')}
          </p>
        </div>
        <p className="max-w-md text-xs text-muted-foreground">{t('chung.dangPhatTrienGoiY')}</p>
      </CardContent>
    </Card>
  )
}
