# GiapTech.SoccerRoom — Hệ thống Quản lý Câu lạc bộ đá bóng

> README này chỉ là **mục lục điều hướng** — không lặp lại nội dung chi tiết. Đọc
> [`CLAUDE.md`](./CLAUDE.md) trước khi chỉnh sửa code hoặc tài liệu trong repo này.

## Bắt đầu từ đâu

| Bạn cần biết gì | Đọc ở đâu |
|---|---|
| Quy tắc bắt buộc khi làm việc với repo (đặc biệt cho AI agent) | [`CLAUDE.md`](./CLAUDE.md) |
| Tổng quan nghiệp vụ (đọc 1 mạch trước khi đào sâu) | [`docs/tong-thuat.md`](./docs/tong-thuat.md) |
| Quy trình Git, commit, PR | [`CONTRIBUTING.md`](./CONTRIBUTING.md) |
| Chính sách bảo mật | [`SECURITY.md`](./SECURITY.md) |
| Nhật ký thay đổi | [`CHANGELOG.md`](./CHANGELOG.md) |

## Bản đồ tài liệu

| Chủ đề | Thư mục | Nội dung chính |
|---|---|---|
| Kiến trúc & công nghệ | [`docs/kien-truc/`](./docs/kien-truc/TONG-QUAN-KIEN-TRUC.md) | Bản đồ công nghệ, [ADR](./docs/kien-truc/adr/), [thuật ngữ](./docs/kien-truc/THUAT-NGU.md) |
| Nghiệp vụ theo module | [`docs/nghiep-vu/`](./docs/nghiep-vu/README.md) | 16 mã FR: [đăng nhập](./docs/nghiep-vu/dang-nhap.md) · [quản trị](./docs/nghiep-vu/quan-tri-he-thong.md) · [lịch thi đấu](./docs/nghiep-vu/lich-thi-dau.md) · [thống kê](./docs/nghiep-vu/thong-ke.md) · [tài chính](./docs/nghiep-vu/tai-chinh.md) |
| Dữ liệu | [`docs/database/`](./docs/database/README.md) | [ERD 16 bảng](./docs/database/erd.md), [quy ước migration](./docs/database/quy-uoc-migration.md) |
| Backend | [`docs/backend/`](./docs/backend/README.md) | [Clean Architecture](./docs/backend/clean-architecture.md) · [CQRS](./docs/backend/cqrs-mediatr.md) · [multi-tenant](./docs/backend/multi-tenant.md) · [phân quyền động](./docs/backend/phan-quyen-dong.md) |
| Frontend | [`docs/frontend/`](./docs/frontend/README.md) | [Nguyên tắc UI/UX](./docs/frontend/ui-ux-nguyen-tac.md), [design token](./docs/frontend/design-tokens.md) |
| Hạ tầng & vận hành | [`docs/ha-tang/`](./docs/ha-tang/README.md) | [Cài đặt VPS](./docs/ha-tang/cai-dat-vps.md) · [biến môi trường](./docs/ha-tang/bien-moi-truong.md) · [runbook](./docs/ha-tang/runbook.md) |
| Hướng dẫn sử dụng | [`docs/huong-dan-su-dung/`](./docs/huong-dan-su-dung/README.md) | Theo vai trò (viết sau khi có UI thật) |

## Trạng thái quyết định

| Hạng mục | Đã chốt | ADR liên quan |
|---|---|---|
| Backend | ASP.NET Core (.NET 8), Clean Architecture + CQRS/MediatR | [ADR-0001](./docs/kien-truc/adr/0001-lua-chon-cong-nghe.md) |
| Database | PostgreSQL, multi-tenant shared-schema (cột `tenant_id`) | [ADR-0001](./docs/kien-truc/adr/0001-lua-chon-cong-nghe.md) |
| Frontend | React + TypeScript trên nền shadcn-admin (Vite + Tailwind + shadcn/ui) | [ADR-0002](./docs/kien-truc/adr/0002-frontend-shadcn-admin.md) |
| API versioning | URL segment `/api/v1/...`, Asp.Versioning.Mvc | [ADR-0003](./docs/kien-truc/adr/0003-api-versioning.md) |
| Triển khai | 1 VPS, Docker Compose, Caddy reverse proxy, MinIO object storage | [ADR-0004](./docs/kien-truc/adr/0004-ha-tang-tu-host-vps.md) |

## Stack tóm tắt

**Backend:** ASP.NET Core 8 · EF Core · PostgreSQL · Redis (tuỳ chọn) · JWT + ASP.NET Core Identity
**Frontend:** React + TypeScript · shadcn-admin (Vite, Tailwind, shadcn/ui) · TanStack Query/Table · React Hook Form + Zod · Recharts
**Hạ tầng:** Docker Compose · Caddy · MinIO · Prometheus/Grafana · Loki · Uptime Kuma · GitHub Actions → ghcr.io

## Trạng thái dự án

🚧 **Giai đoạn tài liệu.** Chưa khởi tạo mã nguồn. Bước kế tiếp: bootstrap checklist ở
[`CLAUDE.md`](./CLAUDE.md) mục 13.

## Kiểm tra liên kết tài liệu

```bash
python3 scripts/check-doc-links.py
```
