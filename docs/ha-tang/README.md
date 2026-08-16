# Hạ tầng

1 VPS (Ubuntu Server LTS) chạy toàn bộ qua Docker Compose, Caddy làm reverse proxy duy nhất expose
80/443 — xem [ADR-0004](../kien-truc/adr/0004-ha-tang-tu-host-vps.md).

| Nội dung | Tài liệu |
|---|---|
| Cài đặt VPS lần đầu (ufw, fail2ban, Docker) | [cai-dat-vps.md](./cai-dat-vps.md) |
| Giải thích từng biến trong `.env.example` | [bien-moi-truong.md](./bien-moi-truong.md) |
| Runbook xử lý sự cố (VPS down, DB đầy, restore) | [runbook.md](./runbook.md) |

## Sơ đồ triển khai

```
Internet → Caddy (chỉ service này expose 80/443, auto HTTPS Let's Encrypt)
             ├─ /       → Frontend (React build, Caddy phục vụ static trực tiếp)
             └─ /api/*  → Backend API (.NET, container)
                            ├─ PostgreSQL (container, KHÔNG expose port)
                            ├─ Redis (container, tuỳ chọn)
                            ├─ MinIO (container, presigned URL cho client)
                            ├─ SMTP / SMS Gateway (dịch vụ ngoài)
                            └─ Sentry (dịch vụ ngoài, error tracking)

Quan sát: Loki + Promtail (log) · Prometheus + Grafana (metrics) · Uptime Kuma (alert)
Backup:   cron pg_dump định kỳ, đẩy bản sao ra ngoài VPS
CI/CD:    GitHub Actions → build & test → image → ghcr.io → SSH `docker compose pull && up -d`
```

> **Nguyên tắc bất di bất dịch #5:** mọi service ngoài Caddy nằm trong Docker network nội bộ, **không mở
> port trực tiếp ra Internet**.

## File cấu hình liên quan

| File | Vai trò |
|---|---|
| [`docker-compose.yml`](../../docker-compose.yml) | Định nghĩa toàn bộ service |
| [`Caddyfile`](../../Caddyfile) | Reverse proxy + HTTPS tự động |
| [`.env.example`](../../.env.example) | Mẫu biến môi trường — copy thành `.env`, **không commit** |
| [`.github/workflows/deploy.yml`](../../.github/workflows/deploy.yml) | Pipeline CI/CD |

## Lộ trình scale

1. **MVP** — 1 VPS chạy toàn bộ qua Docker Compose *(trạng thái hiện tại)*.
2. **Tải tăng** — tách VPS riêng cho database.
3. **Cần HA** — thêm VPS thứ 3 chạy replica DB + load balancer.
4. **Chỉ khi vượt khả năng vận hành thủ công** — chuyển managed cloud/Kubernetes. Không nhảy thẳng lên
   Kubernetes khi Compose còn thừa sức.

## Kiểm tra liên kết tài liệu

```bash
python3 scripts/check-doc-links.py
```

Quét toàn bộ `.md`/`.html`, báo link nội bộ hỏng. Exit code 1 nếu có lỗi (dùng được trong CI). Chạy sau
mỗi đợt sửa nhiều file tài liệu.
