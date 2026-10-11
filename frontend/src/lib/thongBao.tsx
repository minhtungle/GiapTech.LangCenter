import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react'
import { CheckCircle2, XCircle, X } from 'lucide-react'
import { cn } from '@/lib/utils'

/**
 * Thông báo nổi sau mỗi thao tác thành công (11/10/2026 — yêu cầu chủ sản phẩm).
 *
 * ## Vì sao cần
 *
 * Đo 11/10/2026: **58/65 lệnh ghi không báo gì khi xong**. Modal đóng lại, bảng tự nạp lại,
 * và người dùng phải tự suy ra là đã lưu được. Với thao tác không đổi gì thấy được trên màn
 * (gửi mail, gửi yêu cầu xếp lớp) thì không có cách nào biết.
 *
 * ## Vì sao góc dưới bên PHẢI, không phải giữa màn
 *
 * Giữa màn chặn mất nội dung vừa thay đổi — thứ người dùng đang muốn nhìn. Góc dưới phải nằm
 * ngoài luồng đọc (trái→phải, trên→xuống) nên không che gì, mà vẫn trong tầm mắt ngoại vi.
 *
 * ## Vì sao KHÔNG dùng cho lỗi của biểu mẫu
 *
 * Lỗi phải nằm **cạnh ô gây lỗi** để người dùng biết sửa ở đâu; `CanhBaoLoi` trong form làm
 * việc đó. Toast lỗi chỉ dùng cho lệnh chạy NGOÀI form (xoá từ menu, thao tác trên bảng) —
 * ở đó không có ô nào để bám vào.
 */

export type LoaiThongBao = 'ok' | 'loi'

interface MucThongBao {
  id: number
  loai: LoaiThongBao
  noiDung: string
}

interface ApiThongBao {
  /** Báo thao tác thành công. */
  ok: (noiDung: string) => void
  /** Báo lỗi cho lệnh chạy ngoài biểu mẫu. */
  loi: (noiDung: string) => void
}

const Ctx = createContext<ApiThongBao | null>(null)

/** Tự tắt sau bao lâu. Đủ đọc một câu ngắn, không đủ gây phiền. */
const THOI_GIAN_HIEN = 3500

/** Lỗi giữ lâu hơn: người dùng cần thời gian đọc và quyết định làm gì tiếp. */
const THOI_GIAN_HIEN_LOI = 6000

/**
 * Tối đa bao nhiêu cái cùng lúc.
 *
 * Xoá hàng loạt có thể bắn hàng chục lệnh; chồng hết lên nhau sẽ che kín màn hình. Giữ cái
 * mới nhất vì đó là thứ vừa xảy ra.
 */
const TOI_DA = 3

export function NhaCungCapThongBao({ children }: { children: React.ReactNode }) {
  const [ds, setDs] = useState<MucThongBao[]>([])
  const demRef = useRef(0)

  const them = useCallback((loai: LoaiThongBao, noiDung: string) => {
    const id = ++demRef.current
    setDs((cu) => [...cu, { id, loai, noiDung }].slice(-TOI_DA))
    const han = loai === 'loi' ? THOI_GIAN_HIEN_LOI : THOI_GIAN_HIEN
    setTimeout(() => setDs((cu) => cu.filter((m) => m.id !== id)), han)
  }, [])

  // `useMemo`: object mới mỗi render sẽ bắt MỌI component dùng hook này render lại theo.
  const api = useMemo<ApiThongBao>(
    () => ({
      ok: (noiDung) => them('ok', noiDung),
      loi: (noiDung) => them('loi', noiDung),
    }),
    [them],
  )

  // Đăng ký cho `MutationCache` dùng — xem `thongBaoNgoaiReact`.
  useEffect(() => {
    thongBaoToanCuc = api
    return () => {
      thongBaoToanCuc = KHONG_LAM_GI
    }
  }, [api])

  return (
    <Ctx.Provider value={api}>
      {children}
      <KhungThongBao ds={ds} onDong={(id) => setDs((cu) => cu.filter((m) => m.id !== id))} />
    </Ctx.Provider>
  )
}

function KhungThongBao({
  ds,
  onDong,
}: {
  ds: MucThongBao[]
  onDong: (id: number) => void
}) {
  if (ds.length === 0) return null

  return (
    <div
      /*
        `aria-live="polite"`: trình đọc màn hình đọc khi người dùng ngừng thao tác, không cắt
        ngang. `assertive` sẽ ngắt lời giữa câu — phiền hơn là hữu ích cho một lời xác nhận.

        `pointer-events-none` ở khung, bật lại ở từng thẻ: vùng trống quanh thông báo không
        được nuốt cú bấm vào nội dung bên dưới.
      */
      aria-live="polite"
      className="pointer-events-none fixed bottom-4 right-4 z-[110] flex w-full max-w-sm flex-col gap-2"
    >
      {ds.map((m) => (
        <TheThongBao key={m.id} muc={m} onDong={() => onDong(m.id)} />
      ))}
    </div>
  )
}

function TheThongBao({ muc, onDong }: { muc: MucThongBao; onDong: () => void }) {
  const [hien, setHien] = useState(false)

  // Hiện ở khung hình SAU khi gắn vào DOM, để trình duyệt kịp chạy transition. Đặt thẳng
  // `true` lúc dựng thì nó xuất hiện đột ngột, không có hiệu ứng trượt vào.
  useEffect(() => {
    const id = requestAnimationFrame(() => setHien(true))
    return () => cancelAnimationFrame(id)
  }, [])

  const laLoi = muc.loai === 'loi'
  const Icon = laLoi ? XCircle : CheckCircle2

  return (
    <div
      className={cn(
        'pointer-events-auto flex items-start gap-2.5 rounded-lg border bg-card p-3 shadow-lg',
        'transition-all duration-200',
        hien ? 'translate-y-0 opacity-100' : 'translate-y-2 opacity-0',
        laLoi ? 'border-status-loi/30' : 'border-status-ok/30',
      )}
    >
      <Icon
        className={cn('mt-0.5 h-4 w-4 shrink-0', laLoi ? 'text-status-loi' : 'text-status-ok')}
      />
      <p className="flex-1 text-sm">{muc.noiDung}</p>
      <button
        type="button"
        onClick={onDong}
        className="shrink-0 rounded p-0.5 text-muted-foreground hover:bg-muted hover:text-foreground"
        // Nhãn tiếng Anh cố định cho trình đọc màn hình: nút này không hiện chữ nào, và
        // thêm một khoá i18n cho nó là chi phí không đáng.
        aria-label="Close"
      >
        <X className="h-3.5 w-3.5" />
      </button>
    </div>
  )
}

/**
 * Lấy API báo thông báo.
 *
 * Gọi ngoài `NhaCungCapThongBao` thì trả về bản **không làm gì** thay vì ném lỗi: thiếu một
 * lời xác nhận là phiền, còn làm sập cả màn vì chuyện đó thì tệ hơn nhiều.
 */
export function useThongBao(): ApiThongBao {
  const ctx = useContext(Ctx)
  return ctx ?? KHONG_LAM_GI
}

const KHONG_LAM_GI: ApiThongBao = { ok: () => {}, loi: () => {} }

/*
  ---------------------------------------------------------------------------------------
  Cầu nối cho mã chạy NGOÀI React
  ---------------------------------------------------------------------------------------

  `QueryClient` là hằng ở cấp module (xem `App.tsx`) nên `MutationCache` của nó không gọi
  được hook. Mà đó lại là chỗ DUY NHẤT thấy được mọi lệnh ghi của cả app — nơi đúng để báo
  "đã lưu" một lần cho tất cả, thay vì sửa 58 chỗ `onSuccess` rải rác.

  Biến module thay vì context: nơi dùng nó không nằm trong cây React.
*/
let thongBaoToanCuc: ApiThongBao = KHONG_LAM_GI

/** Lấy API để dùng ngoài React. Trước khi provider gắn xong thì nó không làm gì. */
export function thongBaoNgoaiReact(): ApiThongBao {
  return thongBaoToanCuc
}

/**
 * Chọn KHOÁ i18n cho lời xác nhận, theo phương thức HTTP của lệnh vừa xong.
 *
 * Tách khỏi `App.tsx` để test được: đây là phép quyết định thật sự có thể sai (xoá mà báo
 * "Đã lưu"), còn phần dựng giao diện thì không.
 *
 * Trả KHOÁ chứ không trả câu đã dịch — nơi gọi mới biết ngôn ngữ đang dùng, và test không
 * phải dựng cả i18n.
 */
export function khoaNhanThanhCong(method: string | undefined): string {
  // `delete` là lệnh DUY NHẤT cần nhãn khác: post/put/patch đều là "lưu" dưới mắt người dùng.
  return method?.toLowerCase() === 'delete' ? 'chung.daXoa' : 'chung.daLuu'
}
