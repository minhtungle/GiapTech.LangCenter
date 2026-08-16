# SECURITY.md — Chính sách bảo mật

## Phạm vi dữ liệu nhạy cảm

- Mật khẩu tài khoản (lưu hash, không bao giờ log/hiển thị dạng plaintext).
- Thông tin liên hệ cầu thủ (email, số điện thoại, địa chỉ).
- Dữ liệu tài chính (quỹ đội, tiến độ đóng góp từng thành viên).

## Nguyên tắc chung

- Cách ly dữ liệu tuyệt đối giữa các tenant (CLB) — mọi truy vấn nghiệp vụ đều lọc theo `tenant_id`.
- Mật khẩu băm bằng thuật toán chuẩn của ASP.NET Core Identity; bắt buộc đổi mật khẩu ở lần đăng nhập
  đầu tiên cho tài khoản admin mặc định.
- JWT có thời hạn ngắn + refresh token; không lưu token trong `localStorage` phía frontend nếu tránh
  được (ưu tiên httpOnly cookie khi khả thi).
- Secrets (connection string, khóa JWT, API key SMS/Email) lưu trong file `.env` **không commit vào
  Git**, quyền file `chmod 600` trên VPS.
- VPS: `ufw` chỉ mở cổng 22/80/443, SSH key-only (tắt đăng nhập bằng mật khẩu), `fail2ban`,
  `unattended-upgrades` bật tự động vá lỗi bảo mật hệ điều hành.
- Mọi service (DB, Redis, MinIO) chỉ nằm trong Docker network nội bộ, không expose port ra Internet.

## Báo cáo lỗ hổng bảo mật

Nếu phát hiện lỗ hổng bảo mật, liên hệ trực tiếp quản trị dự án, không tạo public issue mô tả chi tiết
cách khai thác.
