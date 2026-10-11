import * as React from 'react'
import { createPortal } from 'react-dom'
import { MoreHorizontal } from 'lucide-react'
import { cn } from '@/lib/utils'

export interface MucThaoTac {
  nhan: string
  onChon: () => void
  icon?: React.ComponentType<{ className?: string }>
  /** Tô đỏ — dành cho xoá, huỷ, gỡ. */
  nguyHiem?: boolean
  /** Chèn đường kẻ NGAY TRÊN mục này, tách nhóm phá huỷ khỏi nhóm thường. */
  ngatNhom?: boolean
  an?: boolean
  /**
   * Hiện nhưng KHOÁ, kèm lý do (11/10/2026).
   *
   * Khác `an`: ẩn hẳn thì người dùng không biết chức năng tồn tại, và tự hỏi vì sao khoá này
   * xoá được còn khoá kia thì không. Hiện kèm lý do ("đã có 40 đơn hàng") trả lời luôn câu
   * hỏi đó, và chỉ đường sang việc nên làm thay thế.
   */
  khoa?: string
}

/**
 * Menu thao tác cho một dòng bảng.
 *
 * Vì sao gom vào menu thay vì bày hết nút ra: bảng lớp học có 6 nút mỗi dòng, chiếm gần nửa
 * bề ngang và biến cột thao tác thành một dải icon khó phân biệt. Menu giữ cột hẹp, và **có
 * chữ** — icon trần buộc người dùng rê chuột từng cái để đoán.
 *
 * Không dùng `<select>`: nó không cho icon, không cho màu cảnh báo, và trên macOS mở ra
 * dạng danh sách hệ điều hành trông lạc lõng giữa bảng.
 */
export function MenuThaoTac({
  muc,
  nhanMo = 'Thao tác',
}: {
  muc: MucThaoTac[]
  nhanMo?: string
}) {
  const [mo, setMo] = React.useState(false)
  const [viTri, setViTri] = React.useState({ top: 0, right: 0 })
  const boc = React.useRef<HTMLDivElement>(null)
  /** Menu nằm ngoài `boc` vì dựng qua portal — cần ref riêng để biết "bấm trong menu". */
  const menu = React.useRef<HTMLDivElement>(null)
  const nut = React.useRef<HTMLButtonElement>(null)

  const hienThi = muc.filter((m) => !m.an)

  // Đóng khi bấm ra ngoài, nhấn Escape, hoặc cuộn/đổi kích thước.
  //
  // Cuộn PHẢI đóng menu: nó định vị `fixed` theo toạ độ chụp lúc mở, cuộn mà không đóng thì
  // menu đứng yên còn dòng của nó trôi đi.
  React.useEffect(() => {
    if (!mo) return
    const bamNgoai = (e: MouseEvent) => {
      // Phải kiểm CẢ hai: từ 11/10/2026 menu dựng ở `document.body` qua portal nên nó KHÔNG
      // còn nằm trong `boc` — chỉ kiểm `boc` thì bấm vào chính mục menu cũng tính là "bấm
      // ngoài", và menu đóng ngay ở `mousedown` trước khi `click` kịp chạy.
      const trong =
        boc.current?.contains(e.target as Node) || menu.current?.contains(e.target as Node)
      if (!trong) setMo(false)
    }
    const phim = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        setMo(false)
        nut.current?.focus()
      }
    }
    const dong = () => setMo(false)
    document.addEventListener('mousedown', bamNgoai)
    document.addEventListener('keydown', phim)
    // `true` để bắt cả cuộn bên trong khung con, không chỉ cuộn trang.
    window.addEventListener('scroll', dong, true)
    window.addEventListener('resize', dong)
    return () => {
      document.removeEventListener('mousedown', bamNgoai)
      document.removeEventListener('keydown', phim)
      window.removeEventListener('scroll', dong, true)
      window.removeEventListener('resize', dong)
    }
  }, [mo])

  /**
   * Tính toạ độ tuyệt đối cho menu.
   *
   * Dùng `position: fixed` chứ không `absolute`: `Table` bọc trong `overflow-x-auto`, nên menu
   * `absolute` bị KHUNG CUỘN CẮT MẤT ở dòng cuối — chính chỗ hay bấm nhất vì bản ghi mới
   * thường nằm dưới cùng.
   *
   * Mở lên trên khi gần đáy màn hình, cùng lý do.
   */
  const tinhViTri = () => {
    const r = nut.current?.getBoundingClientRect()
    if (!r) return
    // +28 cho mỗi dòng lý do của mục bị khoá, nếu không menu bị tính thiếu chiều cao và
    // lật hướng sai khi ở gần đáy màn.
    const cao = hienThi.length * 34 + hienThi.filter((m) => m.khoa).length * 28 + 8
    const canDuoi = window.innerHeight - r.bottom
    setViTri({
      top: canDuoi < cao + 16 ? r.top - cao - 4 : r.bottom + 4,
      right: window.innerWidth - r.right,
    })
  }

  if (hienThi.length === 0) return null

  return (
    <div ref={boc} className="relative inline-block text-left">
      <button
        ref={nut}
        type="button"
        aria-label={nhanMo}
        aria-haspopup="menu"
        aria-expanded={mo}
        onClick={() => {
          tinhViTri()
          setMo((v) => !v)
        }}
        className={cn(
          'inline-flex h-8 w-8 items-center justify-center rounded-md transition-colors',
          'hover:bg-muted focus-visible:outline-none focus-visible:ring-2',
          'focus-visible:ring-ring focus-visible:ring-offset-2',
          mo && 'bg-muted',
        )}
      >
        <MoreHorizontal className="h-4 w-4" />
      </button>

      {/*
        Dựng menu ở `document.body` qua portal, KHÔNG để trong ô bảng (11/10/2026).

        Cột thao tác dùng `position: sticky` để dính khi cuộn ngang. Mà `sticky` tạo ra một
        **stacking context** mới: `z-50` của menu chỉ so với anh em trong chính ô đó, không
        so được với các dòng khác của bảng. Hệ quả: menu mở ra bị các dòng bên dưới đè lên —
        chủ sản phẩm báo 11/10/2026.

        `fixed` không cứu được: nó định vị theo viewport nhưng vẫn nằm TRONG stacking context
        của ô. Chỉ có đưa hẳn ra ngoài cây DOM của bảng mới thoát.

        Toạ độ đã tính sẵn theo viewport (`viTri`) nên chuyển chỗ dựng không đổi vị trí hiện.
      */}
      {mo && createPortal(
        <div
          ref={menu}
          role="menu"
          style={{ top: viTri.top, right: viTri.right }}
          className={cn(
            'fixed z-50 min-w-44 rounded-md border border-border bg-background p-1 shadow-lg',
          )}
        >
          {hienThi.map((m, i) => (
            <React.Fragment key={m.nhan}>
              {m.ngatNhom && i > 0 && <div className="my-1 h-px bg-border" />}
              <button
                type="button"
                role="menuitem"
                disabled={!!m.khoa}
                onClick={() => {
                  setMo(false)
                  m.onChon()
                }}
                className={cn(
                  'flex w-full items-center gap-2 rounded-sm px-2 py-1.5 text-left text-sm',
                  'transition-colors focus-visible:outline-none',
                  m.khoa
                    ? 'cursor-not-allowed opacity-50'
                    : 'hover:bg-muted focus-visible:bg-muted',
                  m.nguyHiem && !m.khoa && 'text-destructive',
                )}
              >
                {m.icon && <m.icon className="h-4 w-4 shrink-0" />}
                <span className="truncate">{m.nhan}</span>
              </button>
              {/* Lý do nằm NGAY DƯỚI mục bị khoá, không phải tooltip: tooltip chỉ hiện khi
                  rê chuột, mà nút đã `disabled` thì nhiều trình duyệt không bắn sự kiện rê. */}
              {m.khoa && (
                <p className="px-2 pb-1 text-xs leading-snug text-muted-foreground">{m.khoa}</p>
              )}
            </React.Fragment>
          ))}
        </div>,
        document.body,
      )}
    </div>
  )
}
