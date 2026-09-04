# Biến môi trường

Copy [`.env.example`](../../.env.example) thành `.env`, điền giá trị thật.

> **`.env` không bao giờ commit vào Git.** Trên VPS: `chmod 600 .env`.

## PostgreSQL

| Biến | Mô tả |
|---|---|
| `DOMAIN` | Domain để Caddy xin chứng chỉ HTTPS — **bắt buộc**, compose dừng nếu thiếu |
| `POSTGRES_DB` | Tên database. Mặc định `langcenter_lms` |
| `POSTGRES_USER` | User ứng dụng dùng để kết nối |
| `POSTGRES_PASSWORD` | Mật khẩu — **bắt buộc đổi**, sinh ngẫu nhiên đủ dài |

Database **không map port ra host** — chỉ truy cập qua Docker network nội bộ. Muốn kết nối từ máy cá
nhân để quản trị: dùng SSH tunnel.

## MinIO (object storage)

| Biến | Mô tả |
|---|---|
| `MINIO_ROOT_USER` | Tài khoản quản trị MinIO — **bắt buộc đổi** |
| `MINIO_ROOT_PASSWORD` | Mật khẩu quản trị — **bắt buộc đổi** |
| `MINIO_BUCKET` | Bucket chứa ảnh, mặc định `langcenter-lms-anh` — tạo tự động lần tải đầu |

Console MinIO (cổng 9001) không public — truy cập qua SSH tunnel khi cần quản trị. Client upload file
(ảnh đại diện cầu thủ, logo, ảnh bìa) qua **presigned URL**, không đi qua backend.

## JWT

| Biến | Mô tả |
|---|---|
| `JWT_SECRET` | Khóa ký token — **tối thiểu 32 ký tự**, sinh ngẫu nhiên. Đổi khóa này làm mọi token đang lưu hành mất hiệu lực |
| `JWT_ISSUER` | Định danh bên phát hành. Mặc định `langcenter-lms-api` |
| `JWT_EXPIRY_MINUTES` | Thời hạn access token (phút). Giữ ngắn, dùng refresh token để gia hạn |

## SMTP (email — quên mật khẩu, nhắc đóng quỹ)

| Biến | Mô tả |
|---|---|
| `SMTP_HOST` | Máy chủ SMTP. Mặc định `smtp.sendgrid.net` |
| `SMTP_PORT` | Cổng. Mặc định `587` (STARTTLS) |
| `SMTP_USER` | Với SendGrid là chuỗi cố định `apikey` |
| `SMTP_PASSWORD` | API key thực tế của nhà cung cấp |

Dùng cho [FR-02 quên mật khẩu](../nghiep-vu/dang-nhap.md#fr-02--quên-mật-khẩu) và
[FR-16 nhắc đóng quỹ](../nghiep-vu/tai-chinh.md#nhắc-nhở).

## SMS Gateway

| Biến | Mô tả |
|---|---|
| `SMS_PROVIDER` | Nhà cung cấp: `esms` hoặc `speedsms` |
| `SMS_API_KEY` | API key |
| `SMS_SECRET_KEY` | Secret key |

Dùng cho nhắc nhở đóng quỹ (FR-16).

## Sentry (error tracking, tùy chọn)

| Biến | Mô tả |
|---|---|
| `SENTRY_DSN` | Để trống nếu chưa dùng Sentry |

## ASP.NET Core

| Biến | Mô tả |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` trên VPS, `Development` khi chạy local |
| `ConnectionStrings__Default` | Chuỗi kết nối PostgreSQL. Host là **`postgres`** (tên service trong Docker network), không phải `localhost` |

Dấu `__` (hai gạch dưới) là quy ước của ASP.NET Core để biểu diễn cấu trúc lồng nhau
(`ConnectionStrings:Default`) qua biến môi trường.

## Checklist trước khi deploy lần đầu

- [ ] Mọi giá trị `doi-gia-tri-nay` trong `.env` đã được thay bằng giá trị thật, sinh ngẫu nhiên.
- [ ] `chmod 600 .env` trên VPS.
- [ ] `.env` nằm trong `.gitignore`.
- [ ] Domain thật đã thay `langcenter-lms.example.com` trong [`Caddyfile`](../../Caddyfile).
- [ ] Secrets CI đã cấu hình trên GitHub: `VPS_HOST`, `VPS_USER`, `VPS_SSH_KEY`.
