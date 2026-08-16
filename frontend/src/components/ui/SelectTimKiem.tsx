import * as React from 'react'
import { Check, ChevronsUpDown, X } from 'lucide-react'
import { cn } from '@/lib/utils'

export interface LuaChon {
  giaTri: string
  nhan: string
  /** Dòng phụ hiển thị mờ bên dưới nhãn (vd: mã đội, email). */
  phu?: string
}

/**
 * Select có ô tìm kiếm (combobox) — thay cho `<select>` cơ bản.
 *
 * Lý do: danh sách cầu thủ và nhóm quyền của một CLB có thể lên vài chục mục. Với `<select>`
 * người dùng phải cuộn tay tìm; ở đây gõ vài ký tự là lọc.
 *
 * Tìm kiếm bỏ dấu tiếng Việt: gõ "nguyen" vẫn ra "Nguyễn" — người dùng thường không bỏ dấu
 * khi tìm nhanh.
 */
export function SelectTimKiem({
  luaChon,
  giaTri,
  onDoi,
  placeholder = 'Chọn…',
  placeholderTimKiem = 'Gõ để tìm…',
  choPhepXoa = true,
  id,
  disabled,
}: {
  luaChon: LuaChon[]
  giaTri: string | null
  onDoi: (giaTri: string | null) => void
  placeholder?: string
  placeholderTimKiem?: string
  choPhepXoa?: boolean
  id?: string
  disabled?: boolean
}) {
  const [mo, setMo] = React.useState(false)
  const [tuKhoa, setTuKhoa] = React.useState('')
  const [chiSoHighlight, setChiSoHighlight] = React.useState(0)
  const boc = React.useRef<HTMLDivElement>(null)
  const oNhap = React.useRef<HTMLInputElement>(null)

  const dangChon = luaChon.find((l) => l.giaTri === giaTri) ?? null

  const daLoc = React.useMemo(() => {
    const tu = boDau(tuKhoa)
    if (!tu) return luaChon
    return luaChon.filter(
      (l) => boDau(l.nhan).includes(tu) || (l.phu ? boDau(l.phu).includes(tu) : false),
    )
  }, [luaChon, tuKhoa])

  // Đóng khi bấm ra ngoài.
  React.useEffect(() => {
    if (!mo) return
    const xuLy = (e: MouseEvent) => {
      if (boc.current && !boc.current.contains(e.target as Node)) setMo(false)
    }
    document.addEventListener('mousedown', xuLy)
    return () => document.removeEventListener('mousedown', xuLy)
  }, [mo])

  React.useEffect(() => {
    if (mo) {
      setTuKhoa('')
      setChiSoHighlight(0)
      // Đợi danh sách render xong mới focus, nếu không ô nhập chưa tồn tại.
      requestAnimationFrame(() => oNhap.current?.focus())
    }
  }, [mo])

  const chon = (v: string) => {
    onDoi(v)
    setMo(false)
  }

  const banPhim = (e: React.KeyboardEvent) => {
    if (e.key === 'ArrowDown') {
      e.preventDefault()
      setChiSoHighlight((i) => Math.min(i + 1, daLoc.length - 1))
    } else if (e.key === 'ArrowUp') {
      e.preventDefault()
      setChiSoHighlight((i) => Math.max(i - 1, 0))
    } else if (e.key === 'Enter') {
      e.preventDefault()
      const muc = daLoc[chiSoHighlight]
      if (muc) chon(muc.giaTri)
    } else if (e.key === 'Escape') {
      e.preventDefault()
      setMo(false)
    }
  }

  return (
    <div ref={boc} className="relative">
      <button
        id={id}
        type="button"
        disabled={disabled}
        onClick={() => setMo((v) => !v)}
        className={cn(
          'flex h-9 w-full items-center justify-between gap-2 rounded-md border border-input',
          'bg-background px-3 text-sm',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
          'disabled:cursor-not-allowed disabled:opacity-50',
        )}
        aria-haspopup="listbox"
        aria-expanded={mo}
      >
        <span className={cn('truncate', !dangChon && 'text-muted-foreground')}>
          {dangChon?.nhan ?? placeholder}
        </span>
        <span className="flex shrink-0 items-center gap-1">
          {choPhepXoa && dangChon && !disabled && (
            <span
              role="button"
              tabIndex={-1}
              aria-label="Bỏ chọn"
              onClick={(e) => {
                e.stopPropagation()
                onDoi(null)
              }}
              className="rounded p-0.5 hover:bg-muted"
            >
              <X className="h-3.5 w-3.5 text-muted-foreground" />
            </span>
          )}
          <ChevronsUpDown className="h-4 w-4 text-muted-foreground" />
        </span>
      </button>

      {mo && (
        <div className="absolute z-50 mt-1 w-full rounded-md border border-border bg-card shadow-lg">
          <div className="border-b border-border p-1.5">
            <input
              ref={oNhap}
              value={tuKhoa}
              onChange={(e) => {
                setTuKhoa(e.target.value)
                setChiSoHighlight(0)
              }}
              onKeyDown={banPhim}
              placeholder={placeholderTimKiem}
              className="h-8 w-full rounded bg-transparent px-2 text-sm outline-none placeholder:text-muted-foreground"
            />
          </div>

          <ul role="listbox" className="max-h-56 overflow-y-auto p-1">
            {daLoc.length === 0 ? (
              <li className="px-2 py-3 text-center text-sm text-muted-foreground">
                Không tìm thấy
              </li>
            ) : (
              daLoc.map((l, i) => (
                <li key={l.giaTri}>
                  <button
                    type="button"
                    role="option"
                    aria-selected={l.giaTri === giaTri}
                    onMouseEnter={() => setChiSoHighlight(i)}
                    onClick={() => chon(l.giaTri)}
                    className={cn(
                      'flex w-full items-center justify-between gap-2 rounded px-2 py-1.5 text-left text-sm',
                      i === chiSoHighlight && 'bg-muted',
                    )}
                  >
                    <span className="min-w-0">
                      <span className="block truncate">{l.nhan}</span>
                      {l.phu && (
                        <span className="block truncate text-xs text-muted-foreground">
                          {l.phu}
                        </span>
                      )}
                    </span>
                    {l.giaTri === giaTri && <Check className="h-4 w-4 shrink-0 text-primary" />}
                  </button>
                </li>
              ))
            )}
          </ul>
        </div>
      )}
    </div>
  )
}

/**
 * Bỏ dấu tiếng Việt để tìm kiếm không phụ thuộc dấu: gõ "nguyen" ra "Nguyễn".
 *
 * `̀-ͯ` là dải dấu tổ hợp mà NFD tách ra. Viết bằng escape thay vì dán ký tự
 * tổ hợp trực tiếp — ký tự đó vô hình trong editor và dễ mất khi copy qua công cụ.
 * `đ/Đ` phải xử lý riêng vì NFD không tách nó thành d + dấu.
 */
function boDau(s: string) {
  return s
    .toLowerCase()
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/đ/g, 'd')
}
