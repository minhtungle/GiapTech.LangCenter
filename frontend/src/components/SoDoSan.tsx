import { useCallback, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import {
  CAU_HINH_SAN, timMauAo, MAU_MAC_DINH_TA, MAU_MAC_DINH_DOI_THU, type LoaiSan,
} from '@/components/soDo/loaiSan'

/**
 * Bảng chiến thuật kéo-thả (FR-10 tab b).
 *
 * Tham khảo `renderfoot.com/tactical-board`: áo tròn mang **số + mã vị trí**, sơ đồ nạp nhanh,
 * hai đội trên cùng một sân, băng ghế dự bị cạnh sân.
 *
 * **Toạ độ lưu dạng phần trăm (0–100), không phải pixel**: sân co giãn theo màn hình, lưu
 * pixel thì mở trên máy khác lệch hết vị trí.
 *
 * **Hai đội thao tác y hệt nhau**: cùng kiểu dữ liệu, cùng đường kéo-thả, cùng ô sửa số áo.
 * Đội đối thủ trước đây chỉ là quân cờ đánh số — không sửa được gì — nên dựng tình huống
 * "đối thủ đá 3-5-2" là không làm được.
 */

/** Một quân trên sân, dùng chung cho cả hai đội. */
export interface QuanTrenSan {
  /** Đội nhà: id hồ sơ cầu thủ. Đối thủ: id tự sinh, không gắn hồ sơ nào. */
  id: string
  /** 0–100, tính từ trái sang. */
  x: number
  /** 0–100, tính từ trên (khung thành đối phương) xuống. */
  y: number
  /** Mã vị trí hiện trên áo (GK, CB, ST…). */
  viTri?: string
  /** Số áo. Đội nhà bỏ trống thì lấy số áo trong hồ sơ, rồi mới đến chữ viết tắt tên. */
  so?: number
  /** Tên hiện dưới áo. Đội nhà lấy từ hồ sơ; đối thủ gõ tay, thường bỏ trống. */
  ten?: string
}

/** Cầu thủ đội nhà có thể xếp lên sân — nguồn từ đội hình trận, thông tin từ hồ sơ. */
export interface CauThuTrenSan {
  id: string
  hoTen: string
  laDuBi: boolean
  /** Số áo trong hồ sơ cầu thủ (FR-04) — mặc định cho áo, khỏi gõ lại từng trận. */
  soAo?: number | null
  /** Vị trí sở trường trong hồ sơ — gợi ý khi nạp sơ đồ. */
  viTriSoTruong?: string | null
}

export type Ben = 'ta' | 'doiThu'

/** Chữ trên áo khi không có số: hai chữ cái đầu của tên và họ. */
function vietTat(hoTen: string) {
  const tu = hoTen.trim().split(/\s+/)
  return tu.length === 1
    ? tu[0].slice(0, 2).toUpperCase()
    : (tu[0][0] + tu[tu.length - 1][0]).toUpperCase()
}

/** Tên hiện dưới áo: hai từ cuối, đủ phân biệt mà không tràn. */
function tenNgan(hoTen: string) {
  return hoTen.trim().split(/\s+/).slice(-2).join(' ')
}

export function SoDoSan({
  loaiSan,
  cauThus,
  quanTa,
  quanDoiThu,
  onDoiTa,
  onDoiDoiThu,
  mauTa,
  mauDoiThu,
  benDangChon,
  onChonBen,
  chiDoc = false,
}: {
  loaiSan: LoaiSan
  cauThus: CauThuTrenSan[]
  quanTa: QuanTrenSan[]
  quanDoiThu: QuanTrenSan[]
  onDoiTa: (q: QuanTrenSan[]) => void
  onDoiDoiThu: (q: QuanTrenSan[]) => void
  /** Mã màu áo trong BANG_MAU_AO. Bỏ trống thì dùng trắng / đỏ như trước. */
  mauTa?: string
  mauDoiThu?: string
  /** Bên đang được thao tác — quyết định nút sơ đồ nạp nhanh áp cho ai. */
  benDangChon: Ben
  onChonBen: (b: Ben) => void
  chiDoc?: boolean
}) {
  const { t } = useTranslation()
  const san = useRef<HTMLDivElement>(null)
  const [dangKeo, setDangKeo] = useState<{ ben: Ben; id: string } | null>(null)
  const [dangChon, setDangChon] = useState<{ ben: Ben; id: string } | null>(null)
  // Điểm bắt đầu bấm, giữ trong ref chứ không chỉ state: `ketThucKeo` chạy trong cùng lượt
  // sự kiện với `batDauKeo` khi bấm nhanh, lúc đó state chưa kịp cập nhật.
  const diemBatDau = useRef<{ x: number; y: number; ben: Ben; id: string } | null>(null)

  const cauHinh = CAU_HINH_SAN[loaiSan]
  const mauCua = (b: Ben) =>
    b === 'ta'
      ? timMauAo(mauTa, MAU_MAC_DINH_TA)
      : timMauAo(mauDoiThu, MAU_MAC_DINH_DOI_THU)
  const daXep = new Set(quanTa.map((q) => q.id))
  const banGhe = cauThus.filter((c) => !daXep.has(c.id))

  const quanCua = (b: Ben) => (b === 'ta' ? quanTa : quanDoiThu)
  const doiQuan = (b: Ben, q: QuanTrenSan[]) => (b === 'ta' ? onDoiTa(q) : onDoiDoiThu(q))

  /** Chuyển toạ độ chuột sang phần trăm trong khung sân, kẹp trong [0,100]. */
  const sangPhanTram = useCallback((clientX: number, clientY: number) => {
    const r = san.current?.getBoundingClientRect()
    if (!r) return null
    return {
      x: Math.min(100, Math.max(0, ((clientX - r.left) / r.width) * 100)),
      y: Math.min(100, Math.max(0, ((clientY - r.top) / r.height) * 100)),
    }
  }, [])

  const batDauKeo = (ben: Ben, id: string) => (e: React.PointerEvent) => {
    if (chiDoc) return
    e.preventDefault()
    // Capture đặt trên KHUNG SÂN, không phải thẻ áo.
    //
    // Đặt trên áo (hay tệ hơn: `e.target`, thường là <span> con) thì mọi `pointermove` sau đó
    // bắn thẳng vào phần tử đó và KHÔNG nổi bọt lên sân — mà `onPointerMove` lại nằm ở sân,
    // nên cầu thủ đứng yên. Kiểm chứng trong trình duyệt: kéo 90px, toạ độ không đổi.
    san.current?.setPointerCapture(e.pointerId)
    diemBatDau.current = { x: e.clientX, y: e.clientY, ben, id }
    setDangKeo({ ben, id })
    onChonBen(ben)
  }

  const dangDiChuyen = (e: React.PointerEvent) => {
    if (!dangKeo) return
    const p = sangPhanTram(e.clientX, e.clientY)
    if (!p) return
    const x = Math.round(p.x * 10) / 10
    // Chuột cho toạ độ MÀN HÌNH; đối thủ vẽ lật nên phải lật ngược về hệ lưu, không thì kéo
    // xuống lại thấy quân đi lên.
    const y = Math.round((dangKeo.ben === 'doiThu' ? 100 - p.y : p.y) * 10) / 10
    doiQuan(dangKeo.ben, quanCua(dangKeo.ben).map((q) => (q.id === dangKeo.id ? { ...q, x, y } : q)))
  }

  const ketThucKeo = (e: React.PointerEvent) => {
    // Nhả capture, nếu không sân giữ con trỏ mãi và cú bấm tiếp theo ở chỗ khác bị nuốt.
    if (san.current?.hasPointerCapture(e.pointerId)) san.current.releasePointerCapture(e.pointerId)

    // Di chuyển dưới 5px = người dùng BẤM chứ không kéo → mở ô sửa số áo.
    //
    // Không thể dùng `onClick` của thẻ áo: capture đặt ở sân nên `pointerup` bắn vào sân,
    // trình duyệt không sinh sự kiện click trên áo nữa. Đo quãng đường là cách duy nhất
    // phân biệt hai ý định. Ngưỡng 5px vì ngón tay trên cảm ứng luôn lệch vài pixel.
    const bd = diemBatDau.current
    if (bd && Math.hypot(e.clientX - bd.x, e.clientY - bd.y) < 5) {
      setDangChon({ ben: bd.ben, id: bd.id })
    }

    diemBatDau.current = null
    setDangKeo(null)
  }

  /** Thả một cầu thủ từ băng ghế vào sân (chỉ đội nhà — đối thủ không có hồ sơ). */
  const thaVaoSan = (e: React.DragEvent) => {
    if (chiDoc) return
    e.preventDefault()
    const cauThuId = e.dataTransfer.getData('text/plain')
    if (!cauThuId) return

    const p = sangPhanTram(e.clientX, e.clientY)
    if (!p) return

    const ho = cauThus.find((c) => c.id === cauThuId)
    onDoiTa([
      ...quanTa.filter((q) => q.id !== cauThuId),
      {
        id: cauThuId,
        x: Math.round(p.x * 10) / 10,
        y: Math.round(p.y * 10) / 10,
        // Kéo từ băng ghế đã lấy sẵn số áo và vị trí sở trường từ hồ sơ.
        so: ho?.soAo ?? undefined,
        viTri: ho?.viTriSoTruong ?? undefined,
      },
    ])
  }

  const boRaKhoiSan = (ben: Ben, id: string) => {
    doiQuan(ben, quanCua(ben).filter((q) => q.id !== id))
    setDangChon(null)
  }

  const doiThuocTinh = (ben: Ben, id: string, thay: Partial<QuanTrenSan>) =>
    doiQuan(ben, quanCua(ben).map((q) => (q.id === id ? { ...q, ...thay } : q)))

  const chon = dangChon ? quanCua(dangChon.ben).find((q) => q.id === dangChon.id) : undefined
  const hoSoDangChon =
    dangChon?.ben === 'ta' ? cauThus.find((c) => c.id === dangChon.id) : undefined

  /** Nội dung áo: số áo trên sân → số áo hồ sơ → viết tắt tên → dấu chấm hỏi. */
  const nhanAo = (ben: Ben, q: QuanTrenSan) => {
    if (q.so !== undefined) return String(q.so)
    if (ben === 'ta') {
      const ho = cauThus.find((c) => c.id === q.id)
      if (ho?.soAo) return String(ho.soAo)
      if (ho) return vietTat(ho.hoTen)
    }
    return q.ten ? vietTat(q.ten) : '?'
  }

  const tenDuoiAo = (ben: Ben, q: QuanTrenSan) => {
    if (ben === 'ta') {
      const ho = cauThus.find((c) => c.id === q.id)
      if (ho) return tenNgan(ho.hoTen)
    }
    return q.ten ? tenNgan(q.ten) : null
  }

  /**
   * Đối thủ đá NGƯỢC HƯỚNG: y hiển thị = 100 − y lưu.
   *
   * Sơ đồ dựng sẵn đều viết theo hướng "thủ môn ở đáy sân". Áp thẳng cho cả hai đội thì hai
   * thủ môn chồng lên nhau ở cùng một khung thành — đã thấy trên ảnh chụp thật. Lật khi VẼ
   * chứ không khi lưu, để dữ liệu của hai đội cùng một hệ quy chiếu; lật lúc lưu thì kéo tay
   * một quân đối thủ rồi lưu lại sẽ nhân đôi phép lật.
   */
  const yHienThi = (ben: Ben, y: number) => (ben === 'doiThu' ? 100 - y : y)

  const veQuan = (ben: Ben) =>
    quanCua(ben).map((q) => {
      const dangKeoQuanNay = dangKeo?.ben === ben && dangKeo.id === q.id
      const dangChonQuanNay = dangChon?.ben === ben && dangChon.id === q.id
      const ten = tenDuoiAo(ben, q)
      const laTa = ben === 'ta'
      const duBi = laTa && cauThus.find((c) => c.id === q.id)?.laDuBi
      const mau = mauCua(ben)

      return (
        <button
          key={`${ben}-${q.id}`}
          type="button"
          onPointerDown={batDauKeo(ben, q.id)}
          onDoubleClick={() => !chiDoc && boRaKhoiSan(ben, q.id)}
          title={`${ten ?? q.id}${chiDoc ? '' : ` — ${t('soDo.nhayDupDeBoRa')}`}`}
          style={{ left: `${q.x}%`, top: `${yHienThi(ben, q.y)}%` }}
          className={cn(
            'absolute flex -translate-x-1/2 -translate-y-1/2 flex-col items-center',
            // Bên đang thao tác nổi lên trên để không bị bên kia che khi chồng lấn.
            benDangChon === ben ? 'z-20' : 'z-10',
            chiDoc ? 'cursor-default' : 'cursor-grab active:cursor-grabbing',
            dangKeoQuanNay && 'z-30 scale-110',
          )}
        >
          <span
            style={
              // Dự bị giữ màu xám riêng: họ chưa vào sân, tô cùng màu đội hình chính thì
              // nhìn nhầm thành 12 người trên sân.
              duBi ? undefined : { background: mau.nen, color: mau.chu }
            }
            className={cn(
              'flex h-7 w-7 items-center justify-center rounded-full border-2 text-[11px] font-bold shadow',
              // Viền trắng nhạt tách áo khỏi nền cỏ với mọi màu, kể cả áo đen.
              'border-white/75',
              duBi && 'bg-[hsl(var(--muted))] text-[hsl(var(--muted-foreground))]',
              dangChonQuanNay && 'ring-2 ring-[hsl(var(--accent))] ring-offset-1',
            )}
          >
            {nhanAo(ben, q)}
          </span>
          {/* Mã vị trí đè lên mép áo — đọc được vai trò mà không tốn thêm một dòng. */}
          {q.viTri && (
            <span className="pointer-events-none absolute -right-1.5 -top-1 rounded bg-[hsl(var(--accent))] px-0.5 text-[8px] font-bold leading-tight text-white">
              {q.viTri}
            </span>
          )}
          {ten && (
            <span className="mt-0.5 max-w-20 truncate rounded bg-black/50 px-1 text-[9px] leading-tight text-white">
              {ten}
            </span>
          )}
        </button>
      )
    })

  const { rong: vcRong, sau: vcSau } = cauHinh.vongCam
  const [, , vbW, vbH] = cauHinh.viewBox.split(' ').map(Number)
  const vcX = (vbW - vcRong) / 2
  const khungRong = vcRong * 0.45
  const khungX = (vbW - khungRong) / 2

  return (
    <div className="flex flex-col gap-4 xl:flex-row xl:items-start">
      <div
        ref={san}
        onPointerMove={dangDiChuyen}
        onPointerUp={ketThucKeo}
        onPointerCancel={ketThucKeo}
        onDragOver={(e) => e.preventDefault()}
        onDrop={thaVaoSan}
        style={{ aspectRatio: cauHinh.tyLe }}
        // Mốc cho test E2E: tỷ lệ sân là inline style (đổi theo loại sân) nên không có class
        // cố định nào để bám vào.
        data-testid="san"

        className={cn(
          'relative mx-auto shrink-0 select-none overflow-hidden rounded-lg',
          // Cỏ sọc như bảng chiến thuật thật — dải sáng/tối xen kẽ theo chiều ngang.
          'bg-[hsl(152_38%_32%)] bg-[linear-gradient(hsl(0_0%_100%/0.045)_50%,transparent_50%)] bg-[length:100%_11.11%]',
          // Chiều CAO là ràng buộc, không phải chiều rộng: sân dọc thả theo bề rộng cột thì
          // cao hơn 1000px, hai đầu sân không cùng nằm trong khung nhìn và kéo-thả hết dùng được.
          'h-[62vh] max-h-[620px] w-auto max-w-full',
          dangKeo && 'cursor-grabbing',
        )}
      >
        {/* Nét sân vẽ bằng SVG — sắc ở mọi kích thước, không cần tệp ảnh. Kích thước vòng cấm
            và khung thành đổi theo loại sân: sân 5 người có vòng cấm nhỏ hơn hẳn sân 11. */}
        <svg
          viewBox={cauHinh.viewBox}
          preserveAspectRatio="none"
          className="pointer-events-none absolute inset-0 h-full w-full"
          aria-hidden
        >
          <g fill="none" stroke="rgba(255,255,255,0.5)" strokeWidth="0.4">
            <rect x="2" y="2" width={vbW - 4} height={vbH - 4} />
            <line x1="2" y1={vbH / 2} x2={vbW - 2} y2={vbH / 2} />
            <circle cx={vbW / 2} cy={vbH / 2} r={vbW * 0.13} />
            <circle cx={vbW / 2} cy={vbH / 2} r="0.9" fill="rgba(255,255,255,0.5)" />

            {/* Vòng cấm + khung thành hai đầu */}
            <rect x={vcX} y="2" width={vcRong} height={vcSau} />
            <rect x={khungX} y="2" width={khungRong} height={vcSau * 0.4} />
            <rect x={vcX} y={vbH - 2 - vcSau} width={vcRong} height={vcSau} />
            <rect x={khungX} y={vbH - 2 - vcSau * 0.4} width={khungRong} height={vcSau * 0.4} />

            {/* Cung phạt góc bốn góc */}
            <path d={`M 2 5 A 3 3 0 0 0 5 2`} />
            <path d={`M ${vbW - 5} 2 A 3 3 0 0 0 ${vbW - 2} 5`} />
            <path d={`M ${vbW - 2} ${vbH - 5} A 3 3 0 0 0 ${vbW - 5} ${vbH - 2}`} />
            <path d={`M 5 ${vbH - 2} A 3 3 0 0 0 2 ${vbH - 5}`} />
          </g>
        </svg>

        {/* Bên KHÔNG được chọn vẽ trước để bên đang thao tác nằm trên khi chồng lấn. */}
        {veQuan(benDangChon === 'ta' ? 'doiThu' : 'ta')}
        {veQuan(benDangChon)}
      </div>

      <div className="flex min-w-0 flex-1 flex-col gap-4">
        {/* Sửa số áo / vị trí của quân vừa bấm. Đặt trên cùng vì đó là việc kế tiếp sau khi
            bấm vào một áo — để dưới băng ghế thì phải cuộn mới thấy. */}
        {!chiDoc && chon && dangChon && (
          <div className="rounded-lg border border-border p-3">
            <p className="mb-2 text-sm font-medium">
              {hoSoDangChon?.hoTen ?? chon.ten ?? t('soDo.quanDoiThu')}
              <span className="ml-2 text-xs font-normal text-muted-foreground">
                {t(dangChon.ben === 'ta' ? 'soDo.doiNha' : 'soDo.doiKhach')}
              </span>
            </p>
            <div className="flex flex-wrap items-end gap-3">
              <label className="flex flex-col gap-1 text-xs text-muted-foreground">
                {t('soDo.soAo')}
                <input
                  type="number"
                  min={1}
                  max={99}
                  value={chon.so ?? ''}
                  onChange={(e) =>
                    doiThuocTinh(dangChon.ben, dangChon.id, {
                      so: e.target.value === '' ? undefined : Number(e.target.value),
                    })
                  }
                  className="h-8 w-16 rounded-md border border-input bg-background px-2 text-sm text-foreground"
                />
              </label>
              <label className="flex flex-col gap-1 text-xs text-muted-foreground">
                {t('soDo.maViTri')}
                <input
                  value={chon.viTri ?? ''}
                  onChange={(e) =>
                    doiThuocTinh(dangChon.ben, dangChon.id, {
                      viTri: e.target.value.toUpperCase() || undefined,
                    })
                  }
                  placeholder="GK"
                  maxLength={4}
                  className="h-8 w-20 rounded-md border border-input bg-background px-2 text-sm uppercase text-foreground"
                />
              </label>
              {/* Tên chỉ sửa được cho đối thủ: đội nhà lấy từ hồ sơ cầu thủ, sửa ở đây sẽ
                  tạo ra hai cái tên cho cùng một người. */}
              {dangChon.ben === 'doiThu' && (
                <label className="flex flex-col gap-1 text-xs text-muted-foreground">
                  {t('soDo.tenCauThu')}
                  <input
                    value={chon.ten ?? ''}
                    onChange={(e) =>
                      doiThuocTinh(dangChon.ben, dangChon.id, { ten: e.target.value || undefined })
                    }
                    className="h-8 w-40 rounded-md border border-input bg-background px-2 text-sm text-foreground"
                  />
                </label>
              )}
              <button
                type="button"
                onClick={() => boRaKhoiSan(dangChon.ben, dangChon.id)}
                className="h-8 rounded-md border border-border px-2 text-xs text-[hsl(var(--status-lose))] hover:bg-muted"
              >
                {t('soDo.boRaKhoiSan')}
              </button>
            </div>
          </div>
        )}

        <div>
          <p className="mb-2 text-sm font-medium">
            {t('soDo.banGhe')} ({banGhe.length})
          </p>

          {banGhe.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t('soDo.daXepHet')}</p>
          ) : (
            <div className="flex flex-wrap gap-1.5">
              {banGhe.map((c) => (
                <span
                  key={c.id}
                  draggable={!chiDoc}
                  onDragStart={(e) => e.dataTransfer.setData('text/plain', c.id)}
                  className={cn(
                    'inline-flex items-center gap-1.5 rounded-md border border-border px-2 py-1 text-xs',
                    chiDoc ? '' : 'cursor-grab active:cursor-grabbing hover:bg-muted',
                  )}
                >
                  <span className="flex h-5 w-5 items-center justify-center rounded-full bg-muted text-[10px] font-bold">
                    {c.soAo ?? vietTat(c.hoTen)}
                  </span>
                  {c.hoTen}
                  {c.viTriSoTruong && (
                    <span className="text-[10px] text-muted-foreground">{c.viTriSoTruong}</span>
                  )}
                </span>
              ))}
            </div>
          )}
        </div>

        {!chiDoc && <p className="text-xs text-muted-foreground">{t('soDo.huongDan')}</p>}
      </div>
    </div>
  )
}
