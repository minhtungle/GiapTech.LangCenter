# 22/09/2026 — Mục 7: refresh token vào cookie `httpOnly` (ADR-0007)

Chủ sản phẩm chọn **phương án đầy đủ**: cookie `httpOnly` + access token vào RAM + chống CSRF.
Viết [ADR-0007](../02-kien-truc/adr/0007-refresh-token-cookie-httponly.md) trước, code sau (quy tắc #7).

Hoàn tất đợt rà soát: **8/8 mục đã vá**.

---

## Phần cài đặt: đúng như dự đoán trong ADR

Bề mặt nhỏ như đã đo: 4 chỗ frontend, 3 endpoint backend. Phần tốn công nhất cũng đúng như ADR
cảnh báo — **21 tệp E2E** đọc token từ `localStorage`.

Điều ADR **không** lường được là ba lỗi bên dưới. Chúng có sẵn từ trước, `localStorage` che đi,
và chỉ lộ ra khi access token chuyển vào RAM.

---

## Lỗi 1 — đổi mật khẩu tự giết phiên của chính mình

`DoiMatKhauCommand` thu hồi **mọi** refresh token. Đúng thiết kế (đổi mật khẩu là phản ứng khi
nghi bị lộ), nhưng "mọi" gồm cả token của **người đang đổi**.

Trước đây vô hại: access token nằm trong `localStorage`, người dùng vẫn ở trong app tới 60 phút
sau. Nay F5 ngay sau khi đổi mật khẩu là **văng về màn đăng nhập**.

Sửa: lệnh trả về cặp token mới cho chính phiên đó. Các phiên khác vẫn chết như thiết kế.

---

## Lỗi 2 — `React.StrictMode` tự kích hoạt cơ chế chống trộm

Refresh token xoay vòng, và dùng lại token đã thu hồi bị backend hiểu là **bị đánh cắp** ⇒ thu
hồi toàn bộ phiên. `StrictMode` gọi `useEffect` hai lần ở dev ⇒ lần hai cầm token vừa bị lần
một xoay vòng ⇒ người dùng tự đá mình ra mỗi lần F5.

Log API nói thẳng điều đang xảy ra: *"Phát hiện tái sử dụng refresh token đã thu hồi — thu hồi
toàn bộ phiên"*. **Đọc log nhanh hơn đoán** — tôi đã đoán sai vài vòng trước khi đọc nó.

Không riêng gì dev: **hai tab mở cùng lúc** cho cùng kết quả. Sửa: đường khôi phục dùng chung
hàng đợi `dangLamMoi` vốn đã có sẵn ở interceptor cho đúng tình huống này.

---

## Lỗi 3 — phiên bị đẩy ra kéo theo phiên của người vừa đăng nhập

Nặng nhất, và mất nhiều công nhất.

A bị đẩy ra → A gọi làm mới → backend tưởng bị trộm → thu hồi toàn bộ → **B vừa đăng nhập cũng
bị đá ra**. Hai người cùng văng, không ai vào được.

Gốc rễ: `REFRESH_TOKEN` không lưu **lý do** thu hồi, nên hai ca đòi xử lý ngược nhau lại trông
giống hệt:

| Ca | Ý nghĩa | Phải làm |
|---|---|---|
| Bị **xoay vòng** rồi có người dùng lại | Nghi bị đánh cắp | Thu hồi toàn bộ |
| Bị **lần đăng nhập mới đẩy ra** | Bình thường | Không thu hồi thêm |

### Cách rẻ mà SAI

Tôi thử suy từ `PhienHienTai` để khỏi phải migration. **Sai**: sau một lần xoay vòng hợp lệ thì
cột đó cũng khác `jti` của token cũ, nên suy đoán coi **ca trộm thật** là "bị đẩy ra" — tức nới
lỏng đúng chốt chặn quan trọng nhất của hệ thống.

Test `Tai_su_dung_token_da_thu_hoi_thi_thu_hoi_toan_bo_phien` đỏ ngay. Đây là lần thứ hai trong
ngày bộ test chặn tôi lại đúng lúc.

### Cách đúng, và cách làm an toàn

Thêm cột `REFRESH_TOKEN.ly_do`. Migration cần hỏi chủ sản phẩm trước (quy tắc #1) — hỏi, được
duyệt, rồi làm theo trình tự:

1. **Sao lưu** DB (28 MB)
2. **Đọc migration** trước khi chạy — xác nhận chỉ `ADD COLUMN ly_do integer NULL`, không đổi,
   không xoá
3. **Đếm hàng trước/sau**: 1329 / 1329

Canh hai chiều: mutation *"luôn thu hồi toàn bộ"* (quay lại lỗi) và *"không bao giờ thu hồi"*
(nới lỏng chống trộm) **đều chết**.

---

## Một chỗ ADR chốt sai và phải sửa

ADR ban đầu chọn `SameSite=Strict`, lập luận "frontend và API cùng origin nên Strict không phá
gì". Sai: `Strict` chặn cookie ở request phát sinh từ **điều hướng tài liệu** — gõ URL, F5, mở
link trực tiếp — tức đúng những thao tác cần cookie nhất.

Đổi sang `Lax`: vẫn chặn POST chéo origin (ca CSRF nguy hiểm), phần còn lại do double-submit
token lo.

---

## Cách tìm ra: đọc thứ hệ thống NÓI RA, đừng đoán

Tôi đã đoán sai vài vòng — đổ cho `withCredentials`, cho `SameSite`, cho `Path` cookie — và mỗi
lần "sửa" xong thì test vẫn đỏ y nguyên. Chỉ khi dùng CDP đọc `blockedReasons` và đọc log API
mới ra nguyên nhân thật.

Ghi lại vì nó lặp lại được: với lỗi kiểu này, **đọc thứ hệ thống tự nói** (log server, CDP,
response body) nhanh hơn hẳn thử từng giả thuyết.

---

## Bài học cho test E2E

Sau ADR-0007, **mỗi `page.goto`/`page.reload` làm token cũ thành 401** (app đổi cookie lấy
token mới, refresh token xoay vòng, `PhienHienTai` đổi theo).

Đây là lỗi im lặng: request đầu còn chạy, request sau 401, và test đỏ ở chỗ trông như lỗi
nghiệp vụ — *"tệp thứ 2 phải vào được"*, *"hàng chờ phải rỗng"*. Mất nhiều vòng mới lần ra.

Hai quy tắc, ghi ở `tro-giup.ts`:

- **Đừng bắt token vào biến rồi dùng lại sau khi điều hướng** — cho closure `api()` gọi
  `layTokenQuaApi(page)` mỗi lần.
- **Đừng cho helper tự `POST /auth/dang-nhap`** để lấy token riêng: đăng nhập lần hai **đẩy
  phiên của trình duyệt ra** và chính trang đang test bị đá về màn đăng nhập. Tôi làm đúng cái
  này lúc đầu — 17 test đỏ.

---

## Kết quả

**630 test backend** · **40 frontend** · **51 E2E**.

Verify trên PostgreSQL thật: body đăng nhập chỉ còn `accessToken, hetHan, phaiDoiMatKhau,
tokenCsrf`; cookie `lms_rt` có `httponly`, `lms_csrf` cố ý không; xoay vòng và phát hiện tái sử
dụng vẫn đúng qua đường cookie.
