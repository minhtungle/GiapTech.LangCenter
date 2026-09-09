import * as React from 'react'
import { cn } from '@/lib/utils'

/*
  Bộ component tối giản theo phong cách shadcn/ui: đọc design token từ CSS variable
  (xem src/index.css), không hard-code màu. Mật độ thông tin cao, ít trang trí —
  theo docs/frontend/ui-ux-nguyen-tac.md.
*/

export const Button = React.forwardRef<
  HTMLButtonElement,
  React.ButtonHTMLAttributes<HTMLButtonElement> & {
    variant?: 'primary' | 'outline' | 'ghost' | 'destructive'
    size?: 'sm' | 'md'
  }
>(({ className, variant = 'primary', size = 'md', ...props }, ref) => (
  <button
    ref={ref}
    className={cn(
      'inline-flex items-center justify-center gap-2 rounded-md font-medium transition-colors',
      'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2',
      'disabled:pointer-events-none disabled:opacity-50',
      size === 'sm' ? 'h-8 px-3 text-xs' : 'h-9 px-4 text-sm',
      variant === 'primary' && 'bg-primary text-primary-foreground hover:bg-primary/90',
      variant === 'outline' && 'border border-input bg-background hover:bg-muted',
      variant === 'ghost' && 'hover:bg-muted',
      variant === 'destructive' &&
        'bg-destructive text-destructive-foreground hover:bg-destructive/90',
      className,
    )}
    {...props}
  />
))
Button.displayName = 'Button'

export const Input = React.forwardRef<
  HTMLInputElement,
  React.InputHTMLAttributes<HTMLInputElement>
>(({ className, ...props }, ref) => (
  <input
    ref={ref}
    className={cn(
      'flex h-9 w-full rounded-md border border-input bg-background px-3 py-1 text-sm',
      'placeholder:text-muted-foreground',
      'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
      'disabled:cursor-not-allowed disabled:opacity-50',
      className,
    )}
    {...props}
  />
))
Input.displayName = 'Input'

/**
 * Ô nhập nhiều dòng cho **mọi trường có thể dài**: nhận xét buổi học, ghi chú, mô tả bài tập.
 *
 * `<input>` một dòng cắt nội dung khỏi tầm nhìn ngay khi vượt bề rộng ô — người dùng gõ một
 * đoạn nhận xét rồi không đọc lại được đoạn đầu, phải rê con trỏ mới thấy. Trường dài dùng
 * `<textarea>`, trường ngắn (tên, số áo, URL) vẫn dùng `Input`.
 *
 * Mặc định 3 dòng và `resize-y`: đủ cho ghi chú thường gặp, người cần dài hơn thì tự kéo —
 * nhưng **có trần**. Không giới hạn thì tay cầm resize kéo được vô hạn: đo thật 09/09/2026 cho
 * ra ô cao 3082px trong modal cao 836px, và nút Lưu bị đẩy xuống dưới 3667px scroll. Trần
 * `18rem` ≈ 12 dòng — quá đó thì nội dung tự cuộn TRONG ô, không đẩy form dài ra.
 *
 * Sàn dùng `field-sizing` không được (Safari chưa hỗ trợ), nên đặt `min-height` theo **chính
 * `rows` của ô đó**: `rows={4}` không bị kéo bóp xuống còn 2 dòng. Một trần/sàn cứng dùng chung
 * sẽ sai với ô khai `rows` khác mặc định — đo thật cho thấy ô `rows={4}` (98px) bị bóp còn 64px.
 */
export const Textarea = React.forwardRef<
  HTMLTextAreaElement,
  React.TextareaHTMLAttributes<HTMLTextAreaElement>
>(({ className, rows = 3, ...props }, ref) => (
  <textarea
    ref={ref}
    rows={rows}
    className={cn(
      'flex w-full rounded-md border border-input bg-background px-3 py-2 text-sm',
      'placeholder:text-muted-foreground',
      'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
      'disabled:cursor-not-allowed disabled:opacity-50',
      // resize-y thôi: kéo ngang sẽ phá vỡ lưới cột của form.
      'resize-y',
      // Trần cho tay cầm resize — xem doc comment ở trên.
      'max-h-72',
      className,
    )}
    {...props}
    // Sàn = đúng chiều cao `rows` đã khai. Tính bằng em để theo cỡ chữ: mỗi dòng ~1.5em cộng
    // padding dọc (py-2 = 1rem). Đặt qua style vì Tailwind không có class động theo prop.
    //
    // Đứng SAU `{...props}` và trải lại `props.style`: đặt trước thì caller truyền `style` sẽ
    // ghi đè cả object và mất sàn — lỗi im lặng, không có cảnh báo biên dịch.
    style={{ minHeight: `calc(${rows} * 1.5em + 1rem)`, ...props.style }}
  />
))
Textarea.displayName = 'Textarea'

export function Label({ className, ...props }: React.LabelHTMLAttributes<HTMLLabelElement>) {
  return (
    <label className={cn('text-sm font-medium leading-none', className)} {...props} />
  )
}

export function Card({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={cn('rounded-lg border border-border bg-card text-card-foreground', className)}
      {...props}
    />
  )
}

export function CardHeader({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return <div className={cn('flex flex-col gap-1 p-5 pb-3', className)} {...props} />
}

export function CardTitle({ className, ...props }: React.HTMLAttributes<HTMLHeadingElement>) {
  return <h3 className={cn('text-base font-semibold', className)} {...props} />
}

export function CardDescription({ className, ...props }: React.HTMLAttributes<HTMLParagraphElement>) {
  return <p className={cn('text-sm text-muted-foreground', className)} {...props} />
}

export function CardContent({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return <div className={cn('p-5 pt-0', className)} {...props} />
}

/** Thông báo lỗi dạng inline — không dùng alert() chặn luồng. */
export function CanhBaoLoi({ children }: { children: React.ReactNode }) {
  if (!children) return null
  return (
    <div
      role="alert"
      className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
    >
      {children}
    </div>
  )
}

/** Bảng mật độ dòng gọn (compact density) theo nguyên tắc UI/UX. */
/**
 * Bảng dữ liệu.
 *
 * `caoToiDa` giới hạn chiều cao và cho bảng tự cuộn, kèm **header dính**: bảng 14–20 dòng làm
 * trang cao gấp đôi màn hình, người dùng cuộn cả trang và mất luôn tiêu đề cột nên không biết
 * cột nào là gì. Đo 21/08: tab Đăng ký trang cao 1060px với viewport 800px, bảng chiếm 550px cho
 * 14 dòng mà phần lớn ô là dấu "—".
 *
 * Không đặt mặc định cho MỌI bảng: bảng 3–5 dòng mà có khung cuộn riêng trông như bị hỏng, và
 * bảng ngắn thì cuộn trang là hành vi đúng.
 */
export function Table({
  className,
  caoToiDa,
  ...props
}: React.TableHTMLAttributes<HTMLTableElement> & {
  /** Lớp Tailwind giới hạn chiều cao, ví dụ `max-h-[22rem]`. Bỏ trống = không giới hạn. */
  caoToiDa?: string
}) {
  return (
    <div
      className={cn(
        'w-full overflow-x-auto rounded-lg border border-border',
        caoToiDa && `overflow-y-auto ${caoToiDa}`,
      )}
    >
      <table
        className={cn(
          'w-full caption-bottom text-sm',
          // Header dính chỉ có nghĩa khi khung cuộn được.
          caoToiDa && '[&_thead_th]:sticky [&_thead_th]:top-0 [&_thead_th]:z-10',
          className,
        )}
        {...props}
      />
    </div>
  )
}

export function Th({ className, ...props }: React.ThHTMLAttributes<HTMLTableCellElement>) {
  return (
    <th
      className={cn(
        'h-9 border-b border-border bg-muted/50 px-3 text-left align-middle text-xs font-semibold text-muted-foreground',
        className,
      )}
      {...props}
    />
  )
}

export function Td({ className, ...props }: React.TdHTMLAttributes<HTMLTableCellElement>) {
  return <td className={cn('border-b border-border px-3 py-2 align-middle', className)} {...props} />
}

export function Badge({
  className,
  variant = 'muted',
  ...props
}: React.HTMLAttributes<HTMLSpanElement> & {
  /**
   * Màu trạng thái — **nghĩa cố định, không dùng để trang trí**:
   *
   * - `ok` — xong · đạt · đủ (lớp đang học, buổi đã chốt, học phí đã đủ, thao tác thành công)
   * - `loi` — hỏng · quá hạn · bị từ chối (lớp đã huỷ, nộp muộn, học phí quá hạn)
   * - `cho` — đang chờ · cần chú ý (lớp nháp, còn nợ, buộc đổi mật khẩu)
   *
   * Ba tên này đổi từ `win`/`lose`/`draw` (08/09/2026, nợ N8) — di sản của dự án tiền thân,
   * đọc lên gây hiểu sai vì không có "thắng/thua" nào trong nghiệp vụ LMS.
   */
  variant?: 'muted' | 'ok' | 'loi' | 'cho' | 'accent'
}) {
  return (
    <span
      className={cn(
        'inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium',
        variant === 'muted' && 'bg-muted text-muted-foreground',
        // Màu trạng thái: nghĩa cố định, không dùng để trang trí.
        variant === 'ok' && 'bg-status-ok/15 text-status-ok',
        variant === 'loi' && 'bg-status-loi/15 text-status-loi',
        variant === 'cho' && 'bg-status-cho/15 text-status-cho',
        variant === 'accent' && 'bg-accent/15 text-accent',
        className,
      )}
      {...props}
    />
  )
}

/** Empty-state luôn kèm hành động + hướng dẫn ngắn (nguyên tắc UI/UX). */
/**
 * Trạng thái rỗng.
 *
 * `py-6` chứ không `py-12`: đo 21/08 thì khối "Chưa có lời mời nào" chiếm 150px chỉ để nói một
 * câu, trong khi đây là trạng thái RỖNG — nó không nên chiếm chỗ hơn nội dung thật. Có `hanhDong`
 * (nút) thì nới ra một chút để nút không sát viền.
 */
export function TrangTrong({
  thongDiep,
  hanhDong,
}: {
  thongDiep: string
  hanhDong?: React.ReactNode
}) {
  return (
    <div
      className={cn(
        'flex flex-col items-center gap-3 rounded-lg border border-dashed border-border text-center',
        hanhDong ? 'py-8' : 'py-6',
      )}
    >
      <p className="max-w-sm text-sm text-muted-foreground">{thongDiep}</p>
      {hanhDong}
    </div>
  )
}
