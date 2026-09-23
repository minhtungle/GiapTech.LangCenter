# ADR-0008: Nhận diện tenant qua domain, giữ mã trung tâm làm đường vào thứ hai

- **Ngày:** 23/09/2026
- **Trạng thái:** Đã chốt (chủ sản phẩm chốt phương án hai đường vào)
- **Bối cảnh liên quan:** [ADR-0004](./0004-ha-tang-tu-host-vps.md) (Nginx trên 1 VPS),
  [ADR-0007](./0007-refresh-token-cookie-httponly.md) (cookie phiên),
  [multi-tenant.md](../../backend/multi-tenant.md)

## Bối cảnh

Tới 23/09/2026, tenant được nhận diện **chỉ bằng mã trung tâm** — người dùng gõ bộ ba
{mã trung tâm, tên đăng nhập, mật khẩu}, mã đi vào claim JWT, `TenantMiddleware` đọc claim và
nạp vào `CurrentTenant`, Global Query Filter lọc theo đó.

Cách này chạy tốt cho quản trị nội bộ nhưng chặn hai việc chủ sản phẩm muốn làm:

1. **Landing page công cộng** (FR-30). Khách vãng lai không có JWT và không bao giờ nên phải gõ
   mã trung tâm để xem trang giới thiệu của một trung tâm.
2. **Thương hiệu riêng.** Trung tâm muốn khách vào `vietgeneducation.edu.vn`, không phải một
   đường dùng chung kèm mã bảy ký tự.

### Vì sao đây là quyết định kiến trúc, không phải một tính năng

Toàn bộ 45 bảng nghiệp vụ dựa trên một giả định: **mọi request đều có tenant, lấy từ JWT đã ký**.
Landing phá đúng giả định đó. Và cách filter được viết khiến việc phá nó nguy hiểm hơn nhiều so
với vẻ ngoài.

`AppDbContext.cs` (`ApDungQueryFilterTheoTenant`) sinh filter:

```
e => TenantIdHienTai == null || e.TenantId == TenantIdHienTai
```

Nhánh `== null` là **có chủ ý** — migration và seeder cấp hệ thống cần chạy khi chưa có tenant.
Chú thích ngay tại đó ghi rõ vì sao nó an toàn:

> *"Middleware bắt buộc mọi endpoint nghiệp vụ phải có tenant, nên nhánh này không mở đường cho
> request thường đọc chéo trung tâm."*

Câu đó đúng **chừng nào mọi endpoint nghiệp vụ còn đòi JWT**. Endpoint ẩn danh của landing làm
tiền đề ấy sai. Hệ quả cụ thể: một request landing **không giải ra tenant** sẽ không trả rỗng —
nó **tắt filter và trả dữ liệu của mọi trung tâm**.

Đây là kiểu hỏng tệ nhất có thể: nó **thất bại theo hướng mở**, và thất bại **im lặng** — trang
vẫn hiện, chỉ là hiện nội dung của trung tâm khác. Không có lỗi, không có log, không có gì để ai
nhận ra.

## Quyết định

**Tenant giải được bằng hai đường, cả hai luôn sống, cùng một nhánh mã.**

| Đường vào | Ai quyết tenant | Ô mã trên màn đăng nhập |
|---|---|---|
| `vietgeneducation-langcenter.giaptex.com` (domain đã gắn) | **domain** | **ẩn** — hiện tên trung tâm |
| `langcenter-giaptex.com` (đường mặc định) | **mã** người dùng gõ | hiện |
| `localhost:5173` (local, E2E) | **mã** người dùng gõ | hiện |

Landing tương ứng:

| Đường vào | Kết quả |
|---|---|
| `vietgeneducation.edu.vn` | landing của tenant gắn domain đó |
| `langcenter-giaptex.com/t/W686AE9` | landing của W686AE9 — xem thử khi chưa trỏ DNS |
| `localhost:5173/t/W686AE9` | y hệt, dùng để phát triển |

### Vì sao hai đường thay vì chỉ domain

Phương án "chỉ domain riêng, chưa trỏ thì chưa dùng được" đã được cân nhắc và **bỏ**, vì ba cái
giá của nó đều rơi vào lúc tệ nhất:

- **Domain hỏng = mất đường vào hoàn toàn.** Khách quên gia hạn domain, đổi nhà cung cấp DNS,
  certbot không tự gia hạn được → cả trung tâm không ai đăng nhập được, kể cả người vận hành.
  Đường chữa duy nhất là sửa tay ở tầng VPS.
- **Tenant mới nằm chờ DNS.** Tài khoản admin và mật khẩu ngẫu nhiên cấp lúc tạo, nhưng chưa
  dùng được — chờ vài giờ tới hai ngày thì mật khẩu dễ thất lạc.
- **Local thành ngoại lệ.** Nếu chỉ có đường domain, local (không có domain) phải chạy một nhánh
  mã khác. Tức là nhánh nguy hiểm nhất — nhánh giải tenant — lại là nhánh **không được test ở
  local**. Hai đường cùng sống thì local chạy đúng đường mà VPS cũng chạy.

### Vì sao KHÔNG rẽ nhánh theo `IsDevelopment()`

`CookiePhien` có tiền lệ `Secure = !moiTruong.IsDevelopment()`. **Không áp dụng kiểu đó ở đây.**

Cờ `Secure` sai ở dev chỉ gây phiền lúc phát triển. Giải tenant sai thì **lộ dữ liệu chéo trung
tâm**. Thứ gì canh giữ cách ly tenant thì không được đổi hành vi theo môi trường — vì như vậy
đường đi thật trên production lại là đường chưa ai chạy thử.

Local không phải "chế độ dev". Local chỉ là môi trường mà đường (1) tra không ra gì nên dùng
đường (2) — đúng như một tenant trên VPS chưa trỏ domain.

## Header `Host` không đáng tin

Đây là ràng buộc an ninh cứng của ADR này.

`Host` là thứ **client gửi lên**, không phải thứ server biết. Nếu API tra tenant thẳng từ `Host`,
kẻ tấn công gửi `Host: vietgeneducation.edu.vn` tới bất kỳ đường nào cũng **tự chọn được tenant**.

Nguy hiểm gấp đôi vì nginx hiện `include /etc/nginx/proxy_params` (4 chỗ trong
`deploy/nginx/langcenter.conf`), mà bản mặc định của file đó trên Debian/Ubuntu chứa
`proxy_set_header Host $http_host` — tức **chuyển tiếp nguyên Host của client xuống API**.

> File `proxy_params` nằm trên VPS, không có trong repo, nên **phải kiểm tận nơi trước khi làm**:
> `cat /etc/nginx/proxy_params`. Nếu nó thật sự có dòng `Host $http_host` thì đó là đường để
> client tự chọn tenant, và bước 1 dưới đây là bắt buộc chứ không phải phòng xa.

**Cách làm bắt buộc:**

1. Nginx là nguồn sự thật duy nhất về domain. Nó chỉ phục vụ `server_name` đã cấu hình, rồi
   truyền xuống bằng **header riêng do chính nó đặt**, ví dụ `X-Tenant-Domain $server_name` —
   dùng `$server_name` (giá trị trong cấu hình), **không** dùng `$host`/`$http_host` (giá trị
   client gửi).
2. API **xoá sạch header đó** nếu request không đến từ reverse proxy tin cậy. Header do client
   tự đặt không bao giờ được đọc tới.
3. Tra domain không ra tenant → **từ chối ngay tại middleware**, không bao giờ chạy tiếp với
   `CurrentTenant` rỗng.
4. Request đến từ domain đã gắn tenant thì **bỏ qua mã trung tâm client gửi lên**. Nếu không,
   domain chỉ là gợi ý: người dùng ở `vietgeneducation.edu.vn` xoá mã đi gõ mã trung tâm khác là
   domain hết tác dụng ràng buộc.

### Ẩn ô mã, không chỉ điền sẵn

Trên domain đã gắn, màn đăng nhập **ẩn hẳn** ô mã và hiện tên trung tâm. Không phải vì thẩm mỹ:
ô mã còn sửa được nghĩa là domain không chốt được tenant. Ẩn ô mã ở client, **bỏ qua mã ở
server** — hai vế phải đi cùng nhau.

## Schema

Thêm vào `TENANT`:

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `domain_quan_tri` | `varchar(253)` NULL | UNIQUE. Domain vào màn quản trị. |
| `domain_landing` | `varchar(253)` NULL | UNIQUE. Domain landing công khai. |

Cả hai **nullable** — tenant chưa trỏ domain vẫn hoạt động đầy đủ qua đường mã.

UNIQUE phải là **UNIQUE INDEX ở tầng DB** (quy tắc #8), không phải `if` trong handler: hai
request song song cùng gắn một domain thì cả hai đều thấy "chưa ai dùng" và đều ghi. Thêm một
dòng `InlineData` vào `DongThoiTests`.

253 ký tự là giới hạn độ dài tên miền theo RFC 1035.

**Không có cột `trang_thai_domain`.** Phương án trước có nó để đánh dấu "chờ xác minh DNS"; với
hai đường vào thì không cần — tenant không bao giờ bị khoá vì domain chưa sẵn sàng. Bớt một enum
trạng thái và bớt một loại lỗi vận hành.

## Hệ quả

**Tích cực**
- Landing công khai khả thi mà không nới lỏng cách ly tenant.
- Trung tâm dùng được thương hiệu riêng.
- Người dùng trên domain riêng không phải gõ mã.
- Đóng nợ N3 gián tiếp: site chủ hệ thống (ADR-0009) thay endpoint tự đăng ký ẩn danh.
- Local và E2E **không đổi gì** — vẫn gõ mã như hiện nay.

**Tiêu cực**
- **Wildcard SSL không dùng được.** Domain của các tenant khác nhau hoàn toàn
  (`vietgeneducation.edu.vn` ≠ `giaptex.com`), nên mỗi tenant có domain riêng cần một chứng chỉ
  certbot riêng. Thêm tenant là thao tác trên VPS, không tự phục vụ được.
- **Nginx phải sinh thêm server block** cho mỗi domain. Hiện `langcenter.conf` chỉ một
  `server_name` cứng.
- **Khách phải tự trỏ DNS.** Ngoài tầm kiểm soát của hệ thống.
- **Mỗi domain một phiên riêng.** Cookie `lms_rt` gắn theo origin, nên đăng nhập ở
  `langcenter-giaptex.com` không mang sang `vietgeneducation-langcenter.giaptex.com`. Về cách ly
  thì **tốt hơn**, nhưng phải nói trước để không bị coi là lỗi.

## Test bắt buộc

| Test | Canh gì |
|---|---|
| `Host` giả không đổi được tenant | Gửi `Host`/`X-Tenant-Domain` do client tự đặt → không được chọn tenant |
| Domain lạ không trả dữ liệu mọi tenant | Tra hụt → từ chối, **không** rơi về `CurrentTenant` rỗng |
| Domain đã gắn thì bỏ qua mã client gửi | Gửi mã trung tâm khác → vẫn ra tenant của domain |
| Hai đường ra cùng một tenant | Cùng dữ liệu qua domain và qua mã |
| `DongThoiTests` | Hai request song song không gắn được cùng một domain |

Test thứ hai là quan trọng nhất: nó canh đúng nhánh `TenantIdHienTai == null` ở
`AppDbContext.cs` — chỗ mà sai lầm sẽ im lặng và rộng.
