# Clean Architecture — 4 lớp

## Cấu trúc thư mục

```
src/
├── GiapTech.LangCenter.Domain          # Entity, Enum, quy tắc nghiệp vụ thuần
├── GiapTech.LangCenter.Application     # CQRS Command/Query, DTO, interface, FluentValidation
├── GiapTech.LangCenter.Infrastructure  # EF Core DbContext, Repository, SMS/Email, MinIO client
└── GiapTech.LangCenter.API             # Controller theo version, Middleware, JWT, Swagger
frontend/                               # React + shadcn-admin (Vite)
```

## Luật phụ thuộc

```
API ──────────┐
              ├──> Application ──> Domain
Infrastructure┘                      ▲
              └──────────────────────┘
```

| Lớp | Được tham chiếu | **Cấm** tham chiếu |
|---|---|---|
| `Domain` | (không tham chiếu lớp nào) | EF Core, ASP.NET Core, MediatR, mọi package hạ tầng |
| `Application` | `Domain` | `Infrastructure`, `API`, EF Core (chỉ định nghĩa **interface**) |
| `Infrastructure` | `Domain`, `Application` | `API` |
| `API` | `Application`, `Infrastructure` (chỉ để đăng ký DI) | — |

**Điểm dễ sai nhất:** `Domain` không được `using Microsoft.EntityFrameworkCore`. Cấu hình EF (khóa, index,
quan hệ, ánh xạ tên cột) đặt ở `Infrastructure` qua `IEntityTypeConfiguration<T>`, không rải attribute
trong Domain.

## Trách nhiệm từng lớp

### Domain

Entity thuần POCO, enum (`KetQuaTranDau`, `TrangThaiTranDau`, `HanhDong`...), quy tắc nghiệp vụ không
phụ thuộc hạ tầng (ví dụ: tính `ket_qua` từ tỷ số, kiểm tra `so_tien_da_dong` không vượt
`so_tien_can_dong`).

### Application

- Mỗi FR-xx = một hoặc vài Command/Query riêng — xem [cqrs-mediatr.md](./cqrs-mediatr.md).
- DTO request/response.
- **Interface** cho hạ tầng: `IAppDbContext`, `IEmailSender`, `ISmsSender`, `IFileStorage`,
  `ICurrentTenant`, `ICurrentUser`.
- Validation bằng FluentValidation, chạy qua MediatR pipeline behavior.

### Infrastructure

- `AppDbContext` + `IEntityTypeConfiguration<T>` + migration.
- Cài đặt cụ thể các interface: SMTP sender, SMS gateway (eSMS/Speedsms), MinIO client (presigned URL).
- Repository (chỉ khi cần trừu tượng hóa thêm — mặc định dùng thẳng `DbContext` qua `IAppDbContext`).

### API

- Controller tổ chức theo version: `Controllers/V1/`, `Controllers/V2/` — xem
  [ADR-0003](../kien-truc/adr/0003-api-versioning.md).
- Middleware: resolve tenant, exception handler → **mã lỗi** (không text cứng), request logging.
- Cấu hình JWT, Swagger (mỗi version một document), CORS.

## Thứ tự viết code khi thêm tính năng

1. Đọc mô tả FR-xx ở [docs/nghiep-vu/](../nghiep-vu/README.md).
2. Cập nhật [ERD](../database/erd.md) + migration nếu đổi dữ liệu.
3. Viết code theo thứ tự **Domain → Application → Infrastructure → API**.
4. Cập nhật Swagger/OpenAPI + tài liệu API.
5. Viết test: unit cho `Application`, integration cho endpoint `API`.
6. Cập nhật [CHANGELOG.md](../../CHANGELOG.md).
