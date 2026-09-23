/**
 * **Danh sách ngôn ngữ hệ thống hỗ trợ** (23/09/2026 — yêu cầu chủ sản phẩm).
 *
 * ## Chỉ dịch GIAO DIỆN, không dịch dữ liệu người dùng nhập
 *
 * Đổi ngôn ngữ chỉ đổi nhãn, nút, thông báo lỗi — **không** đụng tên lớp học, tên học viên,
 * ghi chú, nội dung bài tập. Đó là dữ liệu của trung tâm, dịch máy sẽ làm sai lệch (tên riêng
 * tiếng Việt qua tiếng Anh thành vô nghĩa), và mỗi trung tâm có cách gọi riêng.
 *
 * Đây cũng là cách `react-i18next` vốn hoạt động: chỉ những chuỗi đi qua `t()` mới đổi.
 *
 * ## Thêm ngôn ngữ mới cần làm gì
 *
 * 1. Thêm tệp `ngon-ngu/<mã>.ts`, dịch từ `vi.ts` (nguồn chân lý về cấu trúc khoá).
 * 2. Thêm một dòng vào `NGON_NGU` dưới đây.
 * 3. Chạy `python3 scripts/check-i18n-keys.py` — nó so mọi tệp với `vi.ts` và báo khoá thiếu.
 *
 * Không phải sửa chỗ nào khác: nút chuyển ngôn ngữ đọc thẳng từ danh sách này.
 */

export interface NgonNgu {
  /** Mã BCP 47 — dùng cho `i18next` **và** cho `Intl` (định dạng ngày, số, tiền). */
  ma: string

  /** Tên hiển thị **bằng chính ngôn ngữ đó** — người Hàn tìm "한국어" nhanh hơn "Tiếng Hàn". */
  ten: string

  /** Cờ dạng emoji: nhận ra bằng mắt nhanh hơn đọc chữ, và không cần tải ảnh. */
  co: string
}

/**
 * Thứ tự cố ý: tiếng Việt đầu (ngôn ngữ gốc, đa số người dùng), rồi tiếng Anh (phổ biến
 * nhất), rồi ba thứ tiếng châu Á theo yêu cầu của chủ sản phẩm.
 */
export const NGON_NGU: readonly NgonNgu[] = [
  { ma: 'vi', ten: 'Tiếng Việt', co: '🇻🇳' },
  { ma: 'en', ten: 'English', co: '🇬🇧' },
  { ma: 'zh', ten: '中文', co: '🇨🇳' },
  { ma: 'ko', ten: '한국어', co: '🇰🇷' },
  { ma: 'ja', ten: '日本語', co: '🇯🇵' },
] as const

/** Ngôn ngữ mặc định khi chưa chọn gì — cũng là `fallbackLng`. */
export const NGON_NGU_MAC_DINH = 'vi'

/** Khoá `localStorage` lưu lựa chọn. Giữ nguyên tên cũ để người đang dùng không bị reset. */
export const KHOA_NGON_NGU = 'lms_ngon_ngu'

/**
 * Mã `Intl` tương ứng, để định dạng **ngày, số và tiền** theo đúng ngôn ngữ đang chọn.
 *
 * Cần vì `Intl` muốn mã đầy đủ (`vi-VN`, `en-GB`) chứ không phải mã ngắn. Thiếu bước này thì
 * giao diện tiếng Anh nhưng ngày vẫn hiện `23/09/2026` kiểu Việt — nửa vời và gây nhầm với
 * định dạng Mỹ `09/23/2026`.
 *
 * Chọn `en-GB` (ngày/tháng/năm) chứ không `en-US`: người dùng hệ thống này quen thứ tự
 * ngày-trước, đổi sang tháng-trước dễ đọc nhầm hạn học phí.
 */
const INTL: Record<string, string> = {
  vi: 'vi-VN',
  en: 'en-GB',
  zh: 'zh-CN',
  ko: 'ko-KR',
  ja: 'ja-JP',
}

/** Đổi mã ngôn ngữ sang mã `Intl`. Không biết thì trả về chính nó — `Intl` tự xoay xở. */
export function maIntl(ma: string): string {
  return INTL[ma] ?? ma
}
