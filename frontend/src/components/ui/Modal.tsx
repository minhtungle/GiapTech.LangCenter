import * as React from 'react'
import { X } from 'lucide-react'
import { cn } from '@/lib/utils'
import { Button } from './index'

/**
 * Ngăn xếp modal đang mở, theo thứ tự mở.
 *
 * Vì sao cần: `showModal()` đưa dialog vào **top layer**, và `::backdrop` của dialog trên chỉ
 * che nội dung trang thường — **không** che dialog khác cũng đang ở top layer. Nên khi một
 * modal mở tiếp hộp xác nhận, nút của modal dưới vẫn nhìn thấy VÀ vẫn bấm được.
 *
 * Hậu quả thật đã gặp (11/09/2026): hộp xác nhận "Duyệt vào lớp" nằm đè lên modal duyệt, mà
 * **cả hai nút cùng nhãn "Duyệt vào lớp"** (i18n `xepLop.duyet`), cùng màu, cách nhau ~77px.
 * Bấm nhầm nút của modal dưới thì mở lại chính hộp xác nhận đó — không ghi gì, không lỗi gì.
 * Người dùng tưởng đã duyệt xong; F5 thì dòng quay lại hàng chờ.
 *
 * Chỉ modal TRÊN CÙNG được nhận tương tác; các lớp dưới bị `inert` (không click, không tab,
 * không focus) và mờ đi để thấy rõ lớp nào đang hoạt động.
 */
const dangMo: HTMLDialogElement[] = []

/** Cập nhật `inert` + độ mờ cho mọi modal đang mở: chỉ lớp trên cùng còn hoạt động. */
function capNhatLopTrenCung() {
  dangMo.forEach((d, i) => {
    const duoiCung = i < dangMo.length - 1
    // `inert` chặn click/tab/focus ở cả cây con — không tự làm bằng pointer-events được, vì
    // pointer-events vẫn cho tab tới nút và gõ Enter.
    d.inert = duoiCung
    d.classList.toggle('opacity-40', duoiCung)
    // Backdrop của lớp dưới cộng dồn làm nền đen đặc hơn ở mỗi lớp; tắt để giữ một tầng nền.
    d.classList.toggle('backdrop:bg-transparent', duoiCung)
  })
}

/**
 * Modal cho thao tác thêm/cập nhật không cần chuyển view.
 *
 * Dùng `<dialog>` của trình duyệt thay vì tự dựng overlay: nó có sẵn focus trap, Esc để
 * đóng, và `::backdrop` — ba thứ mà bản tự viết hay thiếu, khiến người dùng bàn phím bị
 * kẹt tab ra ngoài modal.
 *
 * Khi hai modal cùng mở (form → hộp xác nhận), chỉ lớp trên cùng nhận tương tác — xem
 * {@link dangMo}.
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
  rong?: 'sm' | 'md' | 'lg' | 'xl'
}) {
  const ref = React.useRef<HTMLDialogElement>(null)

  React.useEffect(() => {
    const d = ref.current
    if (!d) return

    if (mo && !d.open) {
      d.showModal()
      dangMo.push(d)
      capNhatLopTrenCung()
    } else if (!mo && d.open) {
      d.close()
      const i = dangMo.indexOf(d)
      if (i !== -1) dangMo.splice(i, 1)
      capNhatLopTrenCung()
    }
  }, [mo])

  // Modal bị unmount khi đang mở (đổi route, cha ngừng render) không chạy nhánh `!mo` ở trên
  // → sẽ nằm lại trong `dangMo` mãi và khoá `inert` vĩnh viễn lên modal mở sau đó.
  React.useEffect(() => {
    const d = ref.current
    return () => {
      if (!d) return
      const i = dangMo.indexOf(d)
      if (i === -1) return
      dangMo.splice(i, 1)
      capNhatLopTrenCung()
    }
  }, [])

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
      /*
        KHÔNG đóng khi bấm ra nền (bỏ 08/09/2026 theo yêu cầu chủ sản phẩm).
        Chỉ đóng bằng nút ✕ hoặc Esc.

        Vì sao: form ở đây thường dài (đăng ký khoá học, hồ sơ nhân sự, mua hàng) và người dùng
        hay bấm ra ngoài để bỏ focus một ô hoặc đóng khung select đang mở — mất toàn bộ dữ liệu
        đang nhập mà không có cảnh báo nào. Rủi ro đó lớn hơn tiện lợi của một cú bấm.

        `chanDoiKhiXuLy` vẫn giữ nguyên tác dụng với ✕ và Esc.
      */
      className={cn(
        'w-[calc(100vw-2rem)] rounded-lg border border-border bg-card p-0 text-card-foreground shadow-lg',
        'backdrop:bg-black/40 backdrop:backdrop-blur-[1px]',
        // Lớp dưới mờ đi khi có modal mở trên nó (xem `capNhatLopTrenCung`).
        'transition-opacity duration-150',
        // Cuộn ở cấp dialog, không ở thân — xem chú thích bên dưới.
        'max-h-[calc(100dvh-4rem)] overflow-y-auto',
        rong === 'sm' && 'max-w-sm',
        rong === 'md' && 'max-w-xl',
        rong === 'lg' && 'max-w-3xl',
        // xl cho bảng nhiều cột (điểm danh, chấm điểm, ma trận phân quyền) cần bề ngang thật,
        // nhét vào 3xl thì sân bé tới mức không kéo nổi áo.
        rong === 'xl' && 'max-w-6xl',
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

      {/*
        KHÔNG dùng overflow-y-auto ở đây: nó tạo scroll container, khiến dropdown của
        SelectTimKiem (position: absolute) bị cắt và phải cuộn mới thấy. Thay vào đó giới hạn
        chiều cao chính dialog và cho cả dialog cuộn — dropdown vẫn nổi lên trên bình thường.
      */}
      <div className="p-4">{children}</div>
    </dialog>
  )
}

/** Hàng nút ở chân modal — luôn phải/trái nhất quán giữa các màn. */
export function ModalChan({ children }: { children: React.ReactNode }) {
  return (
    <div className="mt-4 flex justify-end gap-2 border-t border-border pt-4">{children}</div>
  )
}
