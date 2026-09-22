# ADR-0007: Refresh token vào cookie `httpOnly`, access token vào bộ nhớ

- **Ngày:** 22/09/2026
- **Trạng thái:** Đã chốt (chủ sản phẩm duyệt phương án đầy đủ)
- **Bối cảnh liên quan:** [`docs/ra-soat-bao-mat-dang-nhap.md`](../../ra-soat-bao-mat-dang-nhap.md)
  (mục 7), [ADR-0002](./0002-frontend-shadcn-admin.md) (React SPA),
  [ADR-0004](./0004-ha-tang-tu-host-vps.md) (Nginx trên 1 VPS)

## Bối cảnh

Rà soát bảo mật 22/09/2026 vá 7/8 mục. Mục còn lại là **đánh đổi kiến trúc**, không phải lỗi cài
đặt, nên tách riêng ra ADR này.

Hiện `frontend/src/lib/api.ts` lưu **cả hai token** trong `localStorage`:

```ts
localStorage.setItem('lms_access_token', access)
localStorage.setItem('lms_refresh_token', refresh)
```

`localStorage` đọc được bằng JavaScript cùng origin. Một lỗ XSS — hoặc thực tế hơn nhiều, **một
gói npm trong chuỗi phụ thuộc bị chiếm** — lấy được `lms_refresh_token` và mạo danh người dùng
**30 ngày**. Xoay vòng refresh token (đã có, và có phát hiện tái sử dụng) **không cứu được**: kẻ
tấn công cầm token hợp lệ thì cũng xoay vòng bình thường như người dùng thật.

CSP ở `deploy/nginx/langcenter.conf` (`default-src 'self'`, script không có `'unsafe-inline'`)
giảm nhẹ đáng kể, nhưng CSP là hàng rào chống **chạy** script lạ, không phải hàng rào chống
**trộm** token khi script lạ đã chạy được.

## Quyết định

**Refresh token chuyển sang cookie `httpOnly`; access token chuyển vào bộ nhớ (RAM) của tab;
thêm chống CSRF bằng double-submit token.**

| | Trước | Sau |
|---|---|---|
| Access token (60 phút) | `localStorage` | **Biến trong bộ nhớ** của tab |
| Refresh token (30 ngày) | `localStorage` | **Cookie** `httpOnly; Secure; SameSite=Strict; Path=/api/v1/auth` |
| Chống CSRF | không có (không cần, vì không dùng cookie) | **double-submit token** |

### Vì sao cookie `httpOnly` giải quyết được vấn đề

JavaScript **không đọc được** cookie `httpOnly`. XSS vẫn hại được trong lúc phiên đang mở (nó
chạy trong trang, gọi API được), nhưng nó **không mang phiên đi nơi khác được** — mất quyền truy
cập ngay khi tab đóng, thay vì cầm 30 ngày và dùng từ máy khác.

Đó là khác biệt giữa "một sự cố" và "một vụ chiếm tài khoản kéo dài".

### Vì sao access token cũng phải vào RAM

Để access token ở `localStorage` thì XSS vẫn lấy được **60 phút** sử dụng. Có thể chấp nhận,
nhưng khi đã sửa tới đây thì chi phí thêm là nhỏ, còn lợi ích là **không còn gì của phiên nằm
trong `localStorage`**.

Cái giá: **tải lại trang là mất access token**, nên app phải gọi `POST /auth/lam-moi-token` lúc
khởi động (cookie tự gửi kèm, không cần JS đọc gì). Tốn một request mỗi lần mở tab.

### `Path=/api/v1/auth` — hẹp có chủ ý

Cookie chỉ được trình duyệt gửi kèm tới đúng nhóm endpoint xác thực. Mọi request nghiệp vụ khác
(`/api/v1/lop-hoc`, `/api/v1/khach-hang`…) **không** mang refresh token theo, nên bề mặt rò rỉ
qua log proxy, qua header forwarding, qua lỗi lập trình đều hẹp lại.

### Vì sao `SameSite=Strict` dùng được ở đây

Frontend và API **cùng origin**: `deploy/nginx/langcenter.conf` phục vụ build của Vite ở
`location /` và proxy API ở `location /api/`. Không có request chéo origin hợp lệ nào cần cookie
này, nên `Strict` không phá thứ gì — khác với kiến trúc tách domain (`app.x.com` gọi `api.x.com`)
buộc phải hạ xuống `SameSite=None` và mất gần hết lợi ích.

## Ba cái giá — nói trước, không phát hiện sau

### 1. Triển khai sẽ ĐÁ MỌI NGƯỜI ĐANG ĐĂNG NHẬP ra

Refresh token cũ nằm trong `localStorage`; bản mới không đọc nữa. Không có đường lui êm.

**Đã cân nhắc** một bản trung gian đọc cả hai nguồn rồi mới bỏ `localStorage` ở bản sau. **Quyết
định KHÔNG làm**: bản trung gian đó giữ nguyên đúng lỗ hổng đang vá (token vẫn nằm trong
`localStorage`, XSS vẫn lấy được) thêm một khoảng thời gian, đổi lấy việc người dùng khỏi đăng
nhập lại một lần. Không đáng.

Hệ quả vận hành: **chọn giờ thấp điểm để triển khai**, và báo trước cho trung tâm.

### 2. Phải chống CSRF — phần việc mới, không có sẵn

Cookie được trình duyệt **tự gửi kèm**, kể cả với request do trang khác kích hoạt. Đây là thứ
`localStorage` vốn miễn nhiễm (JS phải tự gắn header), nên đổi sang cookie là **nhận thêm một
loại rủi ro mới** — phải xử lý, không được bỏ qua.

`SameSite=Strict` chặn gần hết, nhưng vẫn thêm **double-submit token**: server phát một token
CSRF trong cookie **đọc được** (không `httpOnly`), client đọc rồi gửi lại trong header; server so
hai giá trị. Trang khác không đọc được cookie của origin này nên không dựng được header khớp.

Chỉ áp cho nhóm endpoint nhận cookie refresh (`/api/v1/auth/lam-moi-token`, `/auth/dang-xuat`) —
các endpoint khác dùng `Authorization: Bearer` nên không có bề mặt CSRF.

### 3. `Secure` không chạy trên `http://localhost`

Cookie `Secure` chỉ đi qua HTTPS. Dev chạy `http://localhost:5173` nên cần cờ tắt `Secure`.

Cờ đó là **rủi ro tự tạo**: bật nhầm ở production thì refresh token đi qua HTTP thuần. Nên nó
phải suy từ môi trường (`IHostEnvironment.IsDevelopment()`), **không** phải một biến cấu hình
người vận hành đặt tay được — và có test chốt rằng ở môi trường Production thì cookie luôn
`Secure`.

## Phạm vi ảnh hưởng — đã ĐO, không ước lượng

| Nơi | Số chỗ | Ghi chú |
|---|---|---|
| `frontend/src/lib/api.ts` | 4 | nơi lưu/đọc token, interceptor làm mới |
| `frontend/src/lib/auth.tsx` | 2 | `dangNhap`, khôi phục phiên lúc khởi động |
| `AuthController` | 3 | `dang-nhap`, `lam-moi-token`, `dang-xuat` — đặt/xoá cookie |
| Test tích hợp | 7 tệp | đọc `refreshToken` từ body |
| **Test E2E** | **21 tệp** | **đọc `localStorage.getItem('lms_access_token')`** |

21 tệp E2E là con số đáng chú ý nhất. Chúng lấy access token ra để **dựng dữ liệu qua API** cho
nhanh, ví dụ `modal-long-nhau.spec.ts`:

```ts
const tok = await page.evaluate(() => localStorage.getItem('lms_access_token'))
const H = { Authorization: `Bearer ${tok}`, ... }
```

Access token vào RAM thì dòng đó trả `null` và **21 test đỏ cùng lúc**.

**Cách xử lý:** thêm helper `layTokenQuaApi(request, trungTam)` trong `e2e/tro-giup.ts` — gọi
thẳng `POST /auth/dang-nhap` bằng `APIRequestContext` để lấy token, **không** đọc ra từ trang.
Cách này đúng hơn cả về mặt test: nó không phụ thuộc vào **chi tiết cài đặt** của việc app lưu
token ở đâu.

## Phương án đã cân nhắc và loại

| Phương án | Vì sao loại |
|---|---|
| **Giữ nguyên `localStorage`** | Lỗ hổng còn nguyên: XSS ⇒ 30 ngày. CSP giảm nhẹ nhưng không chặn trộm token. |
| **Chỉ chuyển refresh, giữ access ở `localStorage`** | Bịt được phần nguy hiểm nhất (30 ngày → 60 phút) với ít việc hơn hẳn. Đã trình bày cho chủ sản phẩm; **chọn phương án đầy đủ**. Giữ đây làm đường lui nếu phần access-token-vào-RAM phát sinh vấn đề. |
| **Rút hạn refresh token xuống vài giờ** | Giảm thiệt hại chứ không chặn, mà lại bỏ mất tiện ích "30 ngày không phải đăng nhập lại" — đổi nhiều lấy ít. |
| **Mã hoá token trước khi lưu `localStorage`** | Vô nghĩa: khoá giải mã cũng phải nằm trong JS, XSS lấy được cả hai. |
| **Web Worker giữ token** | Phức tạp hơn hẳn cookie `httpOnly` mà vẫn yếu hơn — XSS nhắn tin được cho worker để xin token. |

## Hai lỗi THẬT mà việc này lôi ra — không phải lỗi của chính nó

Cả hai đã tồn tại từ trước, nhưng `localStorage` che đi. Ghi lại vì chúng là lý do đáng giá
nhất của đợt thay đổi này.

### 1. Đổi mật khẩu tự giết phiên của CHÍNH MÌNH

`DoiMatKhauCommand` thu hồi **mọi** refresh token của tài khoản — đúng, vì đổi mật khẩu thường
là phản ứng khi nghi bị lộ. Nhưng "mọi" bao gồm cả refresh token của **người đang đổi**.

Trước ADR-0007 không ai thấy: access token nằm trong `localStorage` nên người dùng vẫn ở nguyên
trong app cho tới khi nó hết hạn sau 60 phút, và lúc ấy họ đã làm việc khác từ lâu. Sau
ADR-0007, access token nằm trong RAM ⇒ **tải lại trang ngay sau khi đổi mật khẩu là văng về màn
đăng nhập**.

Sửa: lệnh trả về một cặp token mới cho chính phiên đó (các phiên khác vẫn chết như thiết kế).

### 2. `React.StrictMode` gọi `useEffect` hai lần ⇒ tự kích hoạt chống trộm token

Refresh token **xoay vòng**, và dùng lại token đã thu hồi bị backend hiểu là **bị đánh cắp** ⇒
thu hồi TOÀN BỘ phiên. Ở dev, `StrictMode` gọi effect khôi phục phiên hai lần: lần thứ hai cầm
token vừa bị lần thứ nhất thu hồi ⇒ người dùng tự đá mình ra mỗi lần F5.

Log của API nói đúng chuyện đang xảy ra: *"Phát hiện tái sử dụng refresh token đã thu hồi —
thu hồi toàn bộ phiên"*.

Sửa: đường khôi phục dùng chung hàng đợi `dangLamMoi` vốn đã có sẵn ở interceptor cho đúng tình
huống này. Đây không phải lỗi riêng của dev: hai tab mở cùng lúc, hay một lần thử lại, cũng cho
cùng kết quả.

### 3. Phiên bị đẩy ra kéo theo cả phiên của người vừa đăng nhập

Cái này **nặng nhất** và mất nhiều công nhất để tìm.

Backend coi "dùng lại refresh token đã thu hồi" là dấu hiệu **bị đánh cắp** ⇒ thu hồi TOÀN BỘ
phiên của tài khoản. Đúng — nhưng `RefreshToken` không lưu **lý do** thu hồi, nên nó không phân
biệt được hai ca hoàn toàn khác nhau:

| Ca | Ý nghĩa | Phải làm gì |
|---|---|---|
| Token bị **xoay vòng** rồi có người dùng lại | Nghi bị đánh cắp | Thu hồi toàn bộ |
| Token bị **lần đăng nhập mới đẩy ra** | Bình thường | **Không** thu hồi gì thêm |

Trước ADR-0007 không sao: phiên cũ nhận 401 ở endpoint nghiệp vụ và frontend **không** gọi làm
mới (chặn từ 20/09). Nay refresh token đi bằng cookie nên phiên cũ chạm `lam-moi-token` trước,
và hệ quả là **A bị đẩy ra → A gọi làm mới → B vừa đăng nhập cũng bị đá ra**. Hai người cùng
văng, không ai vào được.

**Đã thử suy từ `PhienHienTai` và BỎ**: sau một lần xoay vòng hợp lệ thì cột đó cũng khác `jti`
của token cũ, nên suy đoán sẽ coi **ca trộm thật** là "bị đẩy ra" — tức nới lỏng đúng chốt chặn
quan trọng nhất. Test `Tai_su_dung_token_da_thu_hoi_thi_thu_hoi_toan_bo_phien` đỏ ngay và chặn
tôi lại.

**Cách chữa đúng**: thêm cột `REFRESH_TOKEN.ly_do` (migration `LyDoThuHoiRefreshToken` — chỉ
THÊM một cột nullable, không đổi/xoá gì; đã sao lưu và đếm 1329 hàng trước/sau đều khớp). Ghi
lý do lúc thu hồi, đọc lý do lúc kiểm.

Canh hai chiều: mutation "luôn thu hồi toàn bộ" và "không bao giờ thu hồi" đều chết.

### Và một điều chỉnh về `SameSite`

ADR ban đầu chọn `SameSite=Strict`. **Sai**: `Strict` chặn cookie ở request phát sinh từ **điều
hướng tài liệu** — gõ URL, F5, mở link trực tiếp — tức đúng những thao tác cần cookie nhất.
Đổi sang `Lax`, vẫn chặn POST chéo origin (ca CSRF nguy hiểm), phần còn lại do double-submit
token lo.

## Hệ quả

**Tích cực**
- XSS/phụ thuộc npm bị chiếm **không mang được phiên đi nơi khác**; hết hiệu lực khi tab đóng.
- Không còn gì của phiên nằm trong `localStorage`.
- `Path` hẹp ⇒ refresh token không đi kèm mọi request nghiệp vụ.

**Tiêu cực**
- Mọi người đang đăng nhập bị đá ra **một lần** lúc triển khai.
- Thêm một request lúc mở tab (làm mới token để lấy access token vào RAM).
- Nhận thêm loại rủi ro CSRF, phải duy trì double-submit token.
- Dev cần nhánh tắt `Secure`, và nhánh đó phải có test canh.

**Trung tính**
- Cơ chế một-phiên (`PhienHienTai`), xoay vòng và phát hiện tái sử dụng refresh token **không
  đổi** — chúng làm việc ở tầng dữ liệu, không quan tâm token được chuyên chở bằng gì.
