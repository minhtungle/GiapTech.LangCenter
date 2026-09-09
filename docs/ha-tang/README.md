# Hạ tầng

1 VPS (Ubuntu Server LTS) chạy toàn bộ qua Docker Compose, Caddy làm reverse proxy duy nhất expose
80/443 — xem [ADR-0004](../kien-truc/adr/0004-ha-tang-tu-host-vps.md).

| Nội dung | Tài liệu |
|---|---|
| Cài đặt VPS lần đầu (ufw, fail2ban, Docker) | [cai-dat-vps.md](./cai-dat-vps.md) |
| **Triển khai** bằng `git pull` + build tại chỗ — luồng đang dùng | [trien-khai-pull-code.md](./trien-khai-pull-code.md) |
| Prompt giao cho Claude trên VPS (copy & dán) | [prompt-trien-khai-vps.md](./prompt-trien-khai-vps.md) |
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

## Chạy toàn hệ thống

```bash
cp .env.example .env          # rồi điền giá trị thật
docker compose up -d          # production
docker compose ps             # api phải ở trạng thái (healthy)
```

Ở máy dev, dùng thêm lớp phủ để Caddy chạy HTTP trên `localhost:8080` (không xin cert) và
mở port PostgreSQL/MinIO cho tiện xem dữ liệu:

```bash
cd frontend && npm run build && cd ..     # Caddy phục vụ ./frontend/dist
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d --build
```

| Địa chỉ (dev) | Nội dung |
|---|---|
| http://localhost:8080 | Frontend |
| http://localhost:8080/api/v1/... | API qua Caddy |
| http://localhost:8080/health | Healthcheck |
| localhost:55432 | PostgreSQL (chỉ ở dev) |
| http://localhost:59001 | MinIO console (chỉ ở dev) |

**Quan sát hệ thống** (Grafana/Loki/Prometheus/Uptime Kuma) nằm trong profile riêng để
`docker compose up` mặc định chỉ chạy thứ cần cho ứng dụng:

```bash
docker compose --profile quan-sat up -d
```

### Những điều đã xử lý sẵn

- **Migration tự chạy lúc khởi động** API. Đủ dùng cho một VPS; nếu về sau chạy nhiều bản sao
  API thì phải tách thành bước riêng vì nhiều instance sẽ tranh nhau migrate. Tắt bằng
  `TU_DONG_MIGRATE=false`.
- **API chờ PostgreSQL healthy** rồi mới khởi động — khởi động sớm sẽ chết ngay lúc migrate.
- **Không redirect HTTPS trong container** (`SAU_REVERSE_PROXY=true`): TLS đã kết thúc ở Caddy,
  bật redirect sẽ đá cả healthcheck lẫn request thật sang cổng container không nghe.
- **API chạy bằng user thường**, không phải root.

## File cấu hình liên quan

| File | Vai trò |
|---|---|
| [`docker-compose.yml`](../../docker-compose.yml) | Định nghĩa toàn bộ service (production) |
| [`docker-compose.dev.yml`](../../docker-compose.dev.yml) | Lớp phủ cho máy dev |
| [`src/GiapTech.LangCenter.API/Dockerfile`](../../src/GiapTech.LangCenter.API/Dockerfile) | Image API, 2 giai đoạn build/runtime |
| [`Caddyfile`](../../Caddyfile) | Reverse proxy + HTTPS tự động (production) |
| [`Caddyfile.dev`](../../Caddyfile.dev) | HTTP localhost cho dev |
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
