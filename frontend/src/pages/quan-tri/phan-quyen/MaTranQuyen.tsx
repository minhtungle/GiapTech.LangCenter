import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import type { TFunction } from 'i18next'
import { AlertTriangle, Check } from 'lucide-react'
import type { DanhMucDto } from './kieu'
import { khoaO, thaoTacCua } from './kieu'

/**
 * Ma trận phân quyền dạng **thẻ theo chức năng** (thiết kế lại 14/09/2026).
 *
 * ## Vì sao bỏ bảng
 *
 * Bản trước là một bảng chức năng × thao tác. Với 4 thao tác nó còn đọc được; sau khi tách
 * thành 18 thao tác đặc thù thì một tab LMS thành **15 cột**, nhãn dài ("Cấu hình ma trận
 * quyền", "Gửi yêu cầu xếp lớp") phải **xoay dọc** mới vừa — người đọc nghiêng đầu. Tệ hơn:
 * ma trận rất **thưa**, 49 ô có nghĩa rải trên 300 vị trí, nên mắt không lần được hàng nào
 * ứng với cột nào. `Sinh lịch học` chỉ áp cho đúng một chức năng mà vẫn chiếm một cột suốt
 * chiều cao bảng.
 *
 * Bảng là đúng khi ma trận **đặc** và nhãn **ngắn**. Ở đây cả hai đều sai, nên đổi trục: mỗi
 * chức năng một thẻ, thao tác nằm **trong** thẻ. Nhãn nằm ngang, đọc bình thường; không còn
 * ô trống nào vì mỗi thẻ chỉ hiện thao tác của chính nó.
 *
 * ## Hai nhóm thao tác trong mỗi thẻ
 *
 * `Xem · Thêm · Sửa · Xóa` lên trước vì chúng có ở gần như mọi chức năng và người cấu hình
 * tìm chúng theo phản xạ. Thao tác đặc thù (`Chốt sổ`, `Duyệt`, `Thu tiền`…) xuống dưới, tách
 * bằng một đường kẻ — đó là phần cần đọc kỹ, và gộp lẫn vào CRUD thì nó trôi mất.
 *
 * ## Ô cần cân nhắc
 *
 * Backend đánh dấu qua `canCanNhac` (`ChucNang.CanCanNhac`): không đảo ngược được, leo thang
 * đặc quyền, dính tới tiền, hoặc mở rộng phạm vi dữ liệu. UI hiện **mô tả hệ quả ngay cạnh**
 * chứ không chỉ tô màu — "Chốt sổ" một mình không nói được rằng nó khoá sổ và sinh "Vắng mặc
 * định" cho mọi người chưa khai. Mô tả nằm ở `i18n.ts` (quy tắc #3), backend chỉ trả dấu hiệu.
 */
export function MaTranQuyen({
  danhMuc,
  dsChucNang,
  oDaChon,
  onDoi,
  chiDoc,
  laPhamVi,
}: {
  danhMuc: DanhMucDto
  dsChucNang: string[]
  oDaChon: Set<string>
  onDoi: (khoa: string, bat: boolean) => void
  /** true = chỉ xem, không sửa (người không có `PhanQuyen.CauHinhQuyen`). */
  chiDoc?: boolean
  /** true = đây là nhóm quyền phạm vi dữ liệu, đổi cách diễn đạt cho khớp. */
  laPhamVi?: boolean
}) {
  const { t } = useTranslation()

  const canNhac = useMemo(
    () => new Set(danhMuc.canCanNhac.map((x) => khoaO(x.chucNang, x.hanhDong))),
    [danhMuc.canCanNhac],
  )

  const coBan = ['Xem', 'Them', 'Sua', 'Xoa']

  if (dsChucNang.length === 0) return null

  return (
    <div
      className={
        laPhamVi
          ? 'flex flex-col gap-2'
          // `items-start`: không có nó, grid kéo mọi thẻ trong cùng hàng cao bằng thẻ cao
          // nhất — "Chức vụ" (3 ô) thành một khối rỗng cao bằng "Nhân sự" (6 ô).
          : 'grid items-start gap-2 lg:grid-cols-2'
      }
    >
      {dsChucNang.map((cn) => {
        const thaoTac = thaoTacCua(danhMuc, cn)
        const nhomCoBan = thaoTac.filter((h) => coBan.includes(h))
        const nhomRieng = thaoTac.filter((h) => !coBan.includes(h))
        const daChon = thaoTac.filter((h) => oDaChon.has(khoaO(cn, h))).length
        const dungChung = danhMuc.heThongs.find((x) => x.dungChung)?.chucNangs.includes(cn)

        return (
          <section
            key={cn}
            className="rounded-lg border border-border bg-card p-3"
            aria-label={t(`chucNang.${cn}`, cn)}
          >
            <header className="mb-2 flex items-start justify-between gap-2">
              <div className="min-w-0">
                <h3 className="truncate text-sm font-semibold">{t(`chucNang.${cn}`, cn)}</h3>
                {dungChung && (
                  <p className="mt-0.5 text-xs text-muted-foreground">
                    {t('heThong.DungChung')}
                  </p>
                )}
              </div>
              {/*
                Số đếm trên từng thẻ: thấy ngay chức năng nào đã cấp, chức năng nào chưa —
                thông tin mà bảng cũ không có ở bất kỳ mức nào.

                Không dùng `Badge variant="accent"`: màu nhấn theo quy ước dự án nghĩa là "cần
                chú ý", mà ở đây 3/3 chỉ là con số. Nếu mọi thẻ đã cấp đều sáng màu nhấn thì
                màu đó mất hết giá trị ở chỗ thật sự cần — chính là ô cần cân nhắc bên dưới.
                Phân biệt bằng đậm/nhạt là đủ.
              */}
              <span
                className={
                  'shrink-0 text-xs tabular-nums '
                  + (daChon > 0 ? 'font-semibold text-foreground' : 'text-muted-foreground')
                }
              >
                {daChon}/{thaoTac.length}
              </span>
            </header>

            <div className="flex flex-wrap gap-1.5">
              {nhomCoBan.map((hd) => (
                <O
                  key={hd}
                  nhan={t(`hanhDong.${hd}`, hd)}
                  bat={oDaChon.has(khoaO(cn, hd))}
                  canNhac={canNhac.has(khoaO(cn, hd))}
                  moTa={moTaCanNhac(t, cn, hd, canNhac)}
                  chiDoc={chiDoc}
                  onDoi={(bat) => onDoi(khoaO(cn, hd), bat)}
                />
              ))}
            </div>

            {nhomRieng.length > 0 && (
              <>
                {/*
                  Chỉ một đường kẻ, KHÔNG nhãn chữ. Bản đầu ghi "Thao tác riêng của chức năng
                  này" trên mỗi thẻ — 20 thẻ là 20 lần lặp cùng một câu, chiếm chỗ nhiều hơn
                  chính nội dung nó giới thiệu. Vị trí (dưới nhóm CRUD, cách bằng đường kẻ) đã
                  nói đủ; giải thích chung để một chỗ ở đầu mục.
                */}
                <div className="mt-2 mb-1.5 h-px bg-border" />
                <div className="flex flex-col gap-1.5">
                  {nhomRieng.map((hd) => (
                    <O
                      key={hd}
                      nhan={t(`hanhDong.${hd}`, hd)}
                      bat={oDaChon.has(khoaO(cn, hd))}
                      canNhac={canNhac.has(khoaO(cn, hd))}
                      moTa={moTaCanNhac(t, cn, hd, canNhac)}
                      chiDoc={chiDoc}
                      rongHet
                      onDoi={(bat) => onDoi(khoaO(cn, hd), bat)}
                    />
                  ))}
                </div>
              </>
            )}
          </section>
        )
      })}
    </div>
  )
}

/**
 * Mô tả hệ quả của một ô cần cân nhắc.
 *
 * Tra theo cặp cụ thể trước (`quyen.canNhac.DiemDanh.Chot`), không có thì lùi về mô tả chung
 * của thao tác (`quyen.canNhac.chung.Chot`). Nhờ vậy thêm một cặp vào `ChucNang.CanCanNhac`
 * có mô tả dùng được ngay, và muốn nói cụ thể hơn thì thêm khoá riêng — không phải sửa code.
 */
function moTaCanNhac(
  t: TFunction,
  cn: string,
  hd: string,
  canNhac: Set<string>,
): string | undefined {
  if (!canNhac.has(khoaO(cn, hd))) return undefined
  const rieng = t(`quyen.canNhac.${cn}.${hd}`, '')
  if (rieng) return rieng
  return t(`quyen.canNhac.chung.${hd}`, '') || undefined
}

/**
 * Một ô quyền — **nhãn bấm được**, không phải checkbox trần.
 *
 * Vì sao không dùng checkbox: checkbox 16px trong một bảng thưa là mục tiêu bấm nhỏ và không
 * mang chữ, nên phải đối chiếu với đầu cột mới biết mình đang bật gì. Nhãn bấm được mang luôn
 * tên thao tác, vùng bấm rộng cả nhãn, và trạng thái bật/tắt đọc được bằng màu nền thay vì
 * một dấu tích 4px.
 *
 * Vẫn là `<input type="checkbox">` thật ở bên dưới (`sr-only`) chứ không phải `<div onClick>`:
 * giữ được điều hướng bằng Tab, phím Space, và trình đọc màn hình đọc đúng trạng thái.
 */
function O({
  nhan,
  bat,
  canNhac,
  moTa,
  chiDoc,
  rongHet,
  onDoi,
}: {
  nhan: string
  bat: boolean
  canNhac?: boolean
  moTa?: string
  chiDoc?: boolean
  rongHet?: boolean
  onDoi: (bat: boolean) => void
}) {
  const vien = bat
    ? canNhac
      ? 'border-status-cho bg-status-cho/10'
      : 'border-primary bg-primary/10'
    : 'border-border bg-background hover:bg-muted'

  return (
    <label
      className={
        'flex cursor-pointer items-start gap-1.5 rounded-md border px-2 py-1 text-xs '
        + 'transition-colors has-[:focus-visible]:ring-2 has-[:focus-visible]:ring-ring '
        + (chiDoc ? 'cursor-default opacity-70 ' : '')
        + (rongHet ? 'w-full ' : '')
        + vien
      }
    >
      <input
        type="checkbox"
        className="sr-only"
        checked={bat}
        disabled={chiDoc}
        onChange={(e) => onDoi(e.target.checked)}
      />
      <span
        className={
          'mt-px flex h-3.5 w-3.5 shrink-0 items-center justify-center rounded-[3px] border '
          + (bat
            ? canNhac
              ? 'border-status-cho bg-status-cho text-white'
              : 'border-primary bg-primary text-primary-foreground'
            : 'border-muted-foreground/40')
        }
      >
        {bat && <Check className="h-2.5 w-2.5" strokeWidth={3.5} />}
      </span>
      <span className="min-w-0">
        <span className="font-medium">{nhan}</span>
        {canNhac && (
          <AlertTriangle
            className="ml-1 inline h-3 w-3 shrink-0 align-[-1px] text-status-cho"
            aria-hidden
          />
        )}
        {moTa && (
          <span className="mt-0.5 block font-normal leading-snug text-muted-foreground">
            {moTa}
          </span>
        )}
      </span>
    </label>
  )
}
