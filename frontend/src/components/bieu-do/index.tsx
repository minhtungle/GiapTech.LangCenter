import * as React from 'react'
import { cn } from '@/lib/utils'

/**
 * Biểu đồ dựng bằng SVG thuần — **không thêm thư viện** (FR-28, 14/09/2026).
 *
 * Vì sao: recharts/chart.js kéo theo 100-300KB và một tầng trừu tượng nữa phải học, trong khi
 * bốn dạng biểu đồ dự án cần (đường · thanh ngang · phễu · ô số) đều là vài chục dòng SVG. Cần
 * thêm dạng phức tạp (bản đồ nhiệt, scatter) thì lúc đó hẵng cân nhắc.
 *
 * **Quy cách nét vẽ** — cố định cho mọi biểu đồ, theo chuẩn trực quan hoá:
 * - Thanh ≤ 24px, bo 4px ở ĐẦU dữ liệu, vuông ở chân trục
 * - Đường 2px, điểm ≥ 8px, vùng tô ~10% độ đục
 * - Lưới 1px ĐẶC (không nét đứt), màu lùi hẳn về sau
 * - Khe 2px màu nền tách hai mảng chạm nhau — không vẽ viền quanh mảng
 *
 * **Màu**: `--chart-1..5` trong `index.css`, đã kiểm bằng script (không ước lượng bằng mắt).
 * Cặp cam↔lá có ΔE 6.9 nên **mọi biểu đồ nhiều chuỗi bắt buộc có nhãn trực tiếp**.
 */

/** Định dạng tiền VND gọn: 1.2 tỷ · 340 tr · 85 ng. Số tiền đầy đủ chỉ hiện ở tooltip. */
export function tienGon(v: number): string {
  const a = Math.abs(v)
  if (a >= 1_000_000_000) return `${(v / 1_000_000_000).toFixed(1)} tỷ`
  if (a >= 1_000_000) return `${Math.round(v / 1_000_000)} tr`
  if (a >= 1_000) return `${Math.round(v / 1_000)} ng`
  return String(Math.round(v))
}

export const tienDayDu = (v: number) => v.toLocaleString('vi-VN') + ' ₫'

/** Ô số dẫn — dùng cho một con số hiện tại, KHÔNG dùng biểu đồ một thanh. */
export function OSo({
  nhan,
  giaTri,
  phu,
  deltaPhanTram,
  nhanMau,
}: {
  nhan: string
  giaTri: string
  phu?: string
  /** % thay đổi so kỳ trước. undefined = không có kỳ trước để so. */
  deltaPhanTram?: number
  /** Màu chấm nhận diện, khớp với chuỗi tương ứng trong biểu đồ bên dưới. */
  nhanMau?: string
}) {
  const tang = (deltaPhanTram ?? 0) >= 0
  return (
    // Cao bằng nhau dù ô nào thiếu dòng phụ — hàng ô số so le trông như giao diện hỏng.
    <div className="flex h-full flex-col rounded-lg border border-border p-3">
      <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
        {nhanMau && (
          <span
            className="h-2 w-2 shrink-0 rounded-full"
            style={{ background: nhanMau }}
            aria-hidden
          />
        )}
        {nhan}
      </div>
      <div className="mt-1 text-2xl font-semibold tabular-nums">{giaTri}</div>
      <div className="mt-0.5 flex min-h-[1.25rem] items-center gap-2 text-xs">
        {phu && <span className="text-muted-foreground">{phu}</span>}
        {deltaPhanTram !== undefined && Number.isFinite(deltaPhanTram) && (
          <span className={tang ? 'text-status-ok' : 'text-status-loi'}>
            {tang ? '▲' : '▼'} {Math.abs(deltaPhanTram).toFixed(1)}%
          </span>
        )}
      </div>
    </div>
  )
}

export interface DiemDuong {
  nhan: string
  giaTri: number
  phu?: string
}

/**
 * Đường theo thời gian — MỘT chuỗi, nên không cần chú giải (tiêu đề đã nói đang vẽ gì).
 *
 * Có lớp di chuột theo mặc định: biểu đồ HTML *là* thứ tương tác được, và số chính xác thuộc về
 * tooltip chứ không phải nhãn trên từng điểm.
 */
export function DuongThoiGian({
  diem,
  dinhDang = tienGon,
  dinhDangDayDu = tienDayDu,
  cao = 220,
}: {
  diem: DiemDuong[]
  dinhDang?: (v: number) => string
  dinhDangDayDu?: (v: number) => string
  cao?: number
}) {
  const [hover, setHover] = React.useState<number | null>(null)

  if (diem.length === 0) return null

  /*
    Một mốc thì KHÔNG vẽ đường được — nó ra một chấm lơ lửng giữa khung trống, người đọc không
    biết đang nhìn gì (thấy được khi chụp màn hình thật 14/09/2026, không thấy khi đọc code).
    Dưới 3 mốc thì cột đọc đúng hơn: đường ngụ ý xu hướng, mà hai điểm chưa thành xu hướng.
  */
  if (diem.length < 3) return <CotIt diem={diem} dinhDang={dinhDang} />

  const L = 52
  const R = 12
  const T = 12
  const B = 28
  const W = 720
  const H = cao
  const w = W - L - R
  const h = H - T - B

  const max = Math.max(...diem.map((d) => d.giaTri), 1)
  const x = (i: number) => (diem.length === 1 ? L + w / 2 : L + (i / (diem.length - 1)) * w)
  const y = (v: number) => T + h - (v / max) * h

  const duong = diem.map((d, i) => `${i === 0 ? 'M' : 'L'}${x(i)},${y(d.giaTri)}`).join(' ')
  const vung = `${duong} L${x(diem.length - 1)},${T + h} L${x(0)},${T + h} Z`

  // 4 vạch lưới — đủ để đọc mức, không nhiều tới mức thành nhiễu.
  const vach = [0, 0.25, 0.5, 0.75, 1].map((p) => max * p)

  return (
    <div className="relative">
      <svg viewBox={`0 0 ${W} ${H}`} className="w-full" role="img">
        {vach.map((v) => (
          <g key={v}>
            <line
              x1={L}
              x2={W - R}
              y1={y(v)}
              y2={y(v)}
              stroke="hsl(var(--chart-grid))"
              strokeWidth={1}
            />
            <text
              x={L - 8}
              y={y(v) + 4}
              textAnchor="end"
              className="fill-muted-foreground text-[10px] tabular-nums"
            >
              {dinhDang(v)}
            </text>
          </g>
        ))}

        {/* Vùng tô ~10% — một lớp phủ mờ, không phải mảng đặc. */}
        <path d={vung} fill="hsl(var(--chart-1))" opacity={0.1} />
        <path
          d={duong}
          fill="none"
          stroke="hsl(var(--chart-1))"
          strokeWidth={2}
          strokeLinejoin="round"
          strokeLinecap="round"
        />

        {diem.map((d, i) => (
          <g key={d.nhan}>
            {/* Vùng bắt chuột rộng hơn điểm — điểm 8px quá nhỏ để trỏ trúng. */}
            <rect
              x={x(i) - w / Math.max(diem.length, 1) / 2}
              y={T}
              width={w / Math.max(diem.length, 1)}
              height={h}
              fill="transparent"
              onMouseEnter={() => setHover(i)}
              onMouseLeave={() => setHover(null)}
            />
            {(hover === i || diem.length <= 12) && (
              <circle
                cx={x(i)}
                cy={y(d.giaTri)}
                r={hover === i ? 5 : 4}
                fill="hsl(var(--chart-1))"
                stroke="hsl(var(--background))"
                strokeWidth={2}
              />
            )}
          </g>
        ))}

        {hover !== null && (
          <line
            x1={x(hover)}
            x2={x(hover)}
            y1={T}
            y2={T + h}
            stroke="hsl(var(--chart-grid))"
            strokeWidth={1}
          />
        )}

        {diem.map((d, i) => {
          // Nhãn trục X thưa dần khi nhiều mốc — chồng chữ còn khó đọc hơn không có nhãn.
          const buoc = Math.ceil(diem.length / 8)
          if (i % buoc !== 0 && i !== diem.length - 1) return null
          return (
            <text
              key={d.nhan}
              x={x(i)}
              y={H - 8}
              textAnchor="middle"
              className="fill-muted-foreground text-[10px]"
            >
              {d.nhan}
            </text>
          )
        })}
      </svg>

      {hover !== null && (
        <div
          className="pointer-events-none absolute z-10 rounded-md border border-border bg-background px-2 py-1 text-xs shadow-md"
          style={{
            left: `${(x(hover) / W) * 100}%`,
            top: 0,
            transform: 'translate(-50%, -100%)',
          }}
        >
          <div className="font-medium">{diem[hover].nhan}</div>
          <div className="tabular-nums">{dinhDangDayDu(diem[hover].giaTri)}</div>
          {diem[hover].phu && (
            <div className="text-muted-foreground">{diem[hover].phu}</div>
          )}
        </div>
      )}
    </div>
  )
}

/** Ít mốc quá để vẽ đường — hiện dạng cột kèm nhãn thẳng trên đầu. */
function CotIt({
  diem,
  dinhDang,
}: {
  diem: DiemDuong[]
  dinhDang: (v: number) => string
}) {
  const max = Math.max(...diem.map((d) => d.giaTri), 1)
  return (
    <div className="flex h-[200px] items-end justify-center gap-6 px-4">
      {diem.map((d) => (
        <div key={d.nhan} className="flex w-24 flex-col items-center gap-1">
          <span className="text-sm font-medium tabular-nums">{dinhDang(d.giaTri)}</span>
          <div
            className="w-full rounded-t-[4px]"
            style={{
              height: `${Math.max((d.giaTri / max) * 140, 4)}px`,
              background: 'hsl(var(--chart-1))',
            }}
          />
          <span className="text-[11px] text-muted-foreground">{d.nhan}</span>
          {d.phu && <span className="text-[10px] text-muted-foreground">{d.phu}</span>}
        </div>
      ))}
    </div>
  )
}

export interface HangThanh {
  nhan: string
  giaTri: number
  phu?: string
}

/**
 * Thanh ngang xếp hạng — **MỘT màu cho mọi thanh**.
 *
 * Không tô mỗi thanh một màu: các hạng mục ở đây (người, đội, mặt hàng) không có thứ tự tự
 * nhiên, nên dải màu theo độ lớn sẽ mã hoá hai lần cùng một thông tin mà chiều dài thanh đã
 * nói rồi — và đốt mất kênh màu vào việc vô ích.
 *
 * Nằm ngang vì tên hạng mục dài (họ tên, tên khoá học); nằm dọc thì nhãn phải xoay nghiêng.
 */
export function ThanhNgang({
  hang,
  dinhDang = tienGon,
  mau = 'hsl(var(--chart-1))',
  toiDa = 8,
}: {
  hang: HangThanh[]
  dinhDang?: (v: number) => string
  mau?: string
  /** Quá số này thì gộp phần đuôi — thêm màu/thêm dòng không làm biểu đồ dễ đọc hơn. */
  toiDa?: number
}) {
  if (hang.length === 0) return null

  const sap = [...hang].sort((a, b) => b.giaTri - a.giaTri)
  const hienThi = sap.slice(0, toiDa)
  const con = sap.slice(toiDa)
  if (con.length > 0) {
    hienThi.push({
      nhan: `Khác (${con.length})`,
      giaTri: con.reduce((s, x) => s + x.giaTri, 0),
    })
  }

  const max = Math.max(...hienThi.map((h) => h.giaTri), 1)

  return (
    <ul className="grid gap-2">
      {hienThi.map((h) => (
        <li key={h.nhan} className="grid gap-1">
          <div className="flex items-baseline justify-between gap-3 text-sm">
            <span className="truncate">{h.nhan}</span>
            <span className="shrink-0 tabular-nums text-muted-foreground">
              {dinhDang(h.giaTri)}
              {h.phu && <span className="ml-1.5 text-xs">{h.phu}</span>}
            </span>
          </div>
          {/* Rãnh nền cùng hệ màu, thanh bo 4px ở đầu dữ liệu — chân trục để vuông. */}
          <div className="h-2.5 overflow-hidden rounded-sm bg-muted">
            <div
              className="h-full rounded-r-[4px]"
              style={{
                width: h.giaTri === 0 ? 0 : `${Math.max((h.giaTri / max) * 100, 2)}%`,
                background: mau,
              }}
            />
          </div>
        </li>
      ))}
    </ul>
  )
}

/**
 * Phễu bán hàng — các bước CÓ thứ tự, nên dùng dải một màu đậm dần, không phải 4 màu khác nhau.
 *
 * Giữ đủ mọi bước kể cả bước 0 khách: phễu thiếu bước là phễu đọc sai, và "0 khách ở bước Tư
 * vấn" tự nó đã là thông tin.
 */
export function Pheu({
  buoc,
}: {
  buoc: { nhan: string; soLuong: number }[]
}) {
  if (buoc.length === 0) return null
  const max = Math.max(...buoc.map((b) => b.soLuong), 1)

  return (
    <ul className="grid gap-2">
      {buoc.map((b, i) => {
        const truoc = i > 0 ? buoc[i - 1].soLuong : null
        // Tỷ lệ chuyển đổi từ bước trước — con số người ta thật sự muốn biết ở phễu.
        const tyLe = truoc && truoc > 0 ? (b.soLuong / truoc) * 100 : null
        return (
          <li key={b.nhan} className="grid gap-1">
            <div className="flex items-baseline justify-between gap-3 text-sm">
              <span>{b.nhan}</span>
              <span className="shrink-0 tabular-nums">
                {b.soLuong}
                {tyLe !== null && (
                  <span className="ml-2 text-xs text-muted-foreground">
                    {tyLe.toFixed(0)}%
                  </span>
                )}
              </span>
            </div>
            <div className="h-2.5 overflow-hidden rounded-sm bg-muted">
              <div
                className="h-full rounded-r-[4px]"
                style={{
                  // 0 khách thì KHÔNG vẽ vạch: một vạch tối thiểu ở bước rỗng trông như có
                  // dữ liệu, mà "0 khách ở bước này" chính là điều cần thấy rõ.
                  width: b.soLuong === 0 ? 0 : `${Math.max((b.soLuong / max) * 100, 2)}%`,
                  // Dải MỘT màu đậm dần theo bước — thứ tự có nghĩa nên màu phải có thứ tự.
                  background: `hsl(var(--chart-3) / ${1 - i * 0.18})`,
                }}
              />
            </div>
          </li>
        )
      })}
    </ul>
  )
}

/** Khung một biểu đồ: tiêu đề, mô tả ngắn, và chỗ cho trạng thái rỗng. */
export function KhungBieuDo({
  tieuDe,
  moTa,
  trong,
  children,
  className,
}: {
  tieuDe: string
  moTa?: string
  /** Thông điệp khi không có dữ liệu. Có giá trị = hiện nó thay cho biểu đồ. */
  trong?: string | null
  children: React.ReactNode
  className?: string
}) {
  return (
    <div className={cn('rounded-lg border border-border p-4', className)}>
      <h3 className="text-sm font-semibold">{tieuDe}</h3>
      {moTa && <p className="mt-0.5 text-xs text-muted-foreground">{moTa}</p>}
      <div className="mt-3">
        {trong ? (
          <p className="py-6 text-center text-sm text-muted-foreground">{trong}</p>
        ) : (
          children
        )}
      </div>
    </div>
  )
}
