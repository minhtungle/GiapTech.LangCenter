# ADR-0009: Tài khoản cấp hệ thống ở bảng riêng, không phải cờ trên `TAI_KHOAN`

- **Ngày:** 23/09/2026
- **Trạng thái:** Đã chốt (chủ sản phẩm chốt phương án bảng riêng)
- **Bối cảnh liên quan:** [ADR-0008](0008-nhan-dien-tenant-qua-domain.md) (domain của tenant),
  [multi-tenant.md](../../03-backend/multi-tenant.md),
  [phan-quyen-dong.md](../../03-backend/phan-quyen-dong.md)

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

## Ba chỗ phải sửa khi hiện thực (23/09/2026)

Ba thứ chỉ lộ ra khi viết code thật, đều thuộc loại "hàng rào có sẵn chặn nhầm cả người nhà":

1. **`TenantMiddleware` chặn chính site chủ.** Token chủ cố ý không mang `tenant_id`, mà
   middleware trả 401 cho mọi token thiếu claim đó — nên site chủ không dùng được chút nào.
   Cho đi qua, **nhưng chỉ trên đường `/api/v1/chu-he-thong`**. Bản đầu tôi cho qua ở mọi
   đường và bốn test đỏ ngay: token chủ lọt vào `/hoc-vien`, `/lop-hoc` với `CurrentTenant`
   rỗng — mà tenant rỗng thì Query Filter **tắt hẳn**. Suýt đổi một lỗi 401 lấy lỗ hổng đọc
   chéo toàn hệ thống.

2. **`PhienDuyNhatMiddleware` chặn tiếp.** Cơ chế một-phiên xây trên `TAI_KHOAN.phien_hien_tai`
   và đòi claim `tai_khoan_id`; token chủ không có. Miễn `/api/v1/chu-he-thong` khỏi middleware
   này. Chấp nhận được vì chỉ có vài tài khoản chủ và mọi thao tác đều ghi nhật ký — cần
   một-phiên cho tài khoản chủ thì phải làm cơ chế riêng.

3. **EF tự dựng khoá ngoại cột audit sang `NGUOI_DUNG`.** Sai về khái niệm: `NGUOI_DUNG` thuộc
   tenant, tài khoản chủ đứng trên mọi tenant. Phải chặn **cả hai vế** — `Ignore` navigation
   (chặn quy ước) *và* loại trừ khỏi vòng lặp trong `AppDbContext` (chặn khai tường minh);
   thiếu một vế là migration vẫn sinh ra khoá ngoại.

## Giao diện (23/09/2026)

Hai màn, ở `/chu` và `/chu/trung-tam`, **ngoài** `<CanDangNhap>` và **không** bọc `<Layout />`
— Layout gọi `useQuyen`/`useHeThong`, hai thứ tra quyền theo tenant mà tài khoản chủ không có.

**Client HTTP riêng** (`lib/apiChu.ts`), không dùng chung `api.ts`. Hai lý do:

- `api.ts` có interceptor tự làm mới token qua `/auth/lam-moi-token` rồi đá về `/dang-nhap` của
  tenant — sai màn, và tài khoản chủ không có refresh token để làm mới.
- Nguy hiểm hơn: dùng chung một biến token trong RAM nghĩa là mở site chủ cùng tab với một
  phiên tenant thì hai bên **ghi đè nhau**.

Token chủ cũng giữ trong RAM, không `localStorage` (cùng lý do ADR-0007, và tài khoản này còn
đáng giá hơn). Đánh đổi **chấp nhận**: tải lại trang là phải đăng nhập lại — site dùng thưa nên
phiền ít, đổi lại không có refresh token nào để bị trộm.

Site chủ **không đa ngôn ngữ**: nó chỉ dành cho chủ sản phẩm, thêm 5 bản dịch cho hai màn là
chi phí không đổi lại gì.

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
