import i18n from '@/lib/i18n'
import { maIntl } from './danhSach'

/**
 * **Định dạng ngày / số / tiền theo ngôn ngữ đang chọn** (23/09/2026).
 *
 * ## Vấn đề nó chữa
 *
 * Trước đây 12 tệp gọi thẳng `toLocaleDateString('vi-VN')`. Đổi giao diện sang tiếng Anh thì
 * chữ đổi nhưng ngày vẫn ra `23/09/2026` kiểu Việt — nửa vời, và tệ hơn là **gây đọc nhầm**:
 * người quen định dạng Mỹ sẽ hiểu thành 9 tháng 23.
 *
 * ## Vì sao đọc `i18n.language` thay vì nhận tham số
 *
 * Nhiều chỗ gọi là hàm thuần ngoài component (`lopHocTypes.ts`, `crmTypes.ts`), không dùng
 * hook được. Truyền locale xuống từng chỗ nghĩa là sửa chữ ký của hàng chục hàm và mọi nơi
 * gọi chúng — nhiều chỗ để quên.
 *
 * Đọc thẳng từ `i18n` thì đổi ngôn ngữ là mọi định dạng đổi theo, không sót chỗ nào.
 *
 * ⚠️ **Đánh đổi đã biết**: các hàm này không còn "thuần" (cùng input, khác output khi đổi
 * ngôn ngữ). Chấp nhận được vì bản thân việc định dạng vốn phụ thuộc ngôn ngữ; nhưng **đừng
 * cache kết quả** của chúng qua các lần đổi ngôn ngữ.
 */

/** Locale hiện tại dạng `Intl` (`vi-VN`, `en-GB`…). */
export function locale(): string {
  return maIntl(i18n.language)
}

/** Ngày ngắn: `23/09/2026` (vi) · `23/09/2026` (en-GB) · `2026/9/23` (ja). */
export function ngay(iso?: string | null): string {
  if (!iso) return '—'
  return new Date(iso).toLocaleDateString(locale())
}

/** Ngày + giờ đầy đủ. */
export function ngayGio(iso?: string | null): string {
  if (!iso) return '—'
  return new Date(iso).toLocaleString(locale())
}

/** Giờ ngắn `09:00`, không hiện giây. */
export function gio(d: Date): string {
  return d.toLocaleTimeString(locale(), { hour: '2-digit', minute: '2-digit' })
}

/** Số có phân cách hàng nghìn theo locale. */
export function so(n: number): string {
  return n.toLocaleString(locale())
}

/**
 * Tiền VND.
 *
 * Luôn hiện **₫** dù giao diện đang ngôn ngữ nào: đây là số tiền THẬT của trung tâm, đổi ký
 * hiệu tiền tệ theo ngôn ngữ sẽ nói sai đơn vị. Chỉ phần **phân cách hàng nghìn** đổi theo
 * locale, vì đó là cách đọc số chứ không phải đổi giá trị.
 */
export function tien(n?: number | null): string {
  if (n === null || n === undefined) return '—'
  return n.toLocaleString(locale()) + ' ₫'
}
