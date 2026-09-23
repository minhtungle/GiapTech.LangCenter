# Module Đăng nhập (FR-01, FR-02)

## FR-01 — Đăng nhập

Xác thực bằng **bộ ba**: `{mã trung tâm, tên đăng nhập, mật khẩu}`. Username chỉ duy nhất **trong
phạm vi một tenant** — hai trung tâm khác nhau có thể cùng có tài khoản `admin`.

### Mã trung tâm

**7 ký tự, do hệ thống sinh tự động** — người dùng không tự đặt.

| Quyết định | Lý do |
|---|---|
| Sinh tự động, không cho tự đặt | Tên trung tâm rất dễ trùng (nhiều nơi cùng tên "Ngoại ngữ ABC"), mà mã phải duy nhất toàn hệ thống |
| Bộ 31 ký tự: `23456789ABCDEFGHJKMNPQRSTUVWXYZ` | Bỏ `0/O` và `1/I/L` — người dùng phải đọc mã qua điện thoại và chép tay, nhầm 0 với O là lỗi hay gặp. Vẫn còn 31⁷ ≈ 27 tỷ tổ hợp |
| Không phân biệt hoa/thường | Lưu dạng hoa, chuẩn hoá khi so sánh. Gõ `a3k9m2p` hay `A3K9M2P` đều vào được |
| Không cho sửa (FR-06) | Người dùng gõ mã mỗi lần đăng nhập; đổi mã sẽ khoá cả trung tâm ra ngoài |

Màn đăng ký hiển thị mã to kèm nút sao chép và cảnh báo ghi lại — mã sinh tự động mà người dùng
không lưu thì họ mất đường vào, và không có cách tự tra lại.

Cài đặt: `Domain/Common/MaTrungTam.cs`, kiểm chứng bởi `MaTrungTamTests`.

### Tra tên trung tâm ngay trên trang đăng nhập

Gõ đủ 7 ký tự thì trang hiện luôn tên trung tâm tương ứng; sai thì hiện "Không tìm thấy".
Trước đó gõ sai mã chỉ biết sau khi điền hết form và nhận "sai thông tin đăng nhập" — không phân
biệt được sai mã hay sai mật khẩu.

`GET /auth/ten-trung-tam/{maTrungTam:length(7)}` · `[AllowAnonymous]` · chỉ trả
`{ "tenTrungTam": "..." }`.

**Không có đường tìm theo tên.** Yêu cầu ban đầu của chủ sản phẩm có cả phần đó (hiện các đội gần
giống để chọn), nhưng endpoint này buộc phải ẩn danh — người dùng đang **ở** trang đăng nhập nên
chưa thể có token. Cho tìm theo tên nghĩa là ai cũng liệt kê được toàn bộ trung tâm kèm mã. Chủ sản
phẩm chốt: *"thôi, giờ chỉ tìm theo mã trung tâm chính xác."*

Ba trạng thái trên giao diện, trạng thái đầu là chỗ dễ sai nhất:

| Ô mã | Hiện gì |
|---|---|
| Chưa đủ 7 ký tự | Câu gợi ý "7 ký tự, không phân biệt hoa thường" — **không** được hiện "không tìm thấy" khi người dùng còn đang gõ |
| Đủ 7, có trung tâm | `✓ <tên trung tâm>` |
| Đủ 7, không có trung tâm | "Không tìm thấy đội tương ứng" |

**Điểm căng có ý thức với quy tắc ở mục Luồng bên dưới** ("không tiết lộ tenant nào tồn tại"):
endpoint này *có* tiết lộ một mã trung tâm là có thật. Đánh đổi được chấp nhận vì mã trung tâm **không phải bí
mật** — nó được in ra cho cả trung tâm dùng để đăng nhập, dán trên nhóm chat, đọc qua điện thoại. Điều
phải giữ bí mật là *username + mật khẩu*, và endpoint này không nói gì về chúng. Quy tắc kia vẫn
áp nguyên cho chính lệnh đăng nhập: sai mã, sai username, sai mật khẩu đều trả **cùng một mã lỗi**.

Canh bởi `TraTenTrungTamTests` (12 test) và `e2e/dang-nhap-tra-ma.spec.ts` (3 test). Nợ **N3 rate limit
ở Caddy** thành chặn cứng trước khi mở ra Internet — xem
[năm endpoint ngoài tenant](../03-backend/multi-tenant.md#năm-endpoint-đọcghi-ngoài-tenant-xếp-theo-mức-rộng).

### Luồng

1. Người dùng nhập ID đội + username + password.
2. Hệ thống resolve tenant từ ID đội; nếu không tồn tại → trả mã lỗi chung (không tiết lộ tenant nào tồn
   tại).
3. Xác thực username + password trong phạm vi tenant đó.
4. Nếu tài khoản có cờ `phai_doi_mk = true` → **chuyển hướng sang màn đổi mật khẩu trước khi vào hệ
   thống**, không cấp quyền truy cập chức năng nào khác.
5. Cấp access token (JWT, chứa claim `tenant_id`) + refresh token.

### Quy tắc

- Tài khoản admin mặc định mỗi tenant là `admin` / `123456` với `phai_doi_mk = true` — bắt buộc đổi ở
  lần đăng nhập đầu tiên.
- Thông báo lỗi đăng nhập sai **không phân biệt** "sai username" hay "sai password" — trả cùng một mã lỗi.
- Mật khẩu lưu hash bằng thuật toán chuẩn của ASP.NET Core Identity.

### Nhận diện trung tâm ở màn đăng nhập (22/09/2026)

Yêu cầu chủ sản phẩm: *"nhập đúng mã trung tâm tại đăng nhập sẽ load đúng thông tin trung tâm
như trong thiết lập (logo, tên, ...), bên trong giao diện quản trị cũng vậy"*.

`GET /auth/ten-trung-tam/{ma}` (ẩn danh) nay trả **ba trường**, không còn chỉ tên:

| Trường | Vì sao an toàn |
|---|---|
| `tenTrungTam` | vốn đã hiện từ trước |
| `tenVietTat` | trung tâm tự đặt, vẫn in trên biển hiệu |
| `coLogo` | **cờ boolean, KHÔNG phải khoá ảnh** — khoá mang `tenantId` ở đầu |

**Không** trả địa chỉ, liên hệ, id — dù chúng cũng nằm trong Thiết lập. Ai dò trúng mã 7 ký tự
cũng đọc được những gì endpoint này trả. Canh bởi
`TraTenTrungTamTests.Chi_tra_NHAN_DIEN_khong_tra_gi_khac` (khoá cứng danh sách field).

#### Logo: endpoint riêng, không mở endpoint ảnh dùng chung

`GET /auth/logo/{maTrungTam}` — ẩn danh, `EnableRateLimiting(TraCuu)`.

Không dùng `GET /anh/{khoa}` cho việc này: endpoint đó nhận **khoá tự do** và gác bằng
`Anh.Xem`; mở cho người chưa đăng nhập là mở luôn **ảnh học viên, ảnh CCCD, ảnh QR chuyển
khoản**. Ở đây người gọi chỉ đưa mã trung tâm, server tự tra khoá trong DB.

Mã sai và trung tâm chưa có logo **đều trả 404** — phân biệt là cho người dò biết mã nào có
thật, tức thu hẹp không gian dò từ 27 tỷ xuống danh sách trung tâm có thật.

> **Bẫy khi làm**: `MinioLuuTruAnh.TaiVe` cố ý từ chối khi không biết tenant hiện tại (quy tắc
> #2 — khoá đến từ URL nên phải kiểm tiền tố). Người gọi endpoint này chưa đăng nhập ⇒ `TaiVe`
> trả null ⇒ **404 dù logo có thật**. Không nới chốt chặn đó; controller tự đặt phạm vi bằng
> `ICurrentTenant.DatPhamVi(tenantId)` — id do **server** tra từ mã, không phải người gọi đưa.

Đây là **endpoint ẩn danh thứ 7**; `MoiEndpointPhaiDuocGacTests` đã cập nhật số kèm lý do.

#### Bên trong: sidebar lấy nhận diện từ `/toi/cau-hinh`

`/thiet-lap` gác bằng `ThietLapChung.Xem` — **giáo viên và học viên nhận 403**, mà họ cũng nhìn
sidebar. Nên `/toi/cau-hinh` (mọi vai trò đọc được, vốn đã có cho múi giờ) trả thêm
`tenTrungTam` · `tenVietTat` · `khoaLogo`. Chỉ ba trường nhận diện — không số tài khoản ngân
hàng, không ảnh QR, không ngưỡng cảnh báo nợ.

Ở đây trả **khoá thật** (khác endpoint ẩn danh chỉ trả cờ): người gọi đã đăng nhập và đã thuộc
tenant này, khoá không lộ thêm gì.

**Không đọc tên từ JWT nữa**: token chỉ mang tên **lúc đăng nhập**, nên đổi tên ở Thiết lập thì
sidebar vẫn hiện tên cũ tới khi đăng nhập lại. Canh bởi `e2e/nhan-dien-trung-tam.spec.ts` —
test **đổi tên rồi mới kiểm**, vì giữ nguyên tên thì hai nguồn cho cùng kết quả và mutation
sống (đã xảy ra thật 22/09).

### Bố cục màn đăng nhập: banner trái · form phải · footer (22/09/2026)

Chủ sản phẩm: *"màn hình đăng nhập đang hơi trống — đưa khung đăng nhập sang phải, bên trái để
hiển thị 1 khung banner được setting trong thiết lập như logo, nếu chưa có hãy để mặc định,
nhớ làm responsive... footer có 1 dòng bản quyền phần mềm của GiapTex"*.

**Banner có BA trạng thái**, không phải hai:

| Khi nào | Hiện gì |
|---|---|
| Chưa gõ mã / mã sai | **Ảnh học tập mặc định** + tên phần mềm + câu giới thiệu |
| Đúng mã, chưa có ảnh bìa | Ảnh mặc định + logo + tên + mô tả + địa chỉ |
| Đúng mã, đã có ảnh bìa | **Ảnh bìa của trung tâm** thay ảnh mặc định |

**Ảnh mặc định là ảnh THẬT về học tập** (22/09/2026, lần 3) — `assets/banner-hoc-tap.jpg`, xem
`banner-hoc-tap.NGUON.md` cho nguồn, giấy phép và lý do chọn. Đã xem ba ứng viên và loại hai:
ảnh lớp học có chữ trên màn chiếu (chọi với chữ phủ lên), ảnh sách + khối ABC (đọc ra lớp trẻ
em, trong khi trung tâm ngoại ngữ dạy cả người lớn).

Ba lớp chồng nhau, lớp dưới lộ ra khi lớp trên hỏng: **gradient thương hiệu** → **ảnh mặc
định** (đóng gói trong bundle, không đi mạng) → **ảnh bìa trung tâm**. Lớp phủ tối dùng
gradient **chéo** (`bg-gradient-to-tr`), đậm nhất ở góc dưới-trái nơi có logo và tên.

Trạng thái đầu hay bị quên: người dùng mở trang là thấy ngay, lúc đó chưa gõ gì. Để trống thì
nửa màn hình trắng — đúng cái *"hơi trống"* cần chữa.

**Ảnh bìa dùng `<img>` phủ kín, không `background-image`**: `onError` bắt được ảnh hỏng (bị xoá
khỏi kho, MinIO chết) và ẩn đi, để lộ gradient bên dưới. `background-image` hỏng thì chỉ còn
khoảng trống, không có cách nào biết.

**Logo đặt trên nền trắng** (`bg-white/95`): logo thường là ảnh tối trên nền trong suốt, đặt
thẳng lên nền xanh đậm thì gần như biến mất.

#### Responsive: màn hẹp ẩn hẳn banner

`hidden lg:flex` — dưới 1024px banner biến mất, form chiếm trọn màn. Nhồi cả hai vào màn điện
thoại thì form bị đẩy xuống dưới nếp gấp, người dùng phải cuộn mới đăng nhập được: tệ hơn hẳn
so với không có banner. Nhận diện trung tâm vẫn thấy ở thẻ dưới ô mã.

> Test kiểm `toBeHidden()`, **không** `toHaveCount(0)`: `hidden lg:flex` ẩn bằng CSS nên phần
> tử vẫn nằm trong DOM. Kiểm số lượng thì đỏ oan dù giao diện đúng — đã gặp khi viết test.

#### Tên phần mềm và nút tạo trung tâm

Tên rút gọn thành **"LangCenter"** (22/09/2026) — bỏ tiền tố "GiapTech". Dòng bản quyền vẫn ghi
*"Phần mềm được phát triển bởi GiapTex"*.

**Tự đăng ký trung tâm ĐÓNG mặc định** từ 22/09/2026. Chủ sản phẩm yêu cầu *"ẩn nút tạo trung
tâm"*, và khi được hỏi đã chọn **đóng hẳn chức năng** chứ không chỉ ẩn lối vào — chỉ tắt cờ thì
ai biết đường dẫn vẫn `curl` tạo được tenant.

| | |
|---|---|
| Cờ | `CHO_TU_DANG_KY` (mặc định **tắt**) — `API/TinhNang.cs` |
| Endpoint | `POST /dang-ky-trung-tam` trả **404** khi tắt (không phải 403: 403 xác nhận endpoint tồn tại) |
| Giao diện | `/tinh-nang` khai `dangKyTrungTam=false` ⇒ ẩn link |

**Mặc định TẮT chứ không mặc định bật**: quên cấu hình ở môi trường thật thì hậu quả là "không
ai tạo được trung tâm" (phiền, dễ thấy) chứ không phải "ai cũng tạo được" (âm thầm, nguy hiểm).

> ⚠️ **Bộ E2E cần bật lại** — mỗi test tự tạo một trung tâm qua endpoint này. Chạy API với
> `CHO_TU_DANG_KY=true` (xem CLAUDE.md mục 7). `ApiFactory` tự bật cho integration test.

Cờ và chốt chặn đọc chung **một nguồn** (`TinhNang.ChoTuDangKy`): hai chỗ tự đọc cấu hình riêng
sẽ có ngày lệch nhau — cờ nói "bật" mà endpoint 404, đúng cái ngõ cụt mà cờ sinh ra để tránh.
Canh bởi `TinhNangTests.Co_khop_voi_hanh_vi_that_cua_endpoint_dang_ky`.

#### Footer: chỉ dòng bản quyền

Chủ sản phẩm chốt: footer **chỉ có dòng bản quyền GiapTex**; thông tin trung tâm nằm ở banner
(chỉ hiện sau khi gõ đúng mã). Người chưa biết mã không đọc được gì về trung tâm — cùng lý lẽ
với việc endpoint ẩn danh không trả `lienHe`.

Footer trải **ngang cả hai cột**, không nằm trong cột phải: nằm trong cột phải thì nó dừng ở
mép banner, nhìn như bị cắt.

#### Dữ liệu cho banner

Endpoint tra mã (ẩn danh) trả thêm `moTa`, `diaChi`, `coAnhBia`. Ảnh bìa có endpoint riêng
`GET /auth/anh-bia/{maTrungTam}` — cùng khuôn và cùng lý lẽ với `/auth/logo/{ma}`.

**`diaChi` rời khỏi danh sách cấm**, nhưng `lienHe` thì **giữ nguyên**: số điện thoại là thứ
người dò dùng được ngay, khác hẳn một dòng địa chỉ vốn in trên biển hiệu.

Đây là **endpoint ẩn danh thứ 8**; `MoiEndpointPhaiDuocGacTests` đã cập nhật số kèm lý do.

## FR-02 — Quên mật khẩu

1. Người dùng nhập email đã đăng ký (kèm ID đội để xác định tenant).
2. Hệ thống gửi email chứa **link/mã đặt lại mật khẩu có thời hạn**.
3. Người dùng đặt lại mật khẩu qua link; token hết hạn hoặc đã dùng thì không chấp nhận.

### Quy tắc

- Phản hồi cho người dùng **luôn giống nhau** dù email có tồn tại hay không — tránh dò email hợp lệ.
- Token đặt lại dùng một lần, có thời hạn ngắn.
- Kênh gửi: SMTP (xem [biến môi trường](../07-ha-tang/bien-moi-truong.md)).

## Phiên đăng nhập & refresh token

| Cơ chế | Quyết định | Vì sao |
|---|---|---|
| Lưu trữ | DB giữ **hash SHA-256**, không giữ token thô | Người đọc được DB (backup rò rỉ, SQL injection) không mạo danh được ai |
| Xoay vòng | Token cũ **thu hồi ngay** khi cấp token mới | Bản sao bị lộ chỉ dùng được tới lần làm mới kế tiếp, không sống tới ngày hết hạn |
| Phát hiện đánh cắp | Dùng lại token **đã thu hồi** → thu hồi **toàn bộ** phiên | Tái sử dụng là dấu hiệu có bản sao trong tay người khác; thà buộc đăng nhập lại còn hơn để phiên bị chiếm chạy tiếp |
| Đổi mật khẩu | Thu hồi mọi phiên đang mở | Đổi mật khẩu thường là phản ứng khi nghi bị lộ |
| Hạn | Access 60 phút · Refresh 30 ngày | |
| **Một phiên mỗi tài khoản** (20/09/2026) | Đăng nhập mới **đẩy phiên cũ ra**, hiệu lực **ngay** | Yêu cầu chủ sản phẩm *"chỉ cho phép 1 người đăng nhập tài khoản cùng lúc"* |

### Một phiên mỗi tài khoản (20/09/2026)

Chốt phương án **đẩy phiên CŨ ra** (người vừa đăng nhập được vào, như Facebook/Zalo) thay vì
chặn người mới: ai quên đăng xuất ở máy khác vẫn tự vào được, không phải nhờ quản trị.

**Vì sao không chỉ thu hồi refresh token.** JWT là stateless — server không tra DB mỗi request.
Thu hồi refresh token thôi thì phiên cũ vẫn gọi API bình thường tới **60 phút** (hạn access
token). Một tiếng hai người dùng song song thì không còn là "chỉ 1 người cùng lúc". Nên phải
chặn ở middleware.

Cách làm — ba mảnh, thiếu một là hở:

| Mảnh | Việc |
|---|---|
| `TAI_KHOAN.phien_hien_tai` | Lưu `jti` của access token phát ở lần đăng nhập gần nhất |
| `PhienDuyNhatMiddleware` | Mỗi request so `jti` trong token với cột đó; lệch ⇒ **401 `PHIEN_DA_BI_DAY_RA`** |
| Handler đăng nhập | Ghi `jti` mới **và** thu hồi mọi refresh token cũ (để phiên cũ không tự sống lại) |

**Dùng lại `jti` có sẵn, không thêm claim mới** (quy tắc #1): thêm claim là thay đổi phá vỡ
tương thích với mọi token đang lưu hành — người đang mở app bị đá ra ngay lúc triển khai.

**Hai trường hợp cố ý CHO QUA**: token không có `jti`, và `phien_hien_tai` rỗng (token phát
trước 20/09/2026). Chặn thì đá hàng loạt người đang dùng; họ vào khuôn khổ ở lần đăng nhập kế.

**Làm mới token là CÙNG một phiên đi tiếp** — `LamMoiTokenHandler` phải chuyển `phien_hien_tai`
sang `jti` mới. Thiếu bước này thì cứ 60 phút người dùng lại bị chính mình đá ra;
`Lam_moi_token_tra_ve_cap_token_moi` bắt được ngay.

**Cache 10 giây** theo tài khoản (đọc cột này ở mọi request là đắt), nhưng **xoá ngay khi đăng
nhập / làm mới token** qua `IPhienService`. Không xoá thì chính người vừa đăng nhập bị chặn tới
khi cache hết hạn — đã gặp thật khi kiểm chứng: máy B đăng nhập xong gọi API nhận **401**.

> **Giới hạn đã biết**: đổi mật khẩu thu hồi refresh token của phiên khác nhưng **không** đổi
> `phien_hien_tai`, nên access token của máy kia còn dùng được tối đa 60 phút. Muốn chặt hơn thì
> phải đưa `jti` vào `ICurrentUser` — chưa làm vì ngoài phạm vi yêu cầu.

## Trạng thái triển khai

| Endpoint | Mã | Ghi chú |
|---|---|---|
| `POST /api/v1/auth/dang-nhap` | FR-01 | Trả `phaiDoiMatKhau` để frontend điều hướng |
| `POST /api/v1/auth/doi-mat-khau` | FR-01 | Người dùng tự đổi; thu hồi phiên cũ |
| `POST /api/v1/auth/lam-moi-token` | FR-01 | Xoay vòng + phát hiện tái sử dụng |
| `POST /api/v1/auth/quen-mat-khau` | FR-02 | **Luôn trả 204** dù email có tồn tại hay không |
| `POST /api/v1/auth/dat-lai-mat-khau` | FR-02 | Token hạn 30 phút, dùng **một lần**, thu hồi mọi phiên |

Chưa cấu hình `SMTP_HOST` (môi trường dev) thì email được ghi log thay vì gửi — luồng vẫn chạy
đầu-cuối mà không cần dựng SMTP thật.

## Tham chiếu

- Bảng `NGUOI_DUNG`, `TENANT`, `REFRESH_TOKEN`, `TOKEN_DATLAI_MATKHAU` — xem [ERD](../05-database/erd.md).
- Cấu hình JWT & Identity — xem [SECURITY.md](../../SECURITY.md).
