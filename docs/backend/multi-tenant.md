# Multi-tenant — Cách ly dữ liệu theo `tenant_id`

> **Quy tắc bất di bất dịch #1.** Rò rỉ dữ liệu chéo giữa hai CLB là lỗi nghiêm trọng nhất mà hệ thống
> này có thể mắc phải. Mọi entity nghiệp vụ mới **bắt buộc** đi qua checklist cuối trang.

Mô hình: **shared-schema** — mọi tenant dùng chung bảng, phân biệt bằng cột `tenant_id`
(xem [ADR-0001](../kien-truc/adr/0001-lua-chon-cong-nghe.md)).

## Luồng resolve tenant

```
1. Đăng nhập  →  người dùng nhập {ID đội, username, password}
2. Xác thực   →  tìm tenant theo ID đội, xác thực user trong phạm vi tenant đó
3. Cấp JWT    →  gắn claim tenant_id vào token
4. Mỗi request→  middleware đọc claim tenant_id  →  ICurrentTenant.TenantId
5. DbContext  →  Global Query Filter dùng ICurrentTenant.TenantId  →  lọc tự động
```

## Ba tầng phòng vệ

### Tầng 1 — Global Query Filter (tự động, mặc định)

Áp dụng cho **mọi** entity nghiệp vụ trong `OnModelCreating`. Mọi truy vấn LINQ qua `DbSet` được thêm
điều kiện `WHERE tenant_id = @current` tự động.

Cách áp dụng theo interface đánh dấu (ví dụ `ITenantEntity` có property `TenantId`) và duyệt toàn bộ
model bằng reflection — an toàn hơn khai báo thủ công từng entity, vì entity mới **tự động** được bảo vệ
thay vì phải nhớ thêm dòng cấu hình.

### Tầng 2 — Gán `tenant_id` khi ghi (tự động)

Override `SaveChanges`/`SaveChangesAsync` trong `AppDbContext`: mọi entity ở trạng thái `Added` được gán
`TenantId = ICurrentTenant.TenantId`. Không để tầng Application tự gán thủ công — dễ quên.

### Tầng 3 — Test tự động

Integration test bắt buộc cho mỗi module: tạo dữ liệu ở tenant A, đăng nhập tenant B, xác nhận **không**
đọc/sửa/xóa được dữ liệu của A (kể cả khi truyền đúng `id`).

## Những chỗ Global Query Filter KHÔNG bảo vệ

Đây là các lỗ hổng thực sự, phải xử lý thủ công:

| Trường hợp | Rủi ro | Xử lý |
|---|---|---|
| **Raw SQL** (`FromSqlRaw`, `ExecuteSqlRaw`, Dapper) | Filter không áp dụng | Tự thêm `WHERE tenant_id = @tenant` trong mọi câu lệnh |
| **Truy vấn thống kê aggregate** | Thường viết dạng raw SQL / group-by phức tạp | Điểm rủi ro cao nhất — xem [FR-13, FR-14](../nghiep-vu/thong-ke.md) |
| **`IgnoreQueryFilters()`** | Vô hiệu hóa filter hoàn toàn | Chỉ dùng cho tác vụ quản trị hệ thống, phải review kỹ |
| **Bảng con không có `tenant_id`** (`VOTE_MVP`, `DOIHINH_TRANDAU`, `DONGGOP_QUY`...) | Truy vấn trực tiếp bảng con không bị lọc | Luôn join lên bảng cha, hoặc denormalize `tenant_id` — xem [ERD](../database/erd.md) |
| **Include/navigation từ entity chưa lọc** | Kéo theo dữ liệu tenant khác | Bắt đầu truy vấn từ entity có filter |
| **Background job / cron** | Không có HTTP context → không có claim tenant | Truyền `tenant_id` tường minh vào job, không dựa vào `ICurrentTenant` |

## Checklist khi thêm entity nghiệp vụ mới

- [ ] Entity có property `TenantId` (hoặc kế thừa `ITenantEntity`).
- [ ] Migration tạo cột `tenant_id` + FK → `TENANT` + **index** trên `tenant_id`.
- [ ] Nếu là bảng con không mang `tenant_id`: xác nhận mọi truy vấn đều đi qua bảng cha.
- [ ] Có integration test cách ly tenant cho entity này.
- [ ] [ERD](../database/erd.md) đã cập nhật **trong cùng PR**.

## Tham chiếu

- Đăng nhập & cấp claim: [FR-01](../nghiep-vu/dang-nhap.md)
- Phân quyền trong phạm vi tenant: [phan-quyen-dong.md](./phan-quyen-dong.md)
- Chính sách bảo mật: [SECURITY.md](../../SECURITY.md)
