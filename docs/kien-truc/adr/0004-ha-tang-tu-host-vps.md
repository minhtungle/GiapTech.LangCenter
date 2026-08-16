# ADR-0004: Hạ tầng triển khai tự host trên 1 VPS (Docker Compose + Caddy)

## Bối cảnh
Triển khai ban đầu quy mô nhỏ (1 CLB), ưu tiên chi phí thấp, chưa cần khả năng chịu tải lớn. Cần chuẩn
hoá cách tổ chức hạ tầng để dễ mở rộng sau này mà không phải đập đi làm lại.

## Quyết định
- 1 VPS (Ubuntu Server LTS) chạy toàn bộ qua **Docker Compose**: Caddy, Backend API, Frontend (static),
  PostgreSQL, Redis (tuỳ chọn), MinIO.
- **Caddy** làm reverse proxy duy nhất expose port 80/443; tự động cấp/gia hạn HTTPS qua Let's Encrypt.
- Mọi service khác nằm trong Docker network nội bộ, **không expose port ra Internet**.
- Frontend build ra static assets (SPA) — Caddy phục vụ trực tiếp, không cần container/Node runtime
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
- **Nginx + Certbot** thay Caddy — loại bỏ vì Caddy tự động hoá HTTPS tốt hơn, ít cấu hình hơn cho team
  nhỏ không có người chuyên trách hạ tầng.
- **Managed cloud (AWS/GCP) ngay từ đầu** — loại bỏ vì chi phí không cần thiết ở quy mô 1 CLB; VPS +
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
  quy mô 1 CLB; xem lộ trình scale bên dưới khi cần nâng cấp.

## Lộ trình scale (tham chiếu)
1. MVP: 1 VPS chạy toàn bộ qua Docker Compose (trạng thái hiện tại).
2. Tải tăng: tách VPS riêng cho database.
3. Cần HA: thêm VPS thứ 3 chạy replica DB + load balancer trước các VPS app.
4. Chỉ chuyển sang managed cloud/Kubernetes khi vượt quá khả năng vận hành thủ công của team — không
   nhảy thẳng lên Kubernetes khi 1 VPS Compose còn thừa sức.
