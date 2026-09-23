# Quy ước đặt tên & Migration EF Core

> Quy ước đặt tên đầy đủ cho **cả dự án** (C#, TS, SQL): [quy-uoc-code.md](../08-quy-uoc/quy-uoc-code.md).
> File này chỉ nói phần **DB và migration**.

## Quy ước đặt tên

| Đối tượng | Quy ước | Ví dụ |
|---|---|---|
| Tên bảng | `UPPER_SNAKE_CASE`, tiếng Việt không dấu | `LOP_HOC`, `KHOAN_THU_HOC_PHI` |
| Tên cột | `lower_snake_case`, tiếng Việt không dấu | `hoc_phi_ap_dung`, `ngay_vao_lop` |
| Khóa chính | `id` | `id` |
| Khóa ngoại | `<tên_bảng_đích_số_ít>_id` | `lop_hoc_id`, `hoc_vien_id` |
| Entity C# | `PascalCase` tiếng Việt không dấu | `LopHoc`, `KhoanThuHocPhi` |
| Property C# | `PascalCase` | `HocPhiApDung`, `NgayVaoLop` |
| 4 cột audit (mọi bảng) | có sẵn ở `BaseEntity` | `created_at`, `updated_at`, `created_by_id`, `updated_by_id` |

> ⚠️ `UseSnakeCaseNamingConvention()` suy tên cột **TỪ** tên property. Đổi tên property nghĩa là
> **đổi tên cột** — cần migration, và mọi tên trường JSON của API cũng đổi theo. Đây là chỗ đã
> làm hỏng một lần: xem [ADR-0006](../02-kien-truc/adr/0006-dat-ten-tieng-anh-va-cot-audit.md).

Ánh xạ tên C# ↔ tên cột DB cấu hình tập trung trong `DbContext.OnModelCreating` (hoặc convention
`UseSnakeCaseNamingConvention` của Npgsql), **không rải `[Column]` attribute** khắp Domain layer —
Domain không được phụ thuộc EF Core (xem [clean-architecture](../03-backend/clean-architecture.md)).

## Quy ước migration

### Đặt tên migration

`<Động từ><Đối tượng>` bằng tiếng Anh, PascalCase: `InitialCreate`, `AddLoiMoiDoiThu`,
`AddUniqueVoteMvp`, `AlterQuyThemTrangThai`.

### Lệnh

```bash
# Tạo migration mới (chạy từ thư mục gốc solution)
dotnet ef migrations add <TenMigration> \
  --project src/GiapTech.LangCenter.Infrastructure \
  --startup-project src/GiapTech.LangCenter.API

# Áp dụng vào DB
dotnet ef database update \
  --project src/GiapTech.LangCenter.Infrastructure \
  --startup-project src/GiapTech.LangCenter.API

# Xuất script SQL để review trước khi chạy production
dotnet ef migrations script --idempotent -o migration.sql \
  --project src/GiapTech.LangCenter.Infrastructure \
  --startup-project src/GiapTech.LangCenter.API
```

### Nguyên tắc

1. **Migration đã chạy production thì không sửa/xóa** — sai thì tạo migration mới sửa đè. Sửa migration
   cũ làm lệch trạng thái giữa các môi trường.
2. Migration đang ở nhánh feature, **chưa merge** vào `main` thì được `migrations remove` và tạo lại.
3. Migration đụng dữ liệu (đổi kiểu cột, tách bảng) → **review script SQL** bằng `migrations script`
   trước khi apply, không apply mù bằng `database update` trên production.
4. Thêm cột `NOT NULL` vào bảng đã có dữ liệu → phải có `defaultValue`, hoặc tách thành 3 bước
   (thêm nullable → backfill → set NOT NULL).
5. Đổi schema → cập nhật [erd.md](erd.md) **trong cùng PR** (checklist PR ở
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
[../ha-tang/runbook.md](../07-ha-tang/runbook.md).
