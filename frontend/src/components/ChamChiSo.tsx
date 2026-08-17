import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'

/**
 * Chấm điểm chỉ số kỹ năng cầu thủ sau trận (FR-10 tab c → nguồn của FR-14).
 *
 * Lưu vào `DANHGIA_CAUTHU.chi_so_ky_nang` dạng JSON `{tanCong: 7, phongNgu: 5, ...}` —
 * cột đã có sẵn nên **không cần migration**, và thêm tiêu chí mới sau này cũng không cần.
 *
 * **Thang 1–10, không phải 1–5**: người Việt quen chấm điểm thang 10, và thang 5 dồn quá
 * nhiều cầu thủ vào cùng một bậc nên biểu đồ radar (FR-14) thành hình đa giác đều vô nghĩa.
 */

export const CHI_SO_KY_NANG = [
  'tanCong',
  'phongNgu',
  'chuyenBong',
  'reDat',
  'theLuc',
  'tinhThan',
] as const

export type MaChiSo = (typeof CHI_SO_KY_NANG)[number]
export type BoChiSo = Partial<Record<MaChiSo, number>>

/** Đọc JSON đã lưu; hỏng thì coi như chưa chấm, không làm sập cả bảng đánh giá. */
export function docChiSo(json: string | null | undefined): BoChiSo {
  if (!json) return {}
  try {
    const o = JSON.parse(json) as Record<string, unknown>
    const kq: BoChiSo = {}
    for (const ma of CHI_SO_KY_NANG) {
      const v = o[ma]
      if (typeof v === 'number' && v >= 1 && v <= 10) kq[ma] = Math.round(v)
    }
    return kq
  } catch {
    return {}
  }
}

/** Chỉ ghi JSON khi có ít nhất một chỉ số — chuỗi `{}` rỗng chỉ tổ làm bẩn cột. */
export function ghiChiSo(bo: BoChiSo): string | null {
  const co = Object.entries(bo).filter(([, v]) => typeof v === 'number')
  return co.length ? JSON.stringify(Object.fromEntries(co)) : null
}

/** Điểm trung bình để xếp hạng nhanh; chưa chấm gì thì null, không phải 0. */
export function diemTrungBinh(bo: BoChiSo): number | null {
  const vs = CHI_SO_KY_NANG.map((m) => bo[m]).filter((v): v is number => typeof v === 'number')
  return vs.length ? Math.round((vs.reduce((a, b) => a + b, 0) / vs.length) * 10) / 10 : null
}

/** Màu theo bậc điểm — dùng chung cho thanh trượt và biểu đồ, để hai chỗ không nói khác nhau. */
function mauTheoDiem(d: number) {
  if (d >= 8) return 'hsl(var(--status-win))'
  if (d >= 6) return 'hsl(var(--primary))'
  if (d >= 4) return 'hsl(var(--status-draw))'
  return 'hsl(var(--status-lose))'
}

/**
 * Một hàng chấm điểm: nhãn + 10 ô bấm.
 *
 * Dùng ô bấm thay vì `<input type=range>`: thanh trượt trên chuột phải kéo chính xác mới
 * ra đúng số, còn đây bấm phát ăn ngay — chấm 6 chỉ số × 11 cầu thủ thì chênh lệch đó lớn.
 * Bấm lại đúng ô đang chọn = bỏ chấm, vì "chưa đánh giá" khác "đánh giá 1 điểm".
 */
function HangChiSo({
  ma,
  giaTri,
  onDoi,
}: {
  ma: MaChiSo
  giaTri: number | undefined
  onDoi: (v: number | undefined) => void
}) {
  const { t } = useTranslation()

  return (
    <div className="flex items-center gap-2">
      <span className="w-20 shrink-0 text-xs text-muted-foreground">{t(`chiSo.${ma}`)}</span>
      <div className="flex gap-0.5" role="group" aria-label={t(`chiSo.${ma}`)}>
        {Array.from({ length: 10 }, (_, i) => i + 1).map((d) => {
          const daChon = giaTri !== undefined && d <= giaTri
          return (
            <button
              key={d}
              type="button"
              aria-label={`${t(`chiSo.${ma}`)}: ${d}`}
              aria-pressed={giaTri === d}
              onClick={() => onDoi(giaTri === d ? undefined : d)}
              style={daChon ? { background: mauTheoDiem(giaTri) } : undefined}
              className={cn(
                'h-5 w-5 rounded-sm border text-[10px] font-medium transition-colors',
                daChon
                  ? 'border-transparent text-white'
                  : 'border-border bg-background text-muted-foreground hover:bg-muted',
              )}
            >
              {d}
            </button>
          )
        })}
      </div>
      <span className="w-6 shrink-0 text-right text-xs font-medium tabular-nums">
        {giaTri ?? '—'}
      </span>
    </div>
  )
}

/** Bảng chấm 6 chỉ số của một cầu thủ. */
export function ChamChiSo({
  giaTri,
  onDoi,
}: {
  giaTri: BoChiSo
  onDoi: (bo: BoChiSo) => void
}) {
  const { t } = useTranslation()
  const tb = diemTrungBinh(giaTri)

  return (
    <div className="flex flex-col gap-1.5">
      {CHI_SO_KY_NANG.map((ma) => (
        <HangChiSo
          key={ma}
          ma={ma}
          giaTri={giaTri[ma]}
          onDoi={(v) => {
            const moi = { ...giaTri }
            if (v === undefined) delete moi[ma]
            else moi[ma] = v
            onDoi(moi)
          }}
        />
      ))}

      <div className="mt-1 flex items-center gap-2 border-t border-border pt-1.5 text-xs">
        <span className="w-20 shrink-0 font-medium">{t('chiSo.trungBinh')}</span>
        <span
          className="font-semibold tabular-nums"
          style={tb !== null ? { color: mauTheoDiem(tb) } : undefined}
        >
          {tb ?? '—'}
        </span>
      </div>
    </div>
  )
}

/**
 * Biểu đồ radar 6 cạnh — FR-10 yêu cầu "hiển thị biểu đồ" ở phần đánh giá cầu thủ.
 *
 * Vẽ tay bằng SVG thay vì kéo Recharts vào: hình sáu cạnh cố định, không cần trục/chú giải/
 * tooltip, mà bundle đã 686KB rồi. Recharts vẫn dùng cho biểu đồ thống kê FR-13/14.
 */
export function RadarChiSo({ giaTri, kichThuoc = 120 }: { giaTri: BoChiSo; kichThuoc?: number }) {
  const { t } = useTranslation()
  const tam = kichThuoc / 2
  const banKinh = tam - 18

  /** Điểm thứ i trên vòng tròn, bắt đầu từ đỉnh (−90°) đi thuận chiều kim đồng hồ. */
  const toaDo = (i: number, tyLe: number) => {
    const goc = (Math.PI * 2 * i) / CHI_SO_KY_NANG.length - Math.PI / 2
    return [tam + Math.cos(goc) * banKinh * tyLe, tam + Math.sin(goc) * banKinh * tyLe] as const
  }

  const daChamGi = CHI_SO_KY_NANG.some((m) => giaTri[m] !== undefined)
  if (!daChamGi) {
    return (
      <div
        className="flex items-center justify-center text-xs text-muted-foreground"
        style={{ width: kichThuoc, height: kichThuoc }}
      >
        {t('chiSo.chuaCham')}
      </div>
    )
  }

  const tb = diemTrungBinh(giaTri) ?? 0
  const mau = mauTheoDiem(tb)

  // Chỉ số chưa chấm lấy TRUNG BÌNH của những chỉ số đã chấm, không phải 0.
  //
  // Coi là 0 thì chấm một chỉ số 8 điểm ra hình gần như một vạch thẳng — đọc thành "cầu thủ
  // này kém toàn diện" trong khi sự thật là "mới chấm một mục". Đã thấy trên ảnh chụp thật.
  // Các đỉnh suy đoán vẽ rỗng để phân biệt với đỉnh đã chấm.
  const diem = CHI_SO_KY_NANG.map((m, i) => toaDo(i, (giaTri[m] ?? tb) / 10))

  return (
    <svg width={kichThuoc} height={kichThuoc} role="img" aria-label={t('chiSo.bieuDo')}>
      {/* Lưới nền: 3 vòng ứng với mốc 10 / 6.6 / 3.3 điểm. */}
      {[1, 0.66, 0.33].map((r) => (
        <polygon
          key={r}
          points={CHI_SO_KY_NANG.map((_, i) => toaDo(i, r).join(',')).join(' ')}
          fill="none"
          stroke="hsl(var(--border))"
          strokeWidth="1"
        />
      ))}
      {CHI_SO_KY_NANG.map((_, i) => {
        const [x, y] = toaDo(i, 1)
        return <line key={i} x1={tam} y1={tam} x2={x} y2={y} stroke="hsl(var(--border))" strokeWidth="1" />
      })}

      <polygon
        points={diem.map((p) => p.join(',')).join(' ')}
        fill={mau}
        fillOpacity="0.25"
        stroke={mau}
        strokeWidth="1.5"
      />

      {/* Đỉnh đặc = đã chấm, đỉnh rỗng = suy từ trung bình. Không phân biệt thì người xem
          tưởng cả 6 chỉ số đều đã đánh giá. */}
      {CHI_SO_KY_NANG.map((m, i) => {
        const daCham = giaTri[m] !== undefined
        const [x, y] = diem[i]
        return (
          <circle
            key={`d-${m}`}
            cx={x}
            cy={y}
            r={daCham ? 2 : 1.5}
            fill={daCham ? mau : 'hsl(var(--card))'}
            stroke={mau}
            strokeWidth="1"
          />
        )
      })}

      {CHI_SO_KY_NANG.map((m, i) => {
        const [x, y] = toaDo(i, 1.22)
        return (
          <text
            key={m}
            x={x}
            y={y}
            textAnchor="middle"
            dominantBaseline="middle"
            fontSize="8"
            fill="hsl(var(--muted-foreground))"
          >
            {t(`chiSo.viet.${m}`)}
          </text>
        )
      })}
    </svg>
  )
}
