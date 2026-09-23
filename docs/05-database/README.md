# Database

PostgreSQL, multi-tenant **shared-schema** (mọi bảng nghiệp vụ có cột `tenant_id`) — xem
[ADR-0001](../02-kien-truc/adr/0001-lua-chon-cong-nghe.md).

| Nội dung | Tài liệu |
|---|---|
| ERD đầy đủ **34 bảng** + ràng buộc + hành vi xoá | [erd.md](erd.md) |
| Quy ước đặt tên, migration EF Core, seed data | [quy-uoc-migration.md](quy-uoc-migration.md) |
| Cách áp dụng Global Query Filter theo tenant | [../backend/multi-tenant.md](../03-backend/multi-tenant.md) |

## Quy tắc bất di bất dịch

1. **Mọi bảng nghiệp vụ có cột `tenant_id`** (FK → `TENANT`) và **bắt buộc** áp dụng EF Core Global
   Query Filter. Không được quên ở bất kỳ entity mới nào.
2. Ràng buộc `UNIQUE(tran_dau_id, nguoi_vote_id)` trên `VOTE_MVP` — mỗi người 1 vote/trận, chặn ở tầng
   DB chứ không chỉ ở UI.
3. Đổi schema → tạo migration **và** cập nhật tài liệu này **trong cùng PR**, không tách "làm sau".
