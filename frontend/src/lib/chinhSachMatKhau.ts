/**
 * Chính sách mật khẩu phía giao diện — **bản sao có chủ ý** của
 * `ChinhSachMatKhau.DoDaiToiThieu` bên backend (22/09/2026).
 *
 * Backend mới là nơi quyết định (nó chặn thật); ô `minLength` ở đây chỉ để người dùng biết
 * ngay lúc gõ thay vì gửi lên rồi nhận lỗi. Nhưng **phải khớp số**: đặt 6 trong khi backend
 * đòi 12 thì trình duyệt cho bấm Lưu rồi server mới từ chối — người dùng không hiểu vì sao
 * form "hợp lệ" lại lỗi.
 *
 * Trước đây số 6 nằm rải ở 5 ô `<Input minLength={6}>` khác nhau. Gom về đây để lần sau đổi
 * chính sách chỉ phải sửa một chỗ ở mỗi phía.
 *
 * Bản dịch `MAT_KHAU_QUA_NGAN` trong `i18n.ts` cũng nhắc con số này — nhớ sửa cùng.
 */
export const DO_DAI_MAT_KHAU_TOI_THIEU = 12
