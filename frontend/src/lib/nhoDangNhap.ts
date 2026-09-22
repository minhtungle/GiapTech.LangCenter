/**
 * "Nhớ đăng nhập" — điền sẵn **mã trung tâm + tên đăng nhập** ở lần mở trang sau.
 *
 * ## Vì sao KHÔNG lưu mật khẩu
 *
 * Chủ sản phẩm yêu cầu *"thêm nút nhớ mật khẩu"* (22/09/2026) và khi được hỏi đã chọn phương án
 * **không đụng tới mật khẩu**. Lý do kỹ thuật đứng sau lựa chọn đó:
 *
 * - `localStorage` đọc được bằng JavaScript cùng origin. Một lỗ XSS đọc token thì token còn
 *   **thu hồi được** (đổi mật khẩu thu hồi mọi refresh token — `DoiMatKhauCommand`), còn mật
 *   khẩu thô thì mở được cả những nơi người dùng dùng lại nó.
 * - Trình duyệt đã có sẵn chức năng lưu mật khẩu, mã hoá theo tài khoản hệ điều hành, và các ô
 *   trên form đã khai `autoComplete="username" / "current-password"` đúng chuẩn để nó nhận ra.
 *   Tự dựng lại chức năng đó bằng `localStorage` là làm tệ hơn thứ đã có.
 *
 * Phiền toái thật mà người dùng gặp không phải mật khẩu — mà là **mã 7 ký tự vô nghĩa**
 * (`W686AE9`) phải gõ lại mỗi lần. Đó là thứ được nhớ ở đây.
 *
 * ## Vì sao không bỏ hẳn, để trình duyệt lo tất
 *
 * Trình duyệt điền username khi người dùng bấm vào ô, nhưng **không** điền `maTrungTam` một
 * cách đáng tin (nó là `autoComplete="organization"`, ít trình duyệt lưu). Và không phải ai
 * cũng bật lưu mật khẩu.
 *
 * ## Không bật mặc định
 *
 * Trung tâm có máy dùng chung ở quầy lễ tân. Điền sẵn tên đăng nhập của người trước là nói cho
 * người sau biết ai vừa dùng máy, nên ô này **mặc định tắt** và người dùng phải tự tích.
 */

const KHOA = 'lms_nho_dang_nhap'

export interface DangNhapDaNho {
  maTrungTam: string
  username: string
}

/**
 * Đọc thông tin đã nhớ. Trả `null` khi chưa nhớ gì, hoặc khi `localStorage` không dùng được
 * (chế độ ẩn danh của Safari ném lỗi khi ghi) — không để một lỗi lưu trữ làm trắng màn đăng
 * nhập, vì đây chỉ là tiện ích.
 */
export function docDaNho(): DangNhapDaNho | null {
  try {
    const raw = localStorage.getItem(KHOA)
    if (!raw) return null
    const doc = JSON.parse(raw) as Partial<DangNhapDaNho>
    // Dữ liệu cũ/hỏng: thiếu một trong hai thì coi như chưa nhớ gì. Không cố vá từng phần —
    // điền nửa vời khó hiểu hơn là để trống.
    if (typeof doc.maTrungTam !== 'string' || typeof doc.username !== 'string') return null
    if (!doc.maTrungTam || !doc.username) return null
    return { maTrungTam: doc.maTrungTam, username: doc.username }
  } catch {
    return null
  }
}

/** Ghi nhớ sau khi đăng nhập THÀNH CÔNG — nhớ bộ sai thì lần sau lại phải xoá tay. */
export function luuDaNho(thongTin: DangNhapDaNho) {
  try {
    localStorage.setItem(KHOA, JSON.stringify(thongTin))
  } catch {
    // Hết dung lượng hoặc bị chặn: bỏ qua. Người dùng chỉ mất tiện ích, không mất gì khác.
  }
}

/** Quên — gọi khi người dùng bỏ tích ô, kể cả lúc chưa từng nhớ gì (idempotent). */
export function xoaDaNho() {
  try {
    localStorage.removeItem(KHOA)
  } catch {
    // như trên
  }
}
