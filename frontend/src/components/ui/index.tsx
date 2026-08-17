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
 * Ô nhập nhiều dòng cho **mọi trường có thể dài**: nhận xét, ghi chú, mô tả, ghi chú chiến thuật.
 *
 * `<input>` một dòng cắt nội dung khỏi tầm nhìn ngay khi vượt bề rộng ô — người dùng gõ một
 * đoạn nhận xét rồi không đọc lại được đoạn đầu, phải rê con trỏ mới thấy. Trường dài dùng
 * `<textarea>`, trường ngắn (tên, số áo, URL) vẫn dùng `Input`.
 *
 * Mặc định 3 dòng và `resize-y`: đủ cho ghi chú thường gặp, người cần dài hơn thì tự kéo.
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
      className,
    )}
    {...props}
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
export function Table({ className, ...props }: React.TableHTMLAttributes<HTMLTableElement>) {
  return (
    <div className="w-full overflow-x-auto rounded-lg border border-border">
      <table className={cn('w-full caption-bottom text-sm', className)} {...props} />
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
  variant?: 'muted' | 'win' | 'lose' | 'draw' | 'accent'
}) {
  return (
    <span
      className={cn(
        'inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium',
        variant === 'muted' && 'bg-muted text-muted-foreground',
        // Màu trạng thái: nghĩa cố định, không dùng để trang trí.
        variant === 'win' && 'bg-status-win/15 text-status-win',
        variant === 'lose' && 'bg-status-lose/15 text-status-lose',
        variant === 'draw' && 'bg-status-draw/15 text-status-draw',
        variant === 'accent' && 'bg-accent/15 text-accent',
        className,
      )}
      {...props}
    />
  )
}

/** Empty-state luôn kèm hành động + hướng dẫn ngắn (nguyên tắc UI/UX). */
export function TrangTrong({
  thongDiep,
  hanhDong,
}: {
  thongDiep: string
  hanhDong?: React.ReactNode
}) {
  return (
    <div className="flex flex-col items-center gap-3 rounded-lg border border-dashed border-border py-12 text-center">
      <p className="max-w-sm text-sm text-muted-foreground">{thongDiep}</p>
      {hanhDong}
    </div>
  )
}
