# Multi-tenant — Cách ly dữ liệu theo `tenant_id`

> **Quy tắc bất di bất dịch #2.** Rò rỉ dữ liệu chéo giữa hai trung tâm là lỗi nghiêm trọng nhất mà hệ thống
> này có thể mắc phải. Mọi entity nghiệp vụ mới **bắt buộc** đi qua checklist cuối trang.

Mô hình: **shared-schema** — mọi tenant dùng chung bảng, phân biệt bằng cột `tenant_id`
(xem [ADR-0001](../02-kien-truc/adr/0001-lua-chon-cong-nghe.md)).

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
| **Truy vấn thống kê aggregate** | Thường viết dạng raw SQL / group-by phức tạp | Điểm rủi ro cao nhất — báo cáo điểm danh và công nợ đều là group-by nhiều bảng |
| **`IgnoreQueryFilters()`** | Vô hiệu hóa filter hoàn toàn | Chỉ dùng cho tác vụ quản trị hệ thống, phải review kỹ |
| ~~Bảng con không có `tenant_id`~~ | ~~Truy vấn trực tiếp bảng con không bị lọc~~ | ✅ **Đã xử lý:** mọi bảng con mang `tenant_id` riêng — xem [ERD](../05-database/erd.md#denormalize-tenant_id-xuống-bảng-con) |
| **Kho tệp MinIO** (`ILuuTruAnh`, `ILuuTruTep`) | Kho lưu trữ không có Query Filter — đoán được khoá là đọc được tệp trung tâm khác | Khoá mang tenant ở đầu (`{tenantId}/{loai}/{guid}`) và `TaiVe`/`Xoa` kiểm lại tiền tố trước khi đọc. Canh bởi `AnhTests` |
| **Bổ khuyết quyền lúc khởi động** (`BoKhuyetQuyenQuanTri`) | Chạy khi chưa có tenant trong ngữ cảnh nên phải `IgnoreQueryFilters` trên `QUYEN` và `QUYEN_CHUC_NANG` | Chỉ THÊM, không xoá; chỉ nhắm nhóm tên `"Quản trị viên"`; luôn gán `TenantId` lấy từ chính nhóm đang xét, không từ `ICurrentTenant`. Canh bởi `NhomQuyenMacDinhTests` |
| **Include/navigation từ entity chưa lọc** | Kéo theo dữ liệu tenant khác | Bắt đầu truy vấn từ entity có filter |
| **Background job / cron** | Không có HTTP context → không có claim tenant | Truyền `tenant_id` tường minh vào job, không dựa vào `ICurrentTenant` |

### Endpoint ẩn danh — nơi Query Filter KHÔNG bảo vệ

Query Filter lọc theo tenant của **phiên hiện tại**. Endpoint ẩn danh chưa có phiên, nên mỗi cái
phải tự giới hạn những gì nó tiết lộ. Sửa gì ở đây cũng phải đọc lại cả bảng này:

| Endpoint | Đọc/ghi gì | Giới hạn |
|---|---|---|
| `GET /auth/ten-trung-tam/{maTrungTam}` | **Chỉ tên** một trung tâm, khi biết **chính xác** mã 7 ký tự | Chỉ trả tên — không id, không thông tin khác. **Không tìm theo tên.** 404 giống nhau cho mã sai định dạng và mã không tồn tại. Hạn mức 30 req/phút mỗi IP |
| `POST /auth/dang-nhap` | — | Sai mã trung tâm / sai username / sai mật khẩu đều trả **một mã lỗi duy nhất**, không tiết lộ thứ nào tồn tại. Hạn mức 10 req/phút |
| `POST /auth/quen-mat-khau` | — | Trả **giống nhau** dù email có tồn tại hay không. Token hash, hạn 30 phút, dùng một lần. Hạn mức 10 req/phút |
| `POST /auth/dat-lai-mat-khau` | — | Chỉ nhận token còn hiệu lực; dùng rồi là vô hiệu. Hạn mức 10 req/phút |
| `POST /auth/lam-moi-token` | — | Refresh token **xoay vòng** và **phát hiện tái sử dụng** — chặt hơn rate limit nên được miễn hạn mức |
| `POST /dang-ky-trung-tam` | **GHI**: tạo tenant + tài khoản admin | Chỉ nhận tên trung tâm; mã do hệ thống sinh. Hạn mức 10 req/phút mỗi IP (thêm 08/09/2026). ⚠️ **Rate limit ở reverse proxy vẫn bắt buộc** — nợ N3 |

**Quyết định của chủ sản phẩm (20/08/2026, còn hiệu lực):** trang đăng nhập tra tên trung tâm
theo mã, nhưng **không** tìm theo tên. Yêu cầu ban đầu có cả tìm theo tên và gợi ý tên gần giống;
endpoint này buộc phải ẩn danh, nên cho tìm theo tên đồng nghĩa với việc bất kỳ ai gõ một chữ
cũng liệt kê được toàn hệ thống kèm mã trung tâm.

Canh bởi `TraTenTrungTamTests.Go_TEN_vao_o_ma_thi_KHONG_tra_gi` — test đó **tự tạo một trung tâm
tên dài** rồi thử mọi đoạn 7 ký tự cắt từ tên, vì tên trong fixture quá ngắn nên không chuỗi con
nào đi qua được ràng buộc route `length(7)`. Kèm
`KHONG_co_duong_nao_tim_trung_tam_theo_TEN_o_trang_dang_nhap` quét mọi endpoint ẩn danh để chặn
việc thêm lại đường tìm theo tên.

> **Đã gỡ (05/09/2026):** `GET /cong-dong`, `GET /cong-dong/loi-moi`, `POST /moi-qua-link/xem` —
> nghiệp vụ của dự án cũ (danh bạ công khai và lời mời thách đấu), không còn trong code.

## ⚠️ Bẫy: filter không được trỏ ra object bên ngoài DbContext

Khi dựng query filter bằng expression tree, biểu thức **phải** trỏ vào property của chính
`DbContext`, không trỏ thẳng vào object `ICurrentTenant`:

```csharp
// ĐÚNG — EF thay bằng instance đang chạy ở mỗi truy vấn
Expression.Property(Expression.Constant(this), nameof(TenantIdHienTai))

// SAI — model bị cache, object "nướng cứng" vào context ĐẦU TIÊN
Expression.Property(Expression.Constant(currentTenant), nameof(ICurrentTenant.TenantId))
```

EF Core cache model và dùng chung cho mọi context có cùng options. Bản sai khiến context của tenant B
đọc tenant của A và **thấy dữ liệu của A** — không exception, không log, chỉ trả về dữ liệu sai.

Lỗi này đã thực sự xảy ra trong quá trình dựng `AppDbContext` và bị `CachLyTenantTests` bắt được. Hai
test đỏ khi dùng bản sai, xanh khi dùng bản đúng — đã kiểm chứng cả hai chiều.

## Checklist khi thêm entity nghiệp vụ mới

- [ ] Entity có property `TenantId` (hoặc kế thừa `ITenantEntity`).
- [ ] Migration tạo cột `tenant_id` + FK → `TENANT` + **index** trên `tenant_id`.
- [ ] Bảng chi tiết cũng mang `tenant_id` riêng (không dựa vào join lên bảng cha).
- [ ] Có test cách ly tenant cho entity này (`CachLyTenantTests`).
- [ ] [ERD](../05-database/erd.md) đã cập nhật **trong cùng PR**.

## Tham chiếu

- Đăng nhập & cấp claim: [FR-01](../06-nghiep-vu/dang-nhap.md)
- Phân quyền trong phạm vi tenant: [phan-quyen-dong.md](phan-quyen-dong.md)
- Chính sách bảo mật: [SECURITY.md](../../SECURITY.md)
