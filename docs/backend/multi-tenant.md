# Multi-tenant — Cách ly dữ liệu theo `tenant_id`

> **Quy tắc bất di bất dịch #2.** Rò rỉ dữ liệu chéo giữa hai CLB là lỗi nghiêm trọng nhất mà hệ thống
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
| **Truy vấn thống kê aggregate** | Thường viết dạng raw SQL / group-by phức tạp | Điểm rủi ro cao nhất — báo cáo điểm danh và công nợ đều là group-by nhiều bảng |
| **`IgnoreQueryFilters()`** | Vô hiệu hóa filter hoàn toàn | Chỉ dùng cho tác vụ quản trị hệ thống, phải review kỹ |
| ~~Bảng con không có `tenant_id`~~ | ~~Truy vấn trực tiếp bảng con không bị lọc~~ | ✅ **Đã xử lý:** mọi bảng con mang `tenant_id` riêng — xem [ERD](../database/erd.md#denormalize-tenant_id-xuống-bảng-con) |
| **Kho tệp MinIO** (`ILuuTruAnh`, `ILuuTruTep`) | Kho lưu trữ không có Query Filter — đoán được khoá là đọc được tệp trung tâm khác | Khoá mang tenant ở đầu (`{tenantId}/{loai}/{guid}`) và `TaiVe`/`Xoa` kiểm lại tiền tố trước khi đọc. Canh bởi `AnhTests` |
| **Bổ khuyết quyền lúc khởi động** (`BoKhuyetQuyenQuanTri`) | Chạy khi chưa có tenant trong ngữ cảnh nên phải `IgnoreQueryFilters` trên `QUYEN` và `QUYEN_CHUC_NANG` | Chỉ THÊM, không xoá; chỉ nhắm nhóm tên `"Quản trị viên"`; luôn gán `TenantId` lấy từ chính nhóm đang xét, không từ `ICurrentTenant`. Canh bởi `NhomQuyenMacDinhTests` |
| **Include/navigation từ entity chưa lọc** | Kéo theo dữ liệu tenant khác | Bắt đầu truy vấn từ entity có filter |
| **Background job / cron** | Không có HTTP context → không có claim tenant | Truyền `tenant_id` tường minh vào job, không dựa vào `ICurrentTenant` |

### Năm endpoint đọc/ghi ngoài tenant, xếp theo mức rộng

Càng xuống dưới càng lộ nhiều. Sửa gì ở đây cũng phải đọc lại cả bảng này:

| Endpoint | Đọc gì | Giới hạn |
|---|---|---|
| `GET /auth/ten-doi/{maDoi}` | **Chỉ tên** một CLB, khi biết **chính xác** mã 7 ký tự | **Ẩn danh** (người dùng đang ở trang đăng nhập nên chưa thể có token). Chỉ trả `tenDoi` — không `maDoi`, không `id`, không `khuVuc`. Không tìm theo tên. 404 giống nhau cho mã sai định dạng và mã không tồn tại |
| `GET /doi-thu/tra-cuu-clb/{maDoi}` | Một CLB, khi biết **chính xác** mã 7 ký tự | Không tìm theo tên, không liệt kê, 404 giống nhau cho mọi loại không-tìm-thấy |
| `POST /moi-qua-link/xem` | Một lời mời, khi biết token trong link | **Ẩn danh**. Token trong **body** chứ không trong URL (URL vào log, vào history, vào Referer) |
| `GET /cong-dong/loi-moi` | Lời mời có ta là một trong hai bên | Tự lọc hai chiều; liên hệ bên kia chỉ trả **sau khi** đã chấp nhận |
| `GET /cong-dong` | **Mọi CLB** trong hệ thống + thành tích | Không trả `id`, **không trả liên hệ**, không trả dữ liệu cầu thủ/quỹ/chi tiết trận |

**Quyết định của chủ sản phẩm (20/08/2026):** trang đăng nhập tra tên đội theo mã, nhưng
**không** tìm theo tên. Yêu cầu ban đầu có cả tìm theo tên và gợi ý các đội gần giống; endpoint
này buộc phải ẩn danh, nên cho tìm theo tên đồng nghĩa với việc bất kỳ ai gõ một chữ cũng liệt kê
được toàn hệ thống kèm mã đội. Canh bởi `TraTenDoiTests.Go_TEN_doi_vao_o_ma_thi_KHONG_tra_gi` —
test đó **tự tạo một CLB tên dài** rồi thử mọi đoạn 7 ký tự cắt từ tên, vì tên CLB trong fixture
chỉ 5 ký tự nên không chuỗi con nào đi qua được ràng buộc route `length(7)`.

**Quyết định của chủ sản phẩm (18/08/2026):** mọi CLB tự động lên Cộng đồng, **không có cách
tắt**, kèm thành tích thắng/hoà/thua. Nó cố ý đi ngược thiết kế của `tra-cuu-clb` (vốn dựng để
*chặn* việc liệt kê CLB). Nếu sau này cần cho CLB tự chọn ẩn/hiện, chỗ sửa là mệnh đề `Where`
trong `LayDanhSachCongDongHandler` — thêm cột `Tenant.HienTrenSan` và lọc theo nó. **Không** sửa ở
tầng UI: ẩn ở UI mà API vẫn trả thì chỉ cần mở DevTools là thấy hết.

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
- [ ] [ERD](../database/erd.md) đã cập nhật **trong cùng PR**.

## Tham chiếu

- Đăng nhập & cấp claim: [FR-01](../nghiep-vu/dang-nhap.md)
- Phân quyền trong phạm vi tenant: [phan-quyen-dong.md](./phan-quyen-dong.md)
- Chính sách bảo mật: [SECURITY.md](../../SECURITY.md)
