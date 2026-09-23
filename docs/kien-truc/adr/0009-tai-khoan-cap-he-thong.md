# ADR-0009: Tài khoản cấp hệ thống ở bảng riêng, không phải cờ trên `TAI_KHOAN`

- **Ngày:** 23/09/2026
- **Trạng thái:** Đã chốt (chủ sản phẩm chốt phương án bảng riêng)
- **Bối cảnh liên quan:** [ADR-0008](./0008-nhan-dien-tenant-qua-domain.md) (domain của tenant),
  [multi-tenant.md](../../backend/multi-tenant.md),
  [phan-quyen-dong.md](../../backend/phan-quyen-dong.md)

## Bối cảnh

Chủ sản phẩm cần một site riêng — ví dụ `host-langcenter.giaptex.com` — để tạo tenant mới, nhập
thông tin cơ bản và gắn domain cho họ.

Việc này đẻ ra một khái niệm hệ thống **chưa từng có**: người dùng **đứng trên mọi tenant**.

Tới nay mọi tài khoản đều thuộc đúng một tenant. Toàn bộ 45 bảng, Global Query Filter, phân
quyền động, bốn tầng bảo vệ — tất cả xây trên giả định đó. `TAI_KHOAN` là `ITenantEntity`, JWT
luôn mang `tenant_id`, `TenantMiddleware` chặn 401 nếu token thiếu claim ấy.

Đồng thời việc này **đóng một nợ đang mở**: endpoint `/api/v1/dang-ky-trung-tam` là endpoint ẩn
danh ai cũng gọi được, đã phải tắt mặc định từ 22/09/2026 (`CHO_TU_DANG_KY`, xem `API/TinhNang.cs`)
vì chủ sản phẩm chốt đóng hẳn — nợ N3. Site chủ thay nó bằng đường **có xác thực**.

## Quyết định

**Bảng `QUAN_TRI_HE_THONG` riêng, tách hẳn khỏi `TAI_KHOAN`. JWT mang loại danh tính khác hẳn.**

| | Tài khoản tenant | Tài khoản chủ hệ thống |
|---|---|---|
| Bảng | `TAI_KHOAN` (`ITenantEntity`) | `QUAN_TRI_HE_THONG` (**không** tenant) |
| Claim `tenant_id` | luôn có | **không bao giờ có** |
| Đăng nhập ở | domain tenant / đường mặc định | **chỉ** `host-langcenter.giaptex.com` |
| Gọi được API nghiệp vụ | có | **không** |
| Phân quyền | `QUYEN_CHUC_NANG` động | danh sách thao tác cố định, rất hẹp |

### Vì sao bảng riêng, không phải cờ `la_quan_tri_he_thong`

Phương án cờ trên `TAI_KHOAN` ít bảng hơn nhưng **sai về mặt an toàn**: mọi chỗ đọc `TAI_KHOAN`
từ nay phải nhớ kiểm cờ đó. Quên một chỗ là leo thang đặc quyền. Hệ thống hiện có hàng chục
handler đọc `TAI_KHOAN`, và không có gì bắt người viết mới phải nhớ.

Bảng riêng thì **không có đường nào** để một tài khoản tenant trở thành chủ hệ thống — hai bảng
không liên quan gì nhau. An toàn đến từ cấu trúc, không đến từ việc nhớ kiểm.

Phương án "một tenant đặc biệt làm chủ" bị loại thẳng: nó buộc phải chọc thủng Global Query
Filter để tenant đó đọc được tenant khác — đúng thứ quy tắc #2 cấm.

### Token chủ hệ thống phải *không thể* dùng cho API nghiệp vụ

Không đủ nếu chỉ đánh dấu "loại khác". Token chủ phải **cấu trúc không thoả mãn được**
`TenantMiddleware`.

Hiện `TokenService` luôn gắn `tenant_id`, và `TenantMiddleware` trả 401 `TOKEN_THIEU_TENANT` khi
token đã xác thực mà thiếu/hỏng claim ấy. Tức là **cơ chế chặn đã có sẵn**: token chủ không mang
`tenant_id` sẽ bị chính middleware đó chặn ở mọi endpoint nghiệp vụ, không cần thêm lớp nào.

Chiều ngược lại cũng phải chặn: endpoint của site chủ từ chối token có `tenant_id`. Một tài khoản
tenant không được chạm vào API tạo tenant dù có token hợp lệ.

Thêm claim `loai = "host"` để đọc ra ý định rõ ràng thay vì suy từ việc *thiếu* claim — nhưng
hàng rào thật là sự vắng mặt của `tenant_id`, không phải claim này.

## Site chủ làm được gì

Cố ý **rất hẹp**. Càng ít thao tác, càng ít thứ có thể bị lạm dụng nếu tài khoản chủ bị chiếm.

| Thao tác | Ghi chú |
|---|---|
| Xem danh sách tenant | tên, mã, domain, ngày tạo, trạng thái |
| Tạo tenant mới | thay `/api/v1/dang-ky-trung-tam` — đóng nợ N3 |
| Gắn / đổi domain | `domain_quan_tri`, `domain_landing` (ADR-0008) |
| Cấp lại mật khẩu admin của tenant | tenant mất đường vào thì đây là lối chữa |
| Tạm khoá / mở khoá tenant | ngừng dịch vụ, không xoá dữ liệu |

**Cố ý KHÔNG có:** đọc dữ liệu nghiệp vụ bên trong tenant (học viên, học phí, lớp học). Chủ hệ
thống quản **vòng đời tenant**, không nhìn vào ruột của họ. Muốn có cũng phải chọc thủng Query
Filter — và nếu sau này thật sự cần thì phải là một ADR mới, có lý do viết ra.

Xoá tenant cũng **không có**: xoá hàng loạt dữ liệu thật thuộc quy tắc #1, phải làm tay có backup.

## Hệ quả

**Tích cực**
- Đóng nợ N3: tự đăng ký ẩn danh thay bằng đường có xác thực.
- Không có đường leo quyền từ tenant lên chủ hệ thống — do cấu trúc, không do nhớ kiểm.
- Tenant mất domain vẫn cứu được (cấp lại mật khẩu admin, đổi domain).
- Query Filter giữ nguyên, không phải nới cho bất kỳ ai.

**Tiêu cực**
- **Thêm một bảng người dùng thứ hai.** Hai luồng xác thực song song, hai chỗ băm mật khẩu, hai
  chỗ có thể sai. Chấp nhận vì đổi lại là an toàn đến từ cấu trúc.
- **Tài khoản chủ là mục tiêu giá trị cao.** Chiếm được nó thì tạo tenant, đổi domain, cấp lại
  mật khẩu admin của mọi trung tâm. Bù lại bằng: chỉ đăng nhập được trên một domain, danh sách
  thao tác hẹp, và **mọi thao tác ghi nhật ký**.
- **Không có đường tự phục hồi nếu mất tài khoản chủ cuối cùng.** Giống `ChotConNguoiQuanTri` của
  tenant, cần một chốt tương tự: không cho xoá tài khoản chủ cuối cùng.

## Test bắt buộc

| Test | Canh gì |
|---|---|
| Token chủ gọi API nghiệp vụ → 401 | Hàng rào chính. Canh cả khi thêm endpoint mới |
| Token tenant gọi API site chủ → 403 | Chiều ngược lại |
| Token chủ không có claim `tenant_id` | Canh `TokenService` không lỡ gắn vào |
| Không xoá được tài khoản chủ cuối cùng | Giống `ChotConNguoiQuanTri` |
| Đăng nhập chủ chỉ chạy trên domain site chủ | Không đăng nhập được từ domain tenant |
| `QUAN_TRI_HE_THONG` nằm trong danh sách ngoại lệ của `CachLyTenantTests` **kèm lý do** | Test này hỏi chiều ngược: entity không bị lọc phải khai vì sao |

Dòng cuối quan trọng: `CachLyTenantTests` buộc mọi entity **không** bị Query Filter phải nằm
trong danh sách ngoại lệ có khai lý do. `QUAN_TRI_HE_THONG` là entity đầu tiên cố ý đứng ngoài
tenant kể từ `TENANT`, nên lý do phải viết rõ tại chỗ.
