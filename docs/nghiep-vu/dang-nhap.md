# Module Đăng nhập (FR-01, FR-02)

## FR-01 — Đăng nhập

Xác thực bằng **bộ ba**: `{ID đội (tenant), tên đăng nhập, mật khẩu}`. Username chỉ duy nhất **trong
phạm vi một tenant** — hai CLB khác nhau có thể cùng có tài khoản `admin`.

### Luồng

1. Người dùng nhập ID đội + username + password.
2. Hệ thống resolve tenant từ ID đội; nếu không tồn tại → trả mã lỗi chung (không tiết lộ tenant nào tồn
   tại).
3. Xác thực username + password trong phạm vi tenant đó.
4. Nếu tài khoản có cờ `phai_doi_mk = true` → **chuyển hướng sang màn đổi mật khẩu trước khi vào hệ
   thống**, không cấp quyền truy cập chức năng nào khác.
5. Cấp access token (JWT, chứa claim `tenant_id`) + refresh token.

### Quy tắc

- Tài khoản admin mặc định mỗi tenant là `admin` / `123456` với `phai_doi_mk = true` — bắt buộc đổi ở
  lần đăng nhập đầu tiên.
- Thông báo lỗi đăng nhập sai **không phân biệt** "sai username" hay "sai password" — trả cùng một mã lỗi.
- Mật khẩu lưu hash bằng thuật toán chuẩn của ASP.NET Core Identity.

## FR-02 — Quên mật khẩu

1. Người dùng nhập email đã đăng ký (kèm ID đội để xác định tenant).
2. Hệ thống gửi email chứa **link/mã đặt lại mật khẩu có thời hạn**.
3. Người dùng đặt lại mật khẩu qua link; token hết hạn hoặc đã dùng thì không chấp nhận.

### Quy tắc

- Phản hồi cho người dùng **luôn giống nhau** dù email có tồn tại hay không — tránh dò email hợp lệ.
- Token đặt lại dùng một lần, có thời hạn ngắn.
- Kênh gửi: SMTP (xem [biến môi trường](../ha-tang/bien-moi-truong.md)).

## Tham chiếu

- Bảng `NGUOI_DUNG`, `TENANT` — xem [ERD](../database/erd.md).
- Cấu hình JWT & Identity — xem [SECURITY.md](../../SECURITY.md).
