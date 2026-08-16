# Tổng quan kiến trúc

## Bản đồ công nghệ

| Lớp | Công nghệ | ADR |
|---|---|---|
| Backend | ASP.NET Core Web API (.NET 8), Clean Architecture, CQRS/MediatR | [ADR-0001](./adr/0001-lua-chon-cong-nghe.md) |
| Database | PostgreSQL, multi-tenant shared-schema | [ADR-0001](./adr/0001-lua-chon-cong-nghe.md) |
| Frontend | React + TypeScript, shadcn-admin (Vite/Tailwind/shadcn-ui) | [ADR-0002](./adr/0002-frontend-shadcn-admin.md) |
| API | REST, versioning theo URL segment `/api/v{n}/` | [ADR-0003](./adr/0003-api-versioning.md) |
| Hạ tầng | 1 VPS, Docker Compose, Caddy, MinIO, Prometheus/Grafana/Loki, Uptime Kuma | [ADR-0004](./adr/0004-ha-tang-tu-host-vps.md) |

## Cấu trúc mã nguồn

```
src/
├── GiapTech.SoccerRoom.Domain          # Entity, Enum, quy tắc nghiệp vụ thuần
├── GiapTech.SoccerRoom.Application     # CQRS Command/Query, DTO, interface, FluentValidation
├── GiapTech.SoccerRoom.Infrastructure  # EF Core DbContext, Repository, SMS/Email, MinIO client
└── GiapTech.SoccerRoom.API             # Controller theo version, Middleware, JWT, Swagger
frontend/                               # React + shadcn-admin (Vite)
```

Luật phụ thuộc giữa các lớp: [../backend/clean-architecture.md](../backend/clean-architecture.md).

## Ba cơ chế xuyên suốt

| Cơ chế | Cách hoạt động | Chi tiết |
|---|---|---|
| **Multi-tenant** | Đăng nhập nhập ID đội → middleware resolve `tenant_id` → JWT claim → EF Core `HasQueryFilter` áp dụng tự động cho mọi entity nghiệp vụ | [multi-tenant.md](../backend/multi-tenant.md) |
| **Phân quyền động** | Custom `IAuthorizationHandler` đọc bảng `QUYEN_CHUC_NANG` tại runtime (có cache ngắn hạn); attribute `[RequirePermission("LichThiDau", "Sua")]` trên từng endpoint — **không** dùng role cố định | [phan-quyen-dong.md](../backend/phan-quyen-dong.md) |
| **API versioning** | `/api/v1/matches`, `/api/v2/matches`. Tăng version chỉ khi breaking change; thêm field/endpoint mới hoặc sửa bug **không** tăng version. Version cũ deprecate qua header `Sunset`/`Deprecation` | [ADR-0003](./adr/0003-api-versioning.md) |

## Bảng trạng thái quyết định

| Hạng mục | Trạng thái | Ghi chú |
|---|---|---|
| Multi-tenant qua `tenant_id` | ✅ Đã chốt | [multi-tenant.md](../backend/multi-tenant.md) |
| Phân quyền động theo chức năng/thao tác | ✅ Đã chốt | Custom `IAuthorizationHandler` đọc `QUYEN_CHUC_NANG` |
| Đa ngôn ngữ | ✅ Đã chốt | Backend `.resx` theo culture; Frontend `react-i18next` |
| Tên namespace/solution | ✅ Đã chốt | `GiapTech.SoccerRoom.*` |
| Giá trị design token (màu cụ thể) | 🕓 Chưa chốt | Chốt cùng lúc dựng style-guide — [design-tokens.md](../frontend/design-tokens.md) |
| `tenant_id` ở bảng con (denormalize hay join) | 🕓 Chưa chốt | Quyết định khi tạo migration đầu — [ERD](../database/erd.md#ghi-chú-về-tenant_id-ở-bảng-con) |
| Mobile app | 🕓 Chưa triển khai | Dùng lại API hiện có, ưu tiên React Native khi cần |
| Real-time (SignalR) | 🕓 Chưa triển khai | Cân nhắc khi cần cập nhật vote MVP trực tiếp |

## Sơ đồ triển khai

Xem [../ha-tang/README.md](../ha-tang/README.md#sơ-đồ-triển-khai).

## Đường dẫn tài liệu liên quan

- Tổng thuật nghiệp vụ: [../tong-thuat.md](../tong-thuat.md)
- Nghiệp vụ chi tiết theo module: [../nghiep-vu/](../nghiep-vu/README.md)
- Mô hình dữ liệu: [../database/erd.md](../database/erd.md)
- Thuật ngữ dễ nhầm: [THUAT-NGU.md](./THUAT-NGU.md)
