import { ArrowDown, ArrowUp, ChevronsUpDown } from 'lucide-react'
import { Th } from '@/components/ui'
import { cn } from '@/lib/utils'

/**
 * Ô tiêu đề bảng bấm được để sắp xếp.
 *
 * Bấm lần đầu → giảm dần (giá trị lớn/mới nhất lên trước — cái người dùng thường tìm);
 * bấm lại → tăng dần; bấm cột khác → cột đó thành cột sắp xếp.
 *
 * Không có trạng thái thứ ba "bỏ sắp xếp": bảng luôn phải có thứ tự xác định, nếu không thì
 * phân trang trả về kết quả không ổn định giữa các trang.
 *
 * `aria-sort` để trình đọc màn hình đọc được trạng thái — biểu tượng mũi tên chỉ nói với
 * người nhìn thấy nó.
 */
export function ThSapXep<TCot extends string>({
  cot,
  cotHienTai,
  tangDan,
  onDoi,
  className,
  children,
}: {
  cot: TCot
  cotHienTai: TCot
  tangDan: boolean
  onDoi: (cot: TCot, tangDan: boolean) => void
  className?: string
  children: React.ReactNode
}) {
  const dangSap = cot === cotHienTai

  return (
    <Th
      className={cn('p-0', className)}
      aria-sort={dangSap ? (tangDan ? 'ascending' : 'descending') : 'none'}
    >
      <button
        type="button"
        // Cột đang sắp thì đảo chiều; cột khác thì bắt đầu bằng giảm dần.
        onClick={() => onDoi(cot, dangSap ? !tangDan : false)}
        className={cn(
          'flex w-full items-center gap-1 px-3 py-2 text-left transition-colors hover:text-foreground',
          dangSap ? 'font-semibold text-foreground' : 'text-muted-foreground',
        )}
      >
        {children}
        {dangSap ? (
          tangDan ? (
            <ArrowUp className="h-3.5 w-3.5 shrink-0" />
          ) : (
            <ArrowDown className="h-3.5 w-3.5 shrink-0" />
          )
        ) : (
          // Biểu tượng mờ trên MỌI cột sắp được, không chỉ cột đang sắp: người dùng phải
          // nhìn ra cột nào bấm được mà không cần rê chuột thử từng cái.
          <ChevronsUpDown className="h-3.5 w-3.5 shrink-0 opacity-40" />
        )}
      </button>
    </Th>
  )
}
