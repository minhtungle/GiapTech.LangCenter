# GiapTech.LangCenter — Hệ thống Quản lý Trung tâm Ngoại ngữ

> **27/28 mã FR chạy đầu-cuối** trên PostgreSQL + MinIO thật · 464 test backend · 23 E2E xanh.
> Còn lại: FR-27 (bài tập cho khoá trực tuyến) và Bài kiểm tra (có schema, chưa có API/UI).
>
> Hệ thống multi-tenant — mỗi trung tâm là một tenant độc lập — gồm **ba hệ thống con** chia theo
> nhóm quyền: **HRM** (nhân sự) · **CRM** (khách hàng) · **LMS** (đào tạo). Đây là cách nhóm chức
> năng để lọc giao diện, **không phải ba ứng dụng**: một API, một database, một lần đăng nhập —
> chốt ở [ADR-0005](docs/02-kien-truc/adr/0005-mot-source-va-doi-ten-langcenter.md).

> README này chỉ là **mục lục điều hướng** — không lặp lại nội dung chi tiết. Đọc
> [`CLAUDE.md`](./CLAUDE.md) trước khi chỉnh sửa code hoặc tài liệu trong repo này.

## Bắt đầu từ đâu

| Bạn cần biết gì | Đọc ở đâu |
|---|---|
| Quy tắc bắt buộc khi làm việc với repo (đặc biệt cho AI agent) | [`CLAUDE.md`](./CLAUDE.md) |
| **Base có sẵn gì / cần làm gì tiếp** | [`CLAUDE.md` mục 6](./CLAUDE.md) |
| Lệnh build/test/dev | [`CLAUDE.md` mục 7](./CLAUDE.md) |
| Nhật ký làm việc theo ngày | [`docs/nhat-ky/`](./docs/nhat-ky/README.md) |
| Quy trình Git, commit, PR | [`CONTRIBUTING.md`](./CONTRIBUTING.md) |
| Chính sách bảo mật | [`SECURITY.md`](./SECURITY.md) |
| Nhật ký thay đổi | [`CHANGELOG.md`](./CHANGELOG.md) |

## Bản đồ tài liệu

| Chủ đề | Thư mục | Nội dung chính |
|---|---|---|
| Kiến trúc & công nghệ | [`docs/kien-truc/`](docs/02-kien-truc/tong-quan-kien-truc.md) | Bản đồ công nghệ, [ADR](docs/02-kien-truc/adr), [thuật ngữ](docs/01-tong-quan/thuat-ngu.md) |
| Nghiệp vụ | [`docs/nghiep-vu/`](docs/06-nghiep-vu/README.md) | **24 mã FR** theo module: [LMS](docs/06-nghiep-vu/lop-hoc.md) · [CRM](docs/06-nghiep-vu/crm.md) · [HRM](docs/06-nghiep-vu/hrm.md) · [quản trị](docs/06-nghiep-vu/quan-tri-he-thong.md) |
| Dữ liệu | [`docs/database/`](docs/05-database/README.md) | [ERD 34 bảng](docs/05-database/erd.md) + ràng buộc + hành vi xoá, [quy ước migration](docs/05-database/quy-uoc-migration.md) |
| Backend | [`docs/backend/`](docs/03-backend/README.md) | [Clean Architecture](docs/03-backend/clean-architecture.md) · [CQRS](docs/03-backend/cqrs-mediatr.md) · [multi-tenant](docs/03-backend/multi-tenant.md) · [phân quyền động](docs/03-backend/phan-quyen-dong.md) |
| Frontend | [`docs/frontend/`](docs/04-frontend/README.md) | [Nguyên tắc UI/UX](docs/04-frontend/ui-ux-nguyen-tac.md), [design token](docs/04-frontend/design-tokens.md) |
| Hạ tầng & vận hành | [`docs/ha-tang/`](docs/07-ha-tang/README.md) | [Cài đặt VPS](docs/07-ha-tang/cai-dat-vps.md) · [biến môi trường](docs/07-ha-tang/bien-moi-truong.md) · [runbook](docs/07-ha-tang/runbook.md) |
| Hướng dẫn sử dụng | [`docs/huong-dan-su-dung/`](docs/08-quy-uoc/huong-dan-su-dung.md) | Theo vai trò (viết sau khi có UI thật) |

## Trạng thái quyết định

| Hạng mục | Đã chốt | ADR liên quan |
|---|---|---|
| Backend | ASP.NET Core (.NET 8), Clean Architecture + CQRS/MediatR | [ADR-0001](docs/02-kien-truc/adr/0001-lua-chon-cong-nghe.md) |
| Database | PostgreSQL, multi-tenant shared-schema (cột `tenant_id`) | [ADR-0001](docs/02-kien-truc/adr/0001-lua-chon-cong-nghe.md) |
| Frontend | React + TypeScript trên nền shadcn-admin (Vite + Tailwind + shadcn/ui) | [ADR-0002](docs/02-kien-truc/adr/0002-frontend-shadcn-admin.md) |
| API versioning | URL segment `/api/v1/...`, Asp.Versioning.Mvc | [ADR-0003](docs/02-kien-truc/adr/0003-api-versioning.md) |
| Triển khai | 1 VPS, Docker Compose, Nginx + certbot, MinIO object storage | [ADR-0004](docs/02-kien-truc/adr/0004-ha-tang-tu-host-vps.md) |
| **Một source** cho cả ba hệ thống con (không tách HRM/CRM/LMS) | [ADR-0005](docs/02-kien-truc/adr/0005-mot-source-va-doi-ten-langcenter.md) |

## Stack tóm tắt

**Backend:** ASP.NET Core 8 · EF Core · PostgreSQL · Redis (tuỳ chọn) · JWT + ASP.NET Core Identity
**Frontend:** React + TypeScript · shadcn-admin (Vite, Tailwind, shadcn/ui) · TanStack Query/Table · React Hook Form + Zod · Recharts
**Hạ tầng:** Docker Compose · Caddy · MinIO · Prometheus/Grafana · Loki · Uptime Kuma · GitHub Actions → ghcr.io

## Trạng thái dự án

🚧 **6/16 FR xong** — xác thực (FR-01, FR-02) và cụm quản trị hệ thống (FR-03→06) chạy được
đầu-cuối trên PostgreSQL. Chi tiết và lộ trình: [`docs/01-tong-quan/ke-hoach.md`](docs/01-tong-quan/ke-hoach.md).

## Kiểm tra liên kết tài liệu

```bash
python3 scripts/check-doc-links.py
```
