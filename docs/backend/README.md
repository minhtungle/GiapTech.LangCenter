# Backend

ASP.NET Core Web API (.NET 8 LTS), Clean Architecture 4 lớp, CQRS qua MediatR, EF Core Code-First trên
PostgreSQL — xem [ADR-0001](../kien-truc/adr/0001-lua-chon-cong-nghe.md).

| Nội dung | Tài liệu |
|---|---|
| Cấu trúc 4 lớp, luật phụ thuộc | [clean-architecture.md](./clean-architecture.md) |
| Tổ chức Command/Query theo mã FR | [cqrs-mediatr.md](./cqrs-mediatr.md) |
| Middleware tenant + Global Query Filter | [multi-tenant.md](./multi-tenant.md) |
| `IAuthorizationHandler` đọc quyền động từ DB | [phan-quyen-dong.md](./phan-quyen-dong.md) |
| Chiến lược versioning API | [ADR-0003](../kien-truc/adr/0003-api-versioning.md) |

## Quy tắc bất di bất dịch

1. **Mọi entity nghiệp vụ có `tenant_id` + Global Query Filter** — không được quên.
2. **API không hard-code message lỗi một ngôn ngữ** — trả **mã lỗi**, frontend dịch qua `react-i18next`.
   Backend dùng `.resx` theo culture cho các nội dung cần dịch phía server (email, SMS).
3. **Không dùng `[Authorize(Roles=...)]`** — phân quyền đọc động từ bảng `QUYEN_CHUC_NANG`.
4. Tăng version API chỉ khi **breaking change** (đổi/xóa field, đổi kiểu dữ liệu, đổi hành vi mặc định).
   Thêm field/endpoint mới hoặc sửa bug **không** tăng version.
