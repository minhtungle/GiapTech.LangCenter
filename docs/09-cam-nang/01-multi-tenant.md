# Cẩm nang: Multi-tenant bằng cột `tenant_id` chung (EF Core)

> **Đọc cho ai:** lập trình viên — hoặc một phiên trợ lý AI — sắp dựng một hệ thống SaaS
> multi-tenant trên .NET + EF Core, **không** cần biết gì về dự án nơi các bài học này được rút ra.
>
> Mã mẫu lấy nguyên từ mã đang chạy. Tên tiếng Việt trong mã là quy ước đặt tên của dự án gốc,
> không phải yêu cầu của kỹ thuật này.

---

## 1. Vấn đề

**Multi-tenant** là một lần cài đặt phần mềm phục vụ nhiều tổ chức khách hàng (tenant) cùng lúc,
mỗi tổ chức chỉ thấy dữ liệu của chính mình. Sai sót ở đây không phải bug hiển thị: nó là rò rỉ dữ
liệu giữa hai khách hàng — và nó **thất bại im lặng** (không exception, không log, chỉ trả về dữ
liệu của người khác).

### Ba cách cách ly

| Cách | Cách ly | Chi phí vận hành | Điểm chết người |
|---|---|---|---|
| **Database riêng mỗi tenant** | Mạnh nhất — lỗi lập trình không vượt được ranh giới DB | Cao: N database, N lần migration, N backup | Tenant thứ 200 là 200 lần chạy migration |
| **Schema riêng mỗi tenant** | Khá | Trung bình: vẫn N lần migration | Truy vấn xuyên tenant (báo cáo toàn hệ thống) rất khó |
| **Cột `tenant_id` chung** (shared-schema) | Yếu nhất về mặt vật lý — **cách ly nằm ở tầng ứng dụng** | Thấp nhất: 1 DB, 1 migration | Quên một mệnh đề `WHERE` là rò rỉ |

### Vì sao dự án gốc chọn cột chung (ADR-0001)

- Số tenant giai đoạn đầu nhỏ, hạ tầng là **một VPS chi phí thấp**. Schema-per-tenant làm phức tạp
  hoá migration mà chưa đổi lại được gì.
- Chi phí "quên `WHERE`" được triệt tiêu bằng **cơ chế tự động** (Global Query Filter), không bằng
  kỷ luật của người viết code. Khi rủi ro chính đã có hàng rào tự động, ưu thế của hai cách kia mỏng đi.
- Đường lui vẫn mở: ADR ghi rõ "cân nhắc lại nếu số tenant tăng mạnh".

**Điều kiện để lựa chọn này an toàn** — không làm đủ thì đừng chọn nó:

1. Cách ly là **mặc định tự động**, không phải việc phải nhớ của từng handler.
2. Có **test canh cả hai chiều** (mục 4) để cơ chế tự động không thủng theo thời gian.
3. Liệt kê tường minh **những chỗ cơ chế tự động không với tới** (bẫy 3.4) và xử lý từng chỗ.

---

## 2. Cách làm: Global Query Filter trong EF Core

Đánh dấu entity thuộc phạm vi tenant bằng một interface, rồi **duyệt toàn bộ model bằng reflection**
để áp filter — thay vì khai báo tay từng entity. Khai tay thì một lần quên là rò rỉ.

### 2.1. Interface đánh dấu

```csharp
public interface ITenantEntity { Guid TenantId { get; set; } }

public abstract class TenantEntity : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
}
```

**Bảng chi tiết cũng mang `tenant_id` riêng**, không dựa vào join lên bảng cha: nếu bảng con chỉ có
khoá ngoại tới cha, một truy vấn đi thẳng vào bảng con không được lọc gì cả. Denormalize một cột
`uuid` là cái giá rẻ để mọi đường vào đều có hàng rào.

### 2.2. Nguồn tenant của request

Tenant giải một lần mỗi request, nạp vào một object **scoped** (`CurrentTenant`) có thêm hàm
`DatPhamVi(tenantId)` trả `IDisposable` để seeder và background job ghi đè tạm thời.

Middleware đọc claim từ JWT đã ký. Điểm đáng chép sang dự án khác là **nhánh else**:

```csharp
if (context.User.Identity?.IsAuthenticated == true)
{
    var giaTri = context.User.FindFirstValue(ClaimTenant.TenantId);
    if (Guid.TryParse(giaTri, out var tenantId))
    {
        currentTenant.Gan(tenantId);
    }
    else
    {
        // Token hợp lệ về chữ ký nhưng thiếu/hỏng claim tenant. Không thể phục vụ
        // an toàn: bỏ qua sẽ khiến filter chạy ở chế độ "không tenant" và lộ dữ liệu
        // của mọi trung tâm. Chặn tại đây.
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { errorCode = "TOKEN_THIEU_TENANT" });
        return;
    }
}
```

Request đã xác thực mà không giải được tenant **phải bị từ chối**, không được đi tiếp trong trạng
thái "không tenant" — lý do ở bẫy 3.2. Middleware chạy **sau** `UseAuthentication`, **trước**
`UseAuthorization`.

### 2.3. Áp filter cho toàn bộ model

```csharp
private void ApDungQueryFilterTheoTenant(ModelBuilder modelBuilder)
{
    foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
        if (!typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            continue;

        // e => this.TenantIdHienTai == null || e.TenantId == this.TenantIdHienTai
        var thamSo = Expression.Parameter(entityType.ClrType, "e");

        var tenantHienTai = Expression.Property(
            Expression.Constant(this), nameof(TenantIdHienTai));

        var khongCoTenant = Expression.Equal(
            tenantHienTai, Expression.Constant(null, typeof(Guid?)));

        var khopTenant = Expression.Equal(
            Expression.Convert(
                Expression.Property(thamSo, nameof(ITenantEntity.TenantId)), typeof(Guid?)),
            tenantHienTai);

        modelBuilder.Entity(entityType.ClrType)
            .HasQueryFilter(Expression.Lambda(
                Expression.OrElse(khongCoTenant, khopTenant), thamSo));
    }
}
```

- `foreach` + `IsAssignableFrom` — quét cả model, **không có đường quên** một entity.
- `Expression.Property(Expression.Constant(this), ...)` — đọc qua property của **chính DbContext**.
  Chi tiết sống còn, xem bẫy 3.1.
- `Expression.Convert(..., typeof(Guid?))` — `TenantId` là `Guid`, tenant hiện tại là `Guid?`;
  không ép kiểu thì `Expression.Equal` ném lỗi lúc dựng model.
- Nhánh `== null` — cho hạ tầng chạy khi chưa có tenant. Tiện, nhưng là lỗ hổng có điều kiện: bẫy 3.2.

### 2.4. Tự gán `tenant_id` khi ghi

Filter chỉ lo lúc **đọc**. Lúc **ghi** cần tầng riêng trong `SaveChanges`/`SaveChangesAsync`:

```csharp
foreach (var entry in ChangeTracker.Entries<BaseEntity>())
{
    switch (entry.State)
    {
        case EntityState.Added:
            if (entry.Entity is ITenantEntity moi && moi.TenantId == Guid.Empty
                                                  && currentTenant.TenantId is { } tid)
                moi.TenantId = tid;
            break;

        case EntityState.Modified:
            // Không cho đổi tenant của bản ghi đã tồn tại — đó là chuyển dữ liệu sang trung tâm khác.
            if (entry.Entity is ITenantEntity)
                entry.Property(nameof(ITenantEntity.TenantId)).IsModified = false;
            break;
    }
}
```

Hai điều đáng mang đi. **Gán tập trung, không để tầng ứng dụng nhớ** — chú thích trong mã nói
thẳng: *"Để tầng Application tự gán thì sớm muộn cũng có chỗ quên; gán tập trung ở đây khiến việc
quên trở nên bất khả thi."* Và **chặn sửa `TenantId` ở nhánh `Modified`** — nếu không, một DTO cập
nhật mang theo `TenantId` sẽ lặng lẽ chuyển bản ghi sang tenant khác. Cùng nguyên tắc áp cho cột
audit `CreatedAt`/`CreatedById`, để người sửa không âm thầm trở thành người tạo.

---

## 3. Những bẫy đã vấp thật

Mỗi bẫy dưới đây có bằng chứng trong repo gốc: chú thích tại chỗ, test canh, hoặc ADR.

### 3.1. `Expression.Constant(currentTenant)` bị "nướng cứng" vào context đầu tiên

Cách tự nhiên nhất để viết filter là trỏ thẳng vào object đã tiêm vào constructor:

```csharp
// SAI — model bị cache, object "nướng cứng" vào context ĐẦU TIÊN
Expression.Property(Expression.Constant(currentTenant), nameof(ICurrentTenant.TenantId))

// ĐÚNG — EF thay bằng instance đang chạy ở mỗi truy vấn
Expression.Property(Expression.Constant(this), nameof(TenantIdHienTai))
```

**Vì sao hỏng.** EF Core **cache model** và dùng chung cho mọi `DbContext` có cùng `options`;
`OnModelCreating` chạy **một lần** cho cả vòng đời ứng dụng. Với bản sai, expression tree giữ tham
chiếu cứng tới đúng object `ICurrentTenant` của request đầu tiên. Request thứ hai dùng model đã
cache, nên filter vẫn đọc tenant của request thứ nhất: **context của tenant B đọc tenant của A và
thấy dữ liệu của A**. Không exception, không log.

Bản đúng trỏ vào property của chính `DbContext`; EF nhận diện mẫu này và thay
`Expression.Constant(this)` bằng instance đang chạy ở mỗi truy vấn.

**Bằng chứng đã vấp thật.** Chú thích tại chỗ ghi: *"Đã kiểm chứng cả hai chiều: đổi về
`Expression.Constant(currentTenant)` làm `CachLyTenantTests` đỏ 2 test; đổi lại thì xanh."*

**Quy tắc tổng quát:** mọi giá trị động trong query filter phải đi qua một property của `DbContext`
— áp cả cho soft-delete, filter theo ngôn ngữ, theo chi nhánh.

### 3.2. Nhánh `TenantIdHienTai == null` tắt filter

Nhánh `== null` là **có chủ ý**: migration và seeder cấp hệ thống phải chạy được khi chưa có tenant.
Hệ quả: một context không có tenant **không trả về rỗng — nó trả về dữ liệu của mọi tenant**.

Chú thích tại chỗ biện minh bằng một tiền đề: *"Middleware bắt buộc mọi endpoint nghiệp vụ phải có
tenant, nên nhánh này không mở đường cho request thường đọc chéo trung tâm."* Câu đó đúng **chừng
nào mọi endpoint nghiệp vụ còn đòi JWT**. Thêm một endpoint ẩn danh (landing công khai, webhook,
healthcheck có đọc dữ liệu) là tiền đề sai, và lỗ hổng mở ngay.

**Bằng chứng.** Đây là nội dung ADR-0008 của dự án gốc, viết khi cần thêm trang landing công khai:

> *Một request landing không giải ra tenant sẽ không trả rỗng — nó tắt filter và trả dữ liệu của
> mọi trung tâm. […] Nó thất bại theo hướng mở, và thất bại im lặng.*

**Xử lý,** theo thứ tự ưu tiên:

1. **Đừng dùng nhánh `== null`.** Cho filter luôn so sánh `e.TenantId == TenantIdHienTai`; không
   tenant thì mọi truy vấn trả rỗng — *thất bại theo hướng đóng*. Seeder và migration dùng
   `IgnoreQueryFilters()` tường minh ở đúng chỗ cần.
2. Nếu giữ nhánh đó: middleware **phải chặn** request đã xác thực mà không giải được tenant (mã ở
   2.2), và mọi endpoint ẩn danh phải được **liệt kê tường minh** kèm giới hạn của nó.
3. Viết một test **đếm và chốt số endpoint ẩn danh**, để thêm cái mới phải sửa test — tức là phải
   có ý thức.

### 3.3. `UNIQUE(username)` toàn cục chặn hai tenant cùng có tài khoản `admin`

Ràng buộc viết theo phản xạ đơn-tenant. `UNIQUE(username)` nghe hợp lý — cho tới khi tenant thứ hai
đăng ký và cũng muốn có tài khoản `admin`. Lỗi hiện ra ở dạng khó hiểu nhất: "tên đăng nhập đã tồn
tại" cho một tên mà tenant này chưa bao giờ dùng.

**Quy tắc.** Trong shared-schema, **mọi ràng buộc duy nhất của dữ liệu nghiệp vụ phải có `tenant_id`
ở đầu**: `UNIQUE(tenant_id, username)`. Ngoại lệ chỉ dành cho thứ thật sự toàn cục: bảng định nghĩa
tenant, và token tra cứu trước khi biết tenant (refresh token, token đặt lại mật khẩu — tra theo
hash trước khi biết người dùng là ai).

**Hệ quả kiến trúc lớn hơn:** đây là lý do dự án gốc **không dùng cả ASP.NET Core Identity stack**
mà chỉ mượn `PasswordHasher` — Identity giả định username duy nhất toàn cục, trái thẳng với
multi-tenant. Hãy kiểm giả định này ở mọi thư viện xác thực trước khi kéo vào.

```csharp
[Fact]
public void UNIQUE_username_phai_gom_ca_tenant_id()
{
    var nguoiDung = db.Model.GetEntityTypes().Single(e => e.ClrType.Name == "NguoiDung");

    var chiUsername = nguoiDung.GetIndexes().Any(i =>
        i.IsUnique && i.Properties.Count == 1 && i.Properties[0].Name == "Username");

    Assert.False(chiUsername,
        "UNIQUE(username) toàn cục sẽ chặn hai trung tâm cùng có tài khoản 'admin'. "
        + "Phải là UNIQUE(tenant_id, username).");
}
```

Kèm một integration test tạo **cùng username ở hai tenant** và đòi cả hai thành công — chiều ngược.
Không có nó thì ai đó "sửa" lỗi trùng username bằng cách bỏ `tenant_id` khỏi UNIQUE mà bộ test vẫn xanh.

### 3.4. Query Filter lọc HÀNG, không lọc CỘT

Global Query Filter — và mọi tầng phân quyền phạm vi xây trên nó — quyết định **hàng nào** được
thấy. Không tầng nào quyết định **cột nào**. Một trường nhạy cảm đi nhờ DTO của module khác sẽ lọt
qua tất cả, vì endpoint đó gác bằng quyền của **module chủ DTO**, không phải quyền của dữ liệu nhạy cảm.

**Ví dụ minh hoạ từ dự án gốc** (nêu ra chỉ để cho thấy hình dạng của lỗi): số tiền học phí từng
nằm trong DTO của module lớp học. Endpoint đó gác bằng quyền "xem lớp" — quyền mà giáo viên và cả
người học đều có. Tầng lọc phạm vi hoạt động hoàn toàn đúng: mỗi người chỉ thấy **hàng** của lớp
mình. Nhưng trong những hàng đó có **cột tiền**. Hệ quả đo được bằng `curl` trước khi vá: giáo viên
đọc được mức miễn giảm của từng người, người học đọc được học phí của bạn cùng lớp.

**Quy tắc.** Trường nhạy cảm **không đi nhờ DTO của module khác**. Nếu buộc phải, gọi hàm kiểm
quyền của **module sở hữu dữ liệu** và trả `null` khi không đủ. Ghi thành luật ở đầu file test:
*"Thêm trường tiền vào bất kỳ DTO nào không thuộc module học phí → thêm một test ở đây."*

**Những chỗ khác Query Filter không với tới** — mỗi dự án nên giữ một bảng như thế này:

| Chỗ | Vì sao thủng | Xử lý |
|---|---|---|
| Raw SQL (`FromSqlRaw`, Dapper) | Filter là chuyện của LINQ provider | Tự thêm `WHERE tenant_id = @tenant` mọi câu |
| Thống kê / group-by nhiều bảng | Hay được viết dạng raw SQL | Điểm rủi ro cao nhất — review riêng |
| `IgnoreQueryFilters()` | Tắt filter hoàn toàn | Chỉ cho tác vụ quản trị, review kỹ |
| Kho tệp (S3/MinIO/đĩa) | Không có Query Filter | `tenantId` ở **đầu khoá tệp**, kiểm lại tiền tố khi đọc/xoá |
| Background job / cron | Không có HTTP context → không có claim | Truyền `tenant_id` **tường minh** vào job |
| Navigation từ entity chưa lọc | Kéo theo dữ liệu tenant khác | Luôn bắt đầu truy vấn từ entity **có** filter |
| Endpoint ẩn danh | Chưa có phiên → chưa có tenant | Mỗi cái tự giới hạn thứ nó tiết lộ; liệt kê thành bảng |

### 3.5. Kiểm `AnyAsync` rồi `Add` là bẫy race condition

Ràng buộc "chỉ một" rất dễ được cài bằng:

```csharp
if (await db.X.AnyAsync(...)) return Loi("DA_TON_TAI");
db.X.Add(moi);
await db.SaveChangesAsync();
```

Hai request song song đều chạy `AnyAsync` trước khi cái nào kịp ghi, nên **cả hai đều thấy "chưa
có" và cả hai đều ghi**. Năm request đồng thời cho ra năm bản ghi — lỗi chỉ xuất hiện dưới tải hoặc
khi người dùng bấm hai lần, nên nó lọt qua mọi test tuần tự.

**Bằng chứng.** Bộ test `DongThoiTests` mở đầu bằng: *"Bài học từ rà soát 20/08: ràng buộc kiểu này
rất dễ được kiểm bằng `AnyAsync` rồi `Add`."*

**Quy tắc.** Ràng buộc "chỉ một" phải là **UNIQUE INDEX ở tầng DB**. Kiểm ở tầng ứng dụng vẫn giữ,
nhưng vai trò của nó là **trả mã lỗi đẹp** cho trường hợp thường; UNIQUE index là **lưới cuối** cho
cuộc đua. Kèm theo: middleware bắt vi phạm ràng buộc và trả **400 kèm mã lỗi nghiệp vụ**, không
phải 500 — nếu không, người bấm hai lần thấy "Lỗi hệ thống" và tưởng app hỏng, trong khi lần thứ
hai bị chặn **đúng**.

**Hai cạm bẫy phụ của PostgreSQL, đều đã vấp:**

- **`NULL != NULL`.** `UNIQUE(tenant_id, parent_id, ten)` **không** chặn được hai bản ghi gốc cùng
  tên, vì `parent_id` của cả hai đều `NULL` và PostgreSQL coi hai `NULL` là khác nhau. Dự án gốc
  thử trực tiếp trên DB: hai dòng `(1, NULL, 'X')` đều insert được. Chữa bằng **partial unique
  index** với filter `parent_id IS NULL`.
- **Ràng buộc phụ thuộc trạng thái.** "Mỗi đơn chỉ một yêu cầu **đang chờ**" không viết được bằng
  `UNIQUE(don_id)` — thế sẽ chặn luôn việc gửi lại sau khi bị từ chối. Phải là unique index có
  filter theo cột trạng thái, và vì điểm quan trọng nằm ở **cái filter**, nó cần test riêng.

---

## 4. Cách canh không để hỏng về sau: "test chiều ngược"

Đây là ý tưởng đáng mang sang dự án khác nhất trong cẩm nang này.

**Vấn đề của test thông thường.** Test kiểu "mọi `ITenantEntity` đều có Query Filter" nghe có vẻ
đủ. Nó không đủ: nó chỉ kiểm những entity **đã** cài interface. Thêm một entity **quên** cài
interface là đủ để nó nằm ngoài mọi lớp bảo vệ tự động — và không test nào kêu. Hàng rào tự động có
một lỗ đúng bằng cỡ sự đãng trí của người viết code.

**Khuôn giải pháp — ba phần, luôn đi cùng nhau:**

1. **Test xuôi** — mọi thứ được đánh dấu đều được bảo vệ.
2. **Danh sách ngoại lệ có khai lý do** — một `Dictionary<string, string>` ánh xạ tên sang **lý do**,
   không phải `HashSet` chỉ chứa tên. Bắt viết ra lý do là mấu chốt: nó biến việc bỏ qua thành hành
   động có ý thức, để lại dấu vết cho người đọc sau.
3. **Test ngược** — mọi thứ **không** được bảo vệ phải nằm trong danh sách ngoại lệ. Đây là cái
   buộc người thêm entity mới dừng lại: hoặc viết ra lý do, hoặc nhận ra mình quên.

```csharp
/// Danh sách entity CỐ Ý không có Global Query Filter, kèm lý do.
/// Mỗi tên ở đây là một chỗ Query Filter KHÔNG bảo vệ, tức là chỗ phải tự lọc bằng tay và
/// dễ rò rỉ dữ liệu chéo trung tâm nhất. Danh sách phải ngắn và mỗi mục phải giải thích được.
private static readonly Dictionary<string, string> NgoaiLeKhongLoc = new()
{
    [nameof(Tenant)] = "Bảng ĐỊNH NGHĨA tenant, không thuộc tenant nào.",
    [nameof(RefreshToken)] = "Tra theo token trước khi biết tenant nào — xem XacThucNangCao.",
    [nameof(TokenDatLaiMatKhau)] = "Quên mật khẩu: chưa đăng nhập nên chưa có tenant.",
};

[Fact]
public void Khong_co_entity_nao_am_tham_thoat_khoi_query_filter()
{
    // Chiều ngược của test trên. Test kia hỏi "ITenantEntity có bị lọc không"; test này hỏi
    // "cái KHÔNG bị lọc có phải ngoại lệ có chủ ý không".
    var khongLoc = db.Model.GetEntityTypes()
        .Where(e => e.GetQueryFilter() is null)
        .Select(e => e.ClrType.Name)
        .Where(ten => !NgoaiLeKhongLoc.ContainsKey(ten))
        .ToList();

    Assert.True(
        khongLoc.Count == 0,
        $"Entity không có Query Filter mà chưa khai lý do: {string.Join(", ", khongLoc)}. " +
        "Nếu nó là dữ liệu của một trung tâm → cho kế thừa TenantEntity. Nếu cố ý nằm ngoài → " +
        "thêm vào NgoaiLeKhongLoc kèm lý do, và viết test canh việc tự lọc bằng tay.");
}
```

**Ba điểm làm nên chất lượng của khuôn này:**

- **Thông điệp lỗi nói phải làm gì, không chỉ nói cái gì sai.** Đoạn `Assert` trên liệt kê hai
  đường sửa. Người gặp test đỏ lần đầu — có thể là người mới, có thể là một phiên AI — cần biết
  đường ra ngay tại chỗ.
- **Test đọc model, không đọc dữ liệu.** `db.Model.GetEntityTypes()` chạy trên metadata của EF, nên
  test nhanh, không cần DB thật, chạy được ở mọi lần commit.
- **Dùng lại cho mọi bất biến kiến trúc khác.** Dự án gốc áp khuôn này cho bảy thứ: luật phụ thuộc
  giữa các tầng, cách ly tenant, ràng buộc UNIQUE, "mọi endpoint phải được gác quyền" (kèm **chốt
  số endpoint ẩn danh**), ranh giới giữa các module, "mọi entity phải có cấu hình EF", và "ma trận
  quyền khai báo phải khớp endpoint thực tế". Nếu một bất biến đủ quan trọng để viết vào tài liệu,
  nó đủ quan trọng để có một test chiều ngược.

### Biến thể cho ràng buộc UNIQUE

Cùng ý tưởng, dùng cho bẫy 3.5. Một `[Theory]` liệt kê mọi ràng buộc "chỉ một", mỗi dòng kèm chú
thích **vì sao** nó phải duy nhất:

```csharp
[Theory]
// Username chỉ duy nhất TRONG tenant, không phải toàn cục (quy tắc multi-tenant).
[InlineData("TaiKhoan", new[] { "TenantId", "Username" })]
[InlineData("Quyen",    new[] { "TenantId", "TenQuyen" })]
// `AnyAsync` rồi `Add` trong handler là bẫy kinh điển: hai request song song
// đều thấy "chưa có" và đều ghi.
[InlineData("LopHocKhoaHoc", new[] { "LopHocId", "KhoaHocId" })]
public void Rang_buoc_chi_mot_phai_co_UNIQUE_o_tang_DB(string tenEntity, string[] cot)
{
    var entity = db.Model.GetEntityTypes().Single(e => e.ClrType.Name == tenEntity);

    var index = entity.GetIndexes().FirstOrDefault(i =>
        i.Properties.Select(p => p.Name).SequenceEqual(cot));

    Assert.True(index is not null,
        $"{tenEntity} thiếu index trên ({string.Join(", ", cot)}) — "
        + "không thì hai request song song đều ghi được. "
        + "Kiểm ở tầng ứng dụng (`AnyAsync` rồi `Add`) KHÔNG đủ.");

    Assert.True(index!.IsUnique, $"Index trên {tenEntity} phải UNIQUE.");
}
```

Quy ước đi kèm, ghi vào tài liệu đóng góp: **thêm một ràng buộc "chỉ một" → thêm một dòng
`InlineData`**. Bảng này vừa là test, vừa là danh mục trung thực của mọi ràng buộc duy nhất.

Nên nói thẳng giới hạn ngay trong file test: provider InMemory **không mô phỏng được cuộc đua thật**
— nó không có UNIQUE index và không chạy song song ở tầng DB. Bộ test này canh phần kiểm được (ràng
buộc **được khai** trong model, tầng ứng dụng trả đúng mã lỗi), không canh phần chỉ DB thật mới
chứng minh được. Viết ra để không ai tưởng test xanh là đã chứng minh xong.

---

## 5. Danh sách kiểm khi thêm entity mới

**Cách ly dữ liệu**

- [ ] Entity kế thừa `TenantEntity` (hoặc cài `ITenantEntity`). Nếu **cố ý** không → thêm vào danh
      sách ngoại lệ **kèm lý do viết thành câu**, và nói rõ dữ liệu này được lọc bằng cách nào.
- [ ] Bảng chi tiết **cũng** mang `tenant_id` riêng, không dựa vào join lên bảng cha.
- [ ] Migration tạo cột `tenant_id` + khoá ngoại tới bảng tenant + **index** trên `tenant_id`.

**Ràng buộc**

- [ ] Mọi ràng buộc "chỉ một" là **UNIQUE INDEX ở tầng DB**, không chỉ `if` trong handler.
- [ ] Mọi UNIQUE của dữ liệu nghiệp vụ có `tenant_id` ở đầu — trừ khi có lý do toàn cục rõ ràng.
- [ ] Cột nullable tham gia UNIQUE → cân nhắc **partial index** (`NULL != NULL` trong PostgreSQL).
- [ ] Ràng buộc phụ thuộc trạng thái → partial index có filter, và test riêng cho cái filter.
- [ ] Mọi cột chuỗi khai `HasMaxLength` (không thì EF âm thầm tạo `text` vô hạn).

**Dữ liệu nhạy cảm**

- [ ] Không trường nhạy cảm nào đi nhờ DTO của module khác. Nếu buộc phải → kiểm quyền của **module
      sở hữu dữ liệu**, trả `null` khi không đủ, kèm một test canh.

**Đường đi vòng qua filter**

- [ ] Không raw SQL nào thiếu `WHERE tenant_id`. Không `IgnoreQueryFilters()` nào chưa review.
- [ ] Background job đụng entity này nhận `tenant_id` **tường minh**, không dựa vào ngữ cảnh request.
- [ ] Tệp đính kèm lưu với khoá mang `tenantId` ở **đầu**; đường đọc/xoá kiểm lại tiền tố.
- [ ] Endpoint ẩn danh mới → cập nhật bảng liệt kê **và** con số chốt trong test.

**Test**

- [ ] Test cách ly tenant: tạo ở tenant A, đăng nhập tenant B, xác nhận **không** đọc/sửa/xoá được
      — **kể cả khi truyền đúng `id`**.
- [ ] Thêm dòng `InlineData` vào bảng UNIQUE nếu entity có ràng buộc "chỉ một".
- [ ] Vi phạm ràng buộc trả **400 kèm mã lỗi**, không phải 500.

**Tài liệu**

- [ ] ERD và tài liệu API cập nhật **trong cùng PR**, không tách "làm sau".
