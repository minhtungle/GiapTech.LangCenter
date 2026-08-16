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

## Phiên đăng nhập & refresh token

| Cơ chế | Quyết định | Vì sao |
|---|---|---|
| Lưu trữ | DB giữ **hash SHA-256**, không giữ token thô | Người đọc được DB (backup rò rỉ, SQL injection) không mạo danh được ai |
| Xoay vòng | Token cũ **thu hồi ngay** khi cấp token mới | Bản sao bị lộ chỉ dùng được tới lần làm mới kế tiếp, không sống tới ngày hết hạn |
| Phát hiện đánh cắp | Dùng lại token **đã thu hồi** → thu hồi **toàn bộ** phiên | Tái sử dụng là dấu hiệu có bản sao trong tay người khác; thà buộc đăng nhập lại còn hơn để phiên bị chiếm chạy tiếp |
| Đổi mật khẩu | Thu hồi mọi phiên đang mở | Đổi mật khẩu thường là phản ứng khi nghi bị lộ |
| Hạn | Access 60 phút · Refresh 30 ngày | |

## Trạng thái triển khai

| Endpoint | Mã | Ghi chú |
|---|---|---|
| `POST /api/v1/auth/dang-nhap` | FR-01 | Trả `phaiDoiMatKhau` để frontend điều hướng |
| `POST /api/v1/auth/doi-mat-khau` | FR-01 | Người dùng tự đổi; thu hồi phiên cũ |
| `POST /api/v1/auth/lam-moi-token` | FR-01 | Xoay vòng + phát hiện tái sử dụng |
| `POST /api/v1/auth/quen-mat-khau` | FR-02 | **Luôn trả 204** dù email có tồn tại hay không |
| `POST /api/v1/auth/dat-lai-mat-khau` | FR-02 | Token hạn 30 phút, dùng **một lần**, thu hồi mọi phiên |

Chưa cấu hình `SMTP_HOST` (môi trường dev) thì email được ghi log thay vì gửi — luồng vẫn chạy
đầu-cuối mà không cần dựng SMTP thật.

## Tham chiếu

- Bảng `NGUOI_DUNG`, `TENANT`, `REFRESH_TOKEN`, `TOKEN_DATLAI_MATKHAU` — xem [ERD](../database/erd.md).
- Cấu hình JWT & Identity — xem [SECURITY.md](../../SECURITY.md).
