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

  /*
    Đếm số lần MỞ, dùng làm `key` cho phần nội dung (11/10/2026).

    ## Lỗi nó sửa

    `<dialog>` không bị unmount khi đóng — nó chỉ đổi thuộc tính `open`. Mà React chỉ đọc
    `defaultValue` ở lần dựng ĐẦU TIÊN, nên mọi ô uncontrolled giữ nguyên giá trị của lần mở
    trước: mở sửa khoá A rồi sửa khoá B thì ô giá tiền, số buổi… vẫn là của A.

    Chủ sản phẩm báo 11/10/2026 với màn Khoá học (danh sách ghi 7.275.000đ/15 buổi, modal hiện
    0đ/40 buổi — số của lần nhập trước). Đếm ra 15+ màn, 70+ ô `defaultValue` cùng dính.

    ## Vì sao sửa ở ĐÂY

    Đổi `key` là ép React dựng lại cây con ⇒ mọi `defaultValue` đọc lại. Một chỗ sửa, mọi
    modal hưởng, và form viết sau này tự đúng.

    Cách còn lại là đổi 70+ ô sang controlled: dài, dễ sót, và mỗi lần thêm ô mới lại phải
    nhớ. Hoặc gắn `key` ở từng nơi gọi `<Modal>` — cũng là 20+ chỗ phải nhớ.
  */
  const [lanMo, setLanMo] = React.useState(0)

  React.useEffect(() => {
    if (mo) setLanMo((n) => n + 1)
  }, [mo])

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
        /*
          Khung ngoài KHÔNG cuộn: `flex flex-col` + `overflow-hidden` để thân cuộn riêng,
          nhờ đó tiêu đề và hàng nút ĐỨNG YÊN khi form dài (11/10/2026).

          Trước đó cả dialog cuộn nên tiêu đề trôi mất, và nút Lưu phải cuộn xuống đáy mới
          bấm được — chủ sản phẩm báo 11/10/2026.
        */
        'flex max-h-[calc(100dvh-4rem)] flex-col overflow-hidden',
        rong === 'sm' && 'max-w-sm',
        rong === 'md' && 'max-w-xl',
        rong === 'lg' && 'max-w-3xl',
        // xl cho bảng nhiều cột (điểm danh, chấm điểm, ma trận phân quyền) cần bề ngang thật,
        // nhét vào 3xl thì sân bé tới mức không kéo nổi áo.
        rong === 'xl' && 'max-w-6xl',
      )}
    >
      {/* `shrink-0`: trong flex cột, phần tử mặc định CO LẠI khi chỗ không đủ — tiêu đề sẽ
          bị bóp dẹt thay vì thân cuộn. */}
      <div className="flex shrink-0 items-start justify-between gap-4 border-b border-border p-4">
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
        Thân là vùng CUỘN (đổi 11/10/2026 — trước đó cuộn ở cấp dialog).

        Chú thích cũ ở đây cảnh báo `overflow-y-auto` sẽ cắt dropdown của `SelectTimKiem` vì
        nó dùng `position: absolute`. Cảnh báo đó **không còn đúng**: `SelectTimKiem` và
        `MenuThaoTac` nay đều dựng khung thả xuống bằng `position: fixed` với toạ độ viewport,
        nên không bị khung cuộn nào cắt. Đã kiểm lại trước khi đổi.
      */}
      {/* `key` đổi mỗi lần mở ⇒ nội dung dựng lại ⇒ `defaultValue` đọc lại. Xem `lanMo`. */}
      {/*
        Khoảng cách CHUẨN cho mọi modal (11/10/2026).

        Chủ sản phẩm báo "nhiều modal bố trí chưa cân đối". Đếm ra 77 khung lưới trong các
        modal dùng 6 mức gap khác nhau (gap-1.5, gap-2, gap-3, gap-4…), nên mỗi modal thưa
        dày một kiểu.

        `[&>form]:gap-4` + `[&>.grid]:gap-4` đặt một mức duy nhất ở ĐÂY thay vì sửa 77 chỗ:
        một chỗ sửa, mọi modal đều theo, và form viết sau này tự đúng. Màn nào thật sự cần
        khác thì vẫn ghi đè được vì class của chính nó cụ thể hơn.

        `gap-4` chứ không `gap-3`: nhãn + ô nhập cao 56px, khe 12px làm hai trường dính vào
        nhau tới mức khó nhìn ra đâu là nhãn của ô nào.
      */}
      <div
        key={lanMo}
        className="flex-1 overflow-y-auto p-4 [&>.grid]:gap-4 [&>form]:gap-4"
      >
        {children}
      </div>
    </dialog>
  )
}

/** Hàng nút ở chân modal — luôn phải/trái nhất quán giữa các màn. */
export function ModalChan({ children }: { children: React.ReactNode }) {
  return (
    /*
      `-mx-4 px-4`: đường kẻ chạy HẾT bề ngang modal thay vì thụt vào theo lề nội dung —
      kẻ thụt trông như một gạch ngang lạc giữa form, không ra ranh giới (11/10/2026).

      `mt-2` chứ không `mt-4`: hàng nút nằm trong `grid gap-3/gap-4` của form nên đã có sẵn
      khoảng cách; cộng thêm mt-4 thành khe hở to hơn hẳn mọi khe khác trong modal.
    */
    /*
      `sticky bottom-0`: hàng nút nằm TRONG vùng cuộn (mỗi màn tự đặt `<ModalChan>` trong
      `children`, nên `Modal` không pin nó từ ngoài được). Sticky giữ nó dính đáy khung
      cuộn — form dài vẫn thấy nút Lưu mà không phải cuộn xuống đáy (11/10/2026).

      `bg-card` BẮT BUỘC: nền trong suốt thì nội dung cuộn qua sẽ lộ ra dưới hàng nút.

      `-mx-4 px-4` + `-mb-4 pb-4`: kéo ra sát mép khung cuộn để đường kẻ chạy hết bề ngang
      và không hở một dải nền ở đáy khi cuộn tới cuối.
    */
    <div
      className={cn(
        'sticky bottom-0 z-10 -mx-4 -mb-4 mt-2 flex justify-end gap-2',
        'border-t border-border bg-card px-4 pb-4 pt-4',
      )}
    >
      {children}
    </div>
  )
}
