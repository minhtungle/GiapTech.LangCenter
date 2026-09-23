import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Check, Globe } from 'lucide-react'
import { NGON_NGU } from '@/lib/ngon-ngu/danhSach'
import { doiNgonNgu } from '@/lib/i18n'

/**
 * **Nút chuyển ngôn ngữ** (23/09/2026 — yêu cầu chủ sản phẩm).
 *
 * Đọc thẳng `NGON_NGU` nên thêm ngôn ngữ mới chỉ cần sửa một chỗ trong `danhSach.ts`, không
 * phải đụng component này.
 *
 * ## Vì sao hiện tên bằng CHÍNH ngôn ngữ đó
 *
 * Người Hàn tìm "한국어" nhanh hơn tìm "Tiếng Hàn" — nhất là khi họ đang lạc trong một giao
 * diện tiếng Việt và cần thoát ra. Kèm cờ để nhận ra bằng mắt, không phải đọc.
 *
 * ## Vì sao KHÔNG dùng `<select>`
 *
 * `<select>` gốc trên macOS/iOS vẽ theo hệ điều hành, không nhận emoji cờ nhất quán và không
 * theo được design token. Dựng bằng nút + danh sách thì hiển thị giống nhau trên mọi máy.
 */
export function ChonNgonNgu({
  gonGang = false,
  huong = 'len',
}: {
  gonGang?: boolean
  /**
   * Danh sách mở LÊN hay XUỐNG.
   *
   * Sidebar đặt nút ở đáy màn nên phải mở lên; màn đăng nhập đặt ở góc trên nên phải mở
   * xuống. Không có tham số này thì một trong hai chỗ có danh sách tràn ra ngoài màn hình —
   * thấy ngay khi chụp màn kiểm tra.
   */
  huong?: 'len' | 'xuong'
}) {
  const { i18n, t } = useTranslation()
  const [mo, setMo] = useState(false)

  const hienTai = NGON_NGU.find((n) => n.ma === i18n.language) ?? NGON_NGU[0]

  return (
    <div className="relative">
      <button
        type="button"
        onClick={() => setMo((v) => !v)}
        title={t('ngonNgu.doi')}
        aria-label={t('ngonNgu.doi')}
        aria-expanded={mo}
        className="flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-sm text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
      >
        <Globe className="h-4 w-4 shrink-0" />
        {/* `gonGang`: ở sidebar thu gọn chỉ còn icon, không đủ chỗ cho chữ. */}
        {!gonGang && (
          <span className="truncate">
            {hienTai.co} {hienTai.ten}
          </span>
        )}
      </button>

      {mo && (
        <>
          {/*
            Lớp phủ trong suốt để bấm ra ngoài là đóng.

            Dùng lớp phủ thay vì nghe `document.click`: nghe sự kiện toàn cục dễ bắt nhầm
            chính cú bấm đang mở menu, và phải nhớ gỡ listener khi unmount.
          */}
          <div className="fixed inset-0 z-40" onClick={() => setMo(false)} aria-hidden />

          <ul
            role="listbox"
            className={`absolute z-50 min-w-[10rem] overflow-hidden rounded-md border border-border bg-background py-1 shadow-lg ${
              huong === 'len' ? 'bottom-full left-0 mb-1' : 'left-auto right-0 top-full mt-1'
            }`}
          >
            {NGON_NGU.map((n) => (
              <li key={n.ma}>
                <button
                  type="button"
                  role="option"
                  aria-selected={n.ma === i18n.language}
                  onClick={() => {
                    doiNgonNgu(n.ma)
                    setMo(false)
                  }}
                  className="flex w-full items-center gap-2 px-3 py-1.5 text-left text-sm hover:bg-muted"
                >
                  <span className="w-5 shrink-0">{n.co}</span>
                  <span className="flex-1">{n.ten}</span>
                  {n.ma === i18n.language && (
                    <Check className="h-3.5 w-3.5 shrink-0 text-[hsl(var(--primary))]" />
                  )}
                </button>
              </li>
            ))}
          </ul>
        </>
      )}
    </div>
  )
}
