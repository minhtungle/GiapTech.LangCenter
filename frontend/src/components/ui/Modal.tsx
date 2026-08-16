import * as React from 'react'
import { X } from 'lucide-react'
import { cn } from '@/lib/utils'
import { Button } from './index'

/**
 * Modal cho thao tác thêm/cập nhật không cần chuyển view.
 *
 * Dùng `<dialog>` của trình duyệt thay vì tự dựng overlay: nó có sẵn focus trap, Esc để
 * đóng, và `::backdrop` — ba thứ mà bản tự viết hay thiếu, khiến người dùng bàn phím bị
 * kẹt tab ra ngoài modal.
 */
export function Modal({
  mo,
  onDong,
  tieuDe,
  moTa,
  children,
  chanDoiKhiXuLy = false,
  rong = 'md',
}: {
  mo: boolean
  onDong: () => void
  tieuDe: string
  moTa?: string
  children: React.ReactNode
  /** true = không cho đóng bằng Esc / bấm nền, dùng khi đang lưu dở. */
  chanDoiKhiXuLy?: boolean
  rong?: 'sm' | 'md' | 'lg'
}) {
  const ref = React.useRef<HTMLDialogElement>(null)

  React.useEffect(() => {
    const d = ref.current
    if (!d) return

    if (mo && !d.open) d.showModal()
    else if (!mo && d.open) d.close()
  }, [mo])

  // Esc kích hoạt sự kiện `cancel`; chặn khi đang lưu để không mất dữ liệu đang nhập.
  React.useEffect(() => {
    const d = ref.current
    if (!d) return

    const huy = (e: Event) => {
      e.preventDefault()
      if (!chanDoiKhiXuLy) onDong()
    }
    d.addEventListener('cancel', huy)
    return () => d.removeEventListener('cancel', huy)
  }, [onDong, chanDoiKhiXuLy])

  return (
    <dialog
      ref={ref}
      // Bấm ra nền để đóng: so target với chính dialog vì click bên trong nội dung
      // không nổi bọt lên đây.
      onClick={(e) => {
        if (e.target === ref.current && !chanDoiKhiXuLy) onDong()
      }}
      className={cn(
        'w-[calc(100vw-2rem)] rounded-lg border border-border bg-card p-0 text-card-foreground shadow-lg',
        'backdrop:bg-black/40 backdrop:backdrop-blur-[1px]',
        rong === 'sm' && 'max-w-sm',
        rong === 'md' && 'max-w-xl',
        rong === 'lg' && 'max-w-3xl',
      )}
    >
      <div className="flex items-start justify-between gap-4 border-b border-border p-4">
        <div>
          <h2 className="text-base font-semibold">{tieuDe}</h2>
          {moTa && <p className="mt-0.5 text-sm text-muted-foreground">{moTa}</p>}
        </div>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          onClick={onDong}
          disabled={chanDoiKhiXuLy}
          aria-label="Đóng"
        >
          <X className="h-4 w-4" />
        </Button>
      </div>

      <div className="max-h-[calc(100vh-16rem)] overflow-y-auto p-4">{children}</div>
    </dialog>
  )
}

/** Hàng nút ở chân modal — luôn phải/trái nhất quán giữa các màn. */
export function ModalChan({ children }: { children: React.ReactNode }) {
  return (
    <div className="mt-4 flex justify-end gap-2 border-t border-border pt-4">{children}</div>
  )
}
