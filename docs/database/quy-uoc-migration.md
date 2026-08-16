# Quy ước đặt tên & Migration EF Core

> ⚠️ Tài liệu này chốt quy ước **trước khi** tạo migration đầu tiên. Khi solution .NET được khởi tạo,
> cập nhật các mục còn để ngỏ ở đây trong cùng PR.

## Quy ước đặt tên

| Đối tượng | Quy ước | Ví dụ |
|---|---|---|
| Tên bảng | `UPPER_SNAKE_CASE`, tiếng Việt không dấu | `TRAN_DAU`, `DONGGOP_QUY` |
| Tên cột | `lower_snake_case`, tiếng Việt không dấu | `thoi_gian`, `so_ban_ghi_duoc` |
| Khóa chính | `id` | `id` |
| Khóa ngoại | `<tên_bảng_đích_số_ít>_id` | `tran_dau_id`, `cau_thu_id` |
| Entity C# | `PascalCase` tiếng Việt không dấu | `TranDau`, `DongGopQuy` |
| Property C# | `PascalCase` | `ThoiGian`, `SoBanGhiDuoc` |

Ánh xạ tên C# ↔ tên cột DB cấu hình tập trung trong `DbContext.OnModelCreating` (hoặc convention
`UseSnakeCaseNamingConvention` của Npgsql), **không rải `[Column]` attribute** khắp Domain layer —
Domain không được phụ thuộc EF Core (xem [clean-architecture](../backend/clean-architecture.md)).

## Quy ước migration

### Đặt tên migration

`<Động từ><Đối tượng>` bằng tiếng Anh, PascalCase: `InitialCreate`, `AddLoiMoiDoiThu`,
`AddUniqueVoteMvp`, `AlterQuyThemTrangThai`.

### Lệnh

```bash
# Tạo migration mới (chạy từ thư mục gốc solution)
dotnet ef migrations add <TenMigration> \
  --project src/GiapTech.SoccerRoom.Infrastructure \
  --startup-project src/GiapTech.SoccerRoom.API

# Áp dụng vào DB
dotnet ef database update \
  --project src/GiapTech.SoccerRoom.Infrastructure \
  --startup-project src/GiapTech.SoccerRoom.API

# Xuất script SQL để review trước khi chạy production
dotnet ef migrations script --idempotent -o migration.sql \
  --project src/GiapTech.SoccerRoom.Infrastructure \
  --startup-project src/GiapTech.SoccerRoom.API
```

### Nguyên tắc

1. **Migration đã chạy production thì không sửa/xóa** — sai thì tạo migration mới sửa đè. Sửa migration
   cũ làm lệch trạng thái giữa các môi trường.
2. Migration đang ở nhánh feature, **chưa merge** vào `main` thì được `migrations remove` và tạo lại.
3. Migration đụng dữ liệu (đổi kiểu cột, tách bảng) → **review script SQL** bằng `migrations script`
   trước khi apply, không apply mù bằng `database update` trên production.
4. Thêm cột `NOT NULL` vào bảng đã có dữ liệu → phải có `defaultValue`, hoặc tách thành 3 bước
   (thêm nullable → backfill → set NOT NULL).
5. Đổi schema → cập nhật [erd.md](./erd.md) **trong cùng PR** (checklist PR ở
   [CONTRIBUTING.md](../../CONTRIBUTING.md)).

## Seed data

Tenant mới cần seed tối thiểu:

| Dữ liệu | Giá trị |
|---|---|
| Tài khoản admin mặc định | `admin` / `123456`, `phai_doi_mk = true` |
| Nhóm quyền "Quản trị viên" | Đầy đủ `xem/them/sua/xoa` cho mọi chức năng |
| Thiết lập chung (FR-06) | Giá trị mặc định, cho phép để trống logo/ảnh bìa |

Seed **không** đặt trong migration `InitialCreate` mà tách thành seeder chạy khi tạo tenant mới — vì
mỗi tenant cần một bộ dữ liệu khởi tạo riêng, không phải chạy một lần lúc tạo DB.

## Backup & restore

`pg_dump` định kỳ qua cron, đẩy bản sao ra ngoài VPS. Quy trình restore: xem
[../ha-tang/runbook.md](../ha-tang/runbook.md).
