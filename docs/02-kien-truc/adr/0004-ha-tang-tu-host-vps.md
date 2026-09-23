# ADR-0004: Hạ tầng triển khai tự host trên 1 VPS (Docker Compose + Nginx)

> **Sửa đổi 05/09/2026 — reverse proxy: Caddy → Nginx + certbot.**
>
> Bản gốc (16/08/2026) chọn Caddy. Khi triển khai thật, VPS đích **đã có Nginx + certbot** phục vụ
> nhiều domain của các dự án khác: thêm Caddy sẽ tranh port 80/443 với Nginx đang chạy. Nên dự án
> dùng lại Nginx có sẵn — xem chú thích đầu `docker-compose.yml`.
>
> Phần còn lại của ADR (Docker Compose, MinIO, chỉ expose reverse proxy, backup `pg_dump`) **giữ
> nguyên hiệu lực**. Ghi sửa đổi tại chỗ thay vì viết ADR mới vì đây là đổi **một lựa chọn công
> cụ** do ràng buộc môi trường, không phải đổi hướng kiến trúc.

## Bối cảnh
Triển khai ban đầu quy mô nhỏ (một vài trung tâm), ưu tiên chi phí thấp, chưa cần khả năng chịu
tải lớn. Cần chuẩn hoá cách tổ chức hạ tầng để dễ mở rộng sau này mà không phải đập đi làm lại.

## Quyết định
- 1 VPS (Ubuntu Server LTS) chạy toàn bộ qua **Docker Compose**: Backend API, Frontend (static),
  PostgreSQL, Redis (tuỳ chọn), MinIO.
- **Nginx + certbot** (cài trực tiếp trên VPS, dùng chung với các dự án khác) làm reverse proxy duy
  nhất expose port 80/443; certbot cấp/gia hạn HTTPS qua Let's Encrypt.
- Mọi service khác nằm trong Docker network nội bộ, **không expose port ra Internet**.
- Frontend build ra static assets (SPA) — Nginx phục vụ trực tiếp, không cần container/Node runtime
  riêng chỉ để chạy file tĩnh.
- Object storage: **MinIO** (self-host, tương thích S3), dùng presigned URL để client upload thẳng,
  không qua backend.
- CI/CD: GitHub Actions build image → push **ghcr.io** → SSH vào VPS chạy
  `docker compose pull && up -d`.
- Quan sát hệ thống: Grafana Loki + Promtail (log), Prometheus + Grafana (metrics qua OpenTelemetry),
  Uptime Kuma (uptime/alert). Error tracking dùng Sentry (cloud free/nhỏ) như ngoại lệ hợp lý.
- Bảo mật VPS: `ufw` (chỉ mở 22/80/443), SSH key-only, `fail2ban`, `unattended-upgrades`.
- Backup: cron job `pg_dump` định kỳ, đẩy bản sao ra ngoài VPS.

## Phương án đã cân nhắc
- **Caddy** thay Nginx — là lựa chọn của bản ADR gốc (HTTPS tự động, ít cấu hình hơn). Bỏ khi triển
  khai thật vì VPS đích đã có Nginx phục vụ nhiều domain khác; chạy hai reverse proxy trên cùng máy
  sẽ tranh port 80/443.
- **Managed cloud (AWS/GCP) ngay từ đầu** — loại bỏ vì chi phí không cần thiết ở quy mô hiện tại; VPS +
  Docker Compose đã đủ đáp ứng, tránh over-engineering.
- **Kubernetes** — loại bỏ ở giai đoạn này vì vượt quá nhu cầu vận hành thủ công của team nhỏ; chỉ xem
  xét lại khi vượt quá khả năng quản lý của 1–2 VPS Docker Compose (xem lộ trình scale bên dưới).
- **S3/Cloudflare R2 managed** thay MinIO — cân nhắc là lựa chọn hợp lệ tương đương; MinIO được ưu tiên
  vì giữ toàn bộ dữ liệu tự host, không phụ thuộc dịch vụ ngoài cho MVP; có thể đổi sang R2/S3 sau mà
  không đổi code (cùng chuẩn S3 API).

## Hệ quả
- (+) Chi phí vận hành thấp nhất có thể ở giai đoạn MVP.
- (+) Toàn bộ ngăn xếp có thể di chuyển nguyên vẹn sang VPS/nhà cung cấp khác (đóng gói Docker).
- (−) Team tự chịu trách nhiệm bảo mật hạ tầng (không có Key Vault/WAF managed sẵn như cloud) — bù bằng
  `ufw`/`fail2ban`/`unattended-upgrades`.
- (−) Không có high-availability ở giai đoạn MVP (1 VPS là điểm lỗi đơn) — chấp nhận đánh đổi này cho
  quy mô hiện tại; xem lộ trình scale bên dưới khi cần nâng cấp.

## Lộ trình scale (tham chiếu)
1. MVP: 1 VPS chạy toàn bộ qua Docker Compose (trạng thái hiện tại).
2. Tải tăng: tách VPS riêng cho database.
3. Cần HA: thêm VPS thứ 3 chạy replica DB + load balancer trước các VPS app.
4. Chỉ chuyển sang managed cloud/Kubernetes khi vượt quá khả năng vận hành thủ công của team — không
   nhảy thẳng lên Kubernetes khi 1 VPS Compose còn thừa sức.
