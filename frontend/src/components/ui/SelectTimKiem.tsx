import * as React from 'react'
import { Check, ChevronsUpDown, Plus, X } from 'lucide-react'
import { cn } from '@/lib/utils'

export interface LuaChon {
  giaTri: string
  nhan: string
  /** Dòng phụ hiển thị mờ bên dưới nhãn (vd: mã đội, email). */
  phu?: string
}

/** Chiều cao tối đa của khung thả xuống (ô tìm kiếm + danh sách), khớp `max-h-56` bên dưới. */
const CAO_TOI_DA = 264

/**
 * Định vị khung thả xuống bằng `position: fixed` thay vì `absolute`.
 *
 * **Vì sao cần**: `Modal` có `overflow-y-auto` để form dài cuộn được. Khung `absolute` nằm
 * TRONG luồng cuộn đó, nên mở select ở gần đáy form sẽ **kéo dài thân modal** và người dùng
 * phải cuộn xuống mới thấy danh sách — hoặc tệ hơn, danh sách bị khung cuộn cắt mất.
 *
 * `fixed` + toạ độ chụp lúc mở đưa khung ra khỏi mọi khung cuộn. Cùng cách `MenuThaoTac` đã
 * dùng để thoát `overflow-x-auto` của `Table` (07/09/2026).
 *
 * Đánh đổi: `fixed` không đi theo khi trang cuộn, nên **cuộn phải đóng** khung lại — nếu
 * không nó sẽ trôi lơ lửng giữa màn hình.
 */
function useViTriTha(mo: boolean, neo: React.RefObject<HTMLElement | null>) {
  const [viTri, setViTri] = React.useState<React.CSSProperties>({})

  const tinh = React.useCallback(() => {
    const r = neo.current?.getBoundingClientRect()
    if (!r) return

    // Mở LÊN TRÊN khi không đủ chỗ bên dưới — select cuối form là ca hay gặp nhất.
    const canDuoi = window.innerHeight - r.bottom
    const moLen = canDuoi < CAO_TOI_DA + 16 && r.top > canDuoi

    setViTri({
      position: 'fixed',
      left: r.left,
      width: r.width,
      // Giới hạn theo chỗ còn lại để khung không tràn khỏi viewport khi cả hai phía đều hẹp.
      maxHeight: Math.min(CAO_TOI_DA, (moLen ? r.top : canDuoi) - 12),
      ...(moLen ? { bottom: window.innerHeight - r.top + 4 } : { top: r.bottom + 4 }),
    })
  }, [neo])

  React.useEffect(() => {
    if (!mo) return
    tinh()

    // `capture: true` để bắt cuộn của MỌI khung bên trong (thân modal), không chỉ window.
    const dong = () => tinh()
    window.addEventListener('scroll', dong, true)
    window.addEventListener('resize', dong)
    return () => {
      window.removeEventListener('scroll', dong, true)
      window.removeEventListener('resize', dong)
    }
  }, [mo, tinh])

  return { viTri, tinh }
}

/**
 * Select có ô tìm kiếm (combobox) — thay cho `<select>` cơ bản.
 *
 * Lý do: danh sách nhóm quyền của một trung tâm có thể lên vài chục mục. Với `<select>`
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
  onTaoMoi,
  nhanTaoMoi,
  duoiDanhSach,
}: {
  luaChon: LuaChon[]
  giaTri: string | null
  onDoi: (giaTri: string | null) => void
  placeholder?: string
  placeholderTimKiem?: string
  choPhepXoa?: boolean
  id?: string
  disabled?: boolean
  /**
   * Bật khả năng tạo mục mới từ chính ô tìm kiếm: gõ tên không có trong danh sách thì hiện
   * dòng "Tạo …" ở cuối. Nhận từ khoá đang gõ, trả về giá trị của mục vừa tạo (hoặc null nếu
   * thất bại) — component tự chọn nó.
   *
   * Có nó thì thêm mục mới không phải rời form đang làm, sang màn quản lý, rồi quay lại.
   */
  onTaoMoi?: (ten: string) => Promise<string | null>
  /** Nhãn dòng tạo mới, `{ten}` được thay bằng từ khoá. Mặc định: `Tạo "{ten}"`. */
  nhanTaoMoi?: string
  /** Nội dung tuỳ ý chèn dưới danh sách — dùng cho ô tra cứu theo mã. */
  duoiDanhSach?: React.ReactNode
}) {
  const [mo, setMo] = React.useState(false)
  const [tuKhoa, setTuKhoa] = React.useState('')
  const [chiSoHighlight, setChiSoHighlight] = React.useState(0)
  const boc = React.useRef<HTMLDivElement>(null)
  const nut = React.useRef<HTMLButtonElement>(null)
  const oNhap = React.useRef<HTMLInputElement>(null)
  const { viTri, tinh } = useViTriTha(mo, nut)

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

  const [dangTao, setDangTao] = React.useState(false)

  const chon = (v: string) => {
    onDoi(v)
    setMo(false)
  }

  /**
   * Chỉ hiện dòng tạo mới khi từ khoá KHÔNG khớp chính xác mục nào đang có.
   *
   * Dùng so khớp chính xác (không phải "danh sách rỗng"): gõ "Ban A" mà đã có "Ban A1" thì
   * vẫn cho tạo "Ban A" — hai mục khác nhau. Nhưng gõ đúng "Ban A1" thì không, vì
   * đó chắc chắn là ý muốn chọn mục đã có.
   */
  const tuKhoaSach = tuKhoa.trim()
  const hienTaoMoi =
    Boolean(onTaoMoi) &&
    tuKhoaSach.length > 0 &&
    !luaChon.some((l) => l.nhan.trim().toLowerCase() === tuKhoaSach.toLowerCase())

  const taoMoi = async () => {
    if (!onTaoMoi || dangTao) return
    setDangTao(true)
    try {
      const giaTriMoi = await onTaoMoi(tuKhoaSach)
      // Thất bại thì GIỮ dropdown mở để người dùng thấy lỗi và sửa từ khoá — đóng lại sẽ
      // khiến họ tưởng đã tạo xong.
      if (giaTriMoi) chon(giaTriMoi)
    } finally {
      setDangTao(false)
    }
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
      // Không có mục nào khớp mà đang cho tạo mới → Enter là tạo. Người dùng gõ tên mới rồi
      // nhấn Enter theo phản xạ, không muốn phải rê chuột xuống dòng cuối.
      else if (hienTaoMoi) void taoMoi()
    } else if (e.key === 'Escape') {
      e.preventDefault()
      setMo(false)
    }
  }

  return (
    <div ref={boc} className="relative">
      <button
        ref={nut}
        id={id}
        type="button"
        disabled={disabled}
        onClick={() => {
          // Tính toạ độ TRƯỚC khi mở: `fixed` cần vị trí của neo ở thời điểm này.
          tinh()
          setMo((v) => !v)
        }}
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
        <div
          style={viTri}
          className="z-50 flex flex-col overflow-hidden rounded-md border border-border bg-card shadow-lg"
        >
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

          <ul role="listbox" className="min-h-0 flex-1 overflow-y-auto p-1">
            {daLoc.length === 0 && !hienTaoMoi ? (
              <li className="px-2 py-3 text-center text-sm text-muted-foreground">
                {/* Khi danh sách RỖNG SẴN (chưa gõ gì) mà select này cho tạo mới, "Không tìm
                    thấy" là câu vô nghĩa — chẳng ai tìm gì cả. Và KHÔNG lặp lại
                    placeholderTimKiem: nó đã hiện ngay trên đầu ở ô tìm, in lại thành hai
                    dòng giống nhau. */}
                {onTaoMoi && tuKhoa.length === 0 ? 'Danh sách còn trống' : 'Không tìm thấy'}
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

            {/* Dòng tạo mới đặt CUỐI danh sách, không phải đầu: mục đã có luôn quan trọng hơn
                mục chưa có, và đặt đầu thì người dùng dễ bấm nhầm khi danh sách vừa lọc lại. */}
            {hienTaoMoi && (
              <li className={cn(daLoc.length > 0 && 'mt-1 border-t border-border pt-1')}>
                <button
                  type="button"
                  disabled={dangTao}
                  onClick={() => void taoMoi()}
                  className="flex w-full items-center gap-2 rounded px-2 py-1.5 text-left text-sm hover:bg-muted disabled:opacity-50"
                >
                  <Plus className="h-3.5 w-3.5 shrink-0 text-primary" />
                  <span className="min-w-0 truncate">
                    {dangTao
                      ? 'Đang tạo…'
                      : (nhanTaoMoi ?? 'Tạo "{ten}"').replace('{ten}', tuKhoaSach)}
                  </span>
                </button>
              </li>
            )}
          </ul>

          {duoiDanhSach && <div className="border-t border-border p-1.5">{duoiDanhSach}</div>}
        </div>
      )}
    </div>
  )
}

/**
 * Bản chọn NHIỀU của {@link SelectTimKiem} — mục đã chọn hiển thị dạng chip.
 *
 * Tách component riêng thay vì thêm cờ `multiple`: kiểu của `giaTri`/`onDoi` khác nhau
 * (chuỗi so với mảng), gộp lại sẽ phải dùng union type và ép kiểu ở mọi nơi gọi.
 */
export function SelectTimKiemNhieu({
  luaChon,
  giaTri,
  onDoi,
  placeholder = 'Chọn…',
  placeholderTimKiem = 'Gõ để tìm…',
  id,
  disabled,
}: {
  luaChon: LuaChon[]
  giaTri: string[]
  onDoi: (giaTri: string[]) => void
  placeholder?: string
  placeholderTimKiem?: string
  id?: string
  disabled?: boolean
}) {
  const [mo, setMo] = React.useState(false)
  const [tuKhoa, setTuKhoa] = React.useState('')
  const [chiSoHighlight, setChiSoHighlight] = React.useState(0)
  const boc = React.useRef<HTMLDivElement>(null)
  const nut = React.useRef<HTMLButtonElement>(null)
  const oNhap = React.useRef<HTMLInputElement>(null)
  const { viTri, tinh } = useViTriTha(mo, nut)

  const daChon = luaChon.filter((l) => giaTri.includes(l.giaTri))

  const daLoc = React.useMemo(() => {
    const tu = boDau(tuKhoa)
    if (!tu) return luaChon
    return luaChon.filter(
      (l) => boDau(l.nhan).includes(tu) || (l.phu ? boDau(l.phu).includes(tu) : false),
    )
  }, [luaChon, tuKhoa])

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
      requestAnimationFrame(() => oNhap.current?.focus())
    }
  }, [mo])

  // Không đóng sau mỗi lần chọn: tick nhiều mục liên tiếp là thao tác thường gặp.
  const bat = (v: string) =>
    onDoi(giaTri.includes(v) ? giaTri.filter((x) => x !== v) : [...giaTri, v])

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
      if (muc) bat(muc.giaTri)
    } else if (e.key === 'Escape') {
      e.preventDefault()
      setMo(false)
    }
  }

  return (
    <div ref={boc} className="relative">
      <button
        ref={nut}
        id={id}
        type="button"
        disabled={disabled}
        onClick={() => {
          // Tính toạ độ TRƯỚC khi mở: `fixed` cần vị trí của neo ở thời điểm này.
          tinh()
          setMo((v) => !v)
        }}
        className={cn(
          'flex min-h-9 w-full items-center justify-between gap-2 rounded-md border border-input',
          'bg-background px-2 py-1 text-sm',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
          'disabled:cursor-not-allowed disabled:opacity-50',
        )}
        aria-haspopup="listbox"
        aria-expanded={mo}
      >
        {daChon.length === 0 ? (
          <span className="px-1 text-muted-foreground">{placeholder}</span>
        ) : (
          <span className="flex flex-wrap gap-1">
            {daChon.map((l) => (
              <span
                key={l.giaTri}
                className="inline-flex items-center gap-1 rounded bg-accent/15 px-1.5 py-0.5 text-xs text-accent"
              >
                {l.nhan}
                <span
                  role="button"
                  tabIndex={-1}
                  aria-label={`Bỏ ${l.nhan}`}
                  onClick={(e) => {
                    e.stopPropagation()
                    bat(l.giaTri)
                  }}
                  className="hover:opacity-70"
                >
                  <X className="h-3 w-3" />
                </span>
              </span>
            ))}
          </span>
        )}
        <ChevronsUpDown className="h-4 w-4 shrink-0 text-muted-foreground" />
      </button>

      {mo && (
        <div
          style={viTri}
          className="z-50 flex flex-col overflow-hidden rounded-md border border-border bg-card shadow-lg"
        >
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

          <ul role="listbox" aria-multiselectable className="min-h-0 flex-1 overflow-y-auto p-1">
            {daLoc.length === 0 ? (
              <li className="px-2 py-3 text-center text-sm text-muted-foreground">
                Không tìm thấy
              </li>
            ) : (
              daLoc.map((l, i) => {
                const chon = giaTri.includes(l.giaTri)
                return (
                  <li key={l.giaTri}>
                    <button
                      type="button"
                      role="option"
                      aria-selected={chon}
                      onMouseEnter={() => setChiSoHighlight(i)}
                      onClick={() => bat(l.giaTri)}
                      className={cn(
                        'flex w-full items-center gap-2 rounded px-2 py-1.5 text-left text-sm',
                        i === chiSoHighlight && 'bg-muted',
                      )}
                    >
                      <span
                        className={cn(
                          'flex h-4 w-4 shrink-0 items-center justify-center rounded border',
                          chon
                            ? 'border-primary bg-primary text-primary-foreground'
                            : 'border-input',
                        )}
                      >
                        {chon && <Check className="h-3 w-3" />}
                      </span>
                      <span className="min-w-0">
                        <span className="block truncate">{l.nhan}</span>
                        {l.phu && (
                          <span className="block truncate text-xs text-muted-foreground">
                            {l.phu}
                          </span>
                        )}
                      </span>
                    </button>
                  </li>
                )
              })
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
