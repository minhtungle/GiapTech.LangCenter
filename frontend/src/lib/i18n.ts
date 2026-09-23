import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import { KHOA_NGON_NGU, NGON_NGU, NGON_NGU_MAC_DINH } from './ngon-ngu/danhSach'
import vi from './ngon-ngu/vi'
import en from './ngon-ngu/en'
import zh from './ngon-ngu/zh'
import ko from './ngon-ngu/ko'
import ja from './ngon-ngu/ja'

/**
 * Đa ngôn ngữ (quy tắc #3): API trả **MÃ LỖI**, frontend tra bảng dịch.
 *
 * Từ 23/09/2026 hỗ trợ 5 ngôn ngữ — xem `ngon-ngu/danhSach.ts` về cách thêm ngôn ngữ mới.
 * Bảng dịch tách theo tệp trong `ngon-ngu/`; `vi.ts` là **nguồn chân lý về cấu trúc khoá**.
 *
 * ## Chỉ dịch GIAO DIỆN
 *
 * Chỉ chuỗi đi qua `t()` mới đổi. Tên lớp, tên học viên, ghi chú — dữ liệu người dùng nhập —
 * giữ nguyên. Đó là yêu cầu của chủ sản phẩm và cũng là cách i18n vốn hoạt động.
 *
 * ## Vì sao gộp thẳng cả 5 ngôn ngữ vào bundle
 *
 * Tải lười từng tệp sẽ làm giao diện **nhấp nháy chữ chưa dịch** ở lần đổi đầu tiên, và thêm
 * một trạng thái chờ phải xử lý ở mọi màn. Mỗi tệp ~25 KB thô, gzip xuống rất nhỏ vì văn bản
 * nén rất tốt — không đáng đánh đổi.
 */

/** Lấy ngôn ngữ đã lưu; bỏ giá trị lạ để không rơi vào ngôn ngữ không tồn tại. */
function ngonNguDaLuu(): string {
  try {
    const luu = localStorage.getItem(KHOA_NGON_NGU)
    if (luu && NGON_NGU.some((n) => n.ma === luu)) return luu
  } catch {
    // Chế độ riêng tư chặn storage — dùng mặc định.
  }
  return NGON_NGU_MAC_DINH
}

void i18n.use(initReactI18next).init({
  resources: {
    vi: { translation: vi },
    en: { translation: en },
    zh: { translation: zh },
    ko: { translation: ko },
    ja: { translation: ja },
  },
  lng: ngonNguDaLuu(),
  fallbackLng: NGON_NGU_MAC_DINH,
  interpolation: { escapeValue: false },
})

/**
 * Đổi ngôn ngữ và **nhớ lựa chọn** cho lần sau.
 *
 * Gộp hai việc vào một hàm để không có chỗ nào gọi `i18n.changeLanguage` mà quên lưu — lỗi đó
 * biểu hiện là "đổi xong, F5 lại về tiếng Việt", rất khó chịu mà không rõ vì sao.
 */
export function doiNgonNgu(ma: string) {
  void i18n.changeLanguage(ma)
  try {
    localStorage.setItem(KHOA_NGON_NGU, ma)
  } catch {
    // Không lưu được thì vẫn đổi cho phiên này.
  }
}

export default i18n
