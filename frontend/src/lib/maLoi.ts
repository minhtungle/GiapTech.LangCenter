import axios from 'axios'

/**
 * Mã lỗi backend trả về — frontend tự dịch (quy tắc #3).
 *
 * ## Lỗi kiểm tra đầu vào trả về mã CỤ THỂ, không phải `DU_LIEU_KHONG_HOP_LE`
 *
 * FluentValidation trả hai tầng: `errorCode` là `DU_LIEU_KHONG_HOP_LE` cho mọi lỗi nhập
 * liệu, còn lý do THẬT nằm trong `duLieu.truong`:
 *
 * ```json
 * { "errorCode": "DU_LIEU_KHONG_HOP_LE",
 *   "duLieu": { "truong": { "TaiKhoan.MatKhau": ["MAT_KHAU_QUA_NGAN"] } } }
 * ```
 *
 * Chỉ đọc tầng ngoài thì người dùng nhận "Dữ liệu nhập vào chưa hợp lệ" — không nói được sai
 * ở ô nào, sai thế nào. Trong khi bản dịch cụ thể ĐÃ CÓ SẴN ở `vi.ts`
 * (`MAT_KHAU_QUA_NGAN` → "Mật khẩu phải có ít nhất 12 ký tự") mà không bao giờ được dùng.
 *
 * Xảy ra thật 30/09/2026: tạo tài khoản với mật khẩu 6 ký tự chỉ báo "dữ liệu không hợp lệ",
 * người dùng không đoán được là do chính sách 12 ký tự (nâng từ 6 lên hôm 22/09).
 *
 * Nay lấy mã cụ thể **đầu tiên** khi có. Nhiều trường cùng sai thì hiện cái đầu — người dùng
 * sửa xong bấm lại sẽ thấy cái tiếp theo; gộp hết vào một dòng thì không đọc nổi.
 *
 * `layDuLieuLoi` vẫn giữ để màn nào cần chi tiết hơn (tên trường, danh sách đầy đủ) tự đọc.
 */
export function layMaLoi(error: unknown): string {
  if (!axios.isAxiosError(error)) return 'LOI_HE_THONG'

  const data = error.response?.data as
    | { errorCode?: string; duLieu?: { truong?: Record<string, string[]> } }
    | undefined

  const truong = data?.duLieu?.truong
  if (truong) {
    for (const ma of Object.values(truong)) {
      // Chỉ nhận mã lỗi DẠNG HẰNG (`MAT_KHAU_QUA_NGAN`). FluentValidation trả câu tiếng Anh
      // mặc định khi rule không khai `WithErrorCode` — hiện thẳng câu đó lên giao diện tiếng
      // Việt còn tệ hơn thông báo chung chung.
      const cuThe = ma?.find((x) => /^[A-Z][A-Z0-9_]*$/.test(x))
      if (cuThe) return cuThe
    }
  }

  return data?.errorCode ?? 'LOI_HE_THONG'
}
