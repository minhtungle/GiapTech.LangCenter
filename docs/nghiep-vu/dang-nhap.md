# Module Đăng nhập (FR-01, FR-02)

> ℹ️ FR này **vẫn còn trong code**, nhưng từ ngữ đã đổi (05/09/2026): "đội"/"CLB" →
> "trung tâm", `MaDoi` → `MaTrungTam`, route `/dang-ky-clb` → `/dang-ky-trung-tam`. Phần hồ sơ
> cầu thủ (FR-04) đã bỏ.

## FR-01 — Đăng nhập

Xác thực bằng **bộ ba**: `{mã đội, tên đăng nhập, mật khẩu}`. Username chỉ duy nhất **trong
phạm vi một tenant** — hai CLB khác nhau có thể cùng có tài khoản `admin`.

### Mã đội

**7 ký tự, do hệ thống sinh tự động** — người dùng không tự đặt.

| Quyết định | Lý do |
|---|---|
| Sinh tự động, không cho tự đặt | Tên dạng "FC ..." rất dễ trùng giữa các CLB phong trào, mà mã phải duy nhất toàn hệ thống |
| Bộ 31 ký tự: `23456789ABCDEFGHJKMNPQRSTUVWXYZ` | Bỏ `0/O` và `1/I/L` — người dùng phải đọc mã qua điện thoại và chép tay, nhầm 0 với O là lỗi hay gặp. Vẫn còn 31⁷ ≈ 27 tỷ tổ hợp |
| Không phân biệt hoa/thường | Lưu dạng hoa, chuẩn hoá khi so sánh. Gõ `a3k9m2p` hay `A3K9M2P` đều vào được |
| Không cho sửa (FR-06) | Người dùng gõ mã mỗi lần đăng nhập; đổi mã sẽ khoá cả CLB ra ngoài |

Màn đăng ký hiển thị mã to kèm nút sao chép và cảnh báo ghi lại — mã sinh tự động mà người dùng
không lưu thì họ mất đường vào, và không có cách tự tra lại.

Cài đặt: `Domain/Common/MaDoi.cs`, kiểm chứng bởi `MaDoiTests`.

### Tra tên đội ngay trên trang đăng nhập (20/08/2026)

Gõ đủ 7 ký tự thì trang hiện luôn tên CLB tương ứng; sai thì hiện "Không tìm thấy đội tương ứng".
Trước đó gõ sai mã chỉ biết sau khi điền hết form và nhận "sai thông tin đăng nhập" — không phân
biệt được sai mã hay sai mật khẩu.

`GET /auth/ten-doi/{maDoi:length(7)}` · `[AllowAnonymous]` · chỉ trả `{ "tenDoi": "..." }`.

**Không có đường tìm theo tên.** Yêu cầu ban đầu của chủ sản phẩm có cả phần đó (hiện các đội gần
giống để chọn), nhưng endpoint này buộc phải ẩn danh — người dùng đang **ở** trang đăng nhập nên
chưa thể có token. Cho tìm theo tên nghĩa là ai cũng liệt kê được toàn bộ CLB kèm mã đội. Chủ sản
phẩm chốt: *"thôi, giờ chỉ tìm theo mã đội chính xác."*

Ba trạng thái trên giao diện, trạng thái đầu là chỗ dễ sai nhất:

| Ô mã | Hiện gì |
|---|---|
| Chưa đủ 7 ký tự | Câu gợi ý "7 ký tự, không phân biệt hoa thường" — **không** được hiện "không tìm thấy" khi người dùng còn đang gõ |
| Đủ 7, có CLB | `✓ <tên đội>` |
| Đủ 7, không có CLB | "Không tìm thấy đội tương ứng" |

**Điểm căng có ý thức với quy tắc ở mục Luồng bên dưới** ("không tiết lộ tenant nào tồn tại"):
endpoint này *có* tiết lộ một mã đội là có thật. Đánh đổi được chấp nhận vì mã đội **không phải bí
mật** — nó được in ra cho cả CLB dùng để đăng nhập, dán trên nhóm chat, đọc qua điện thoại. Điều
phải giữ bí mật là *username + mật khẩu*, và endpoint này không nói gì về chúng. Quy tắc kia vẫn
áp nguyên cho chính lệnh đăng nhập: sai mã, sai username, sai mật khẩu đều trả **cùng một mã lỗi**.

Canh bởi `TraTenDoiTests` (12 test) và `e2e/dang-nhap-tra-ma.spec.ts` (3 test). Nợ **N3 rate limit
ở Caddy** thành chặn cứng trước khi mở ra Internet — xem
[năm endpoint ngoài tenant](../backend/multi-tenant.md#năm-endpoint-đọcghi-ngoài-tenant-xếp-theo-mức-rộng).

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

## FR-02 — Quên mật khẩu

1. Người dùng nhập email đã đăng ký (kèm ID đội để xác định tenant).
2. Hệ thống gửi email chứa **link/mã đặt lại mật khẩu có thời hạn**.
3. Người dùng đặt lại mật khẩu qua link; token hết hạn hoặc đã dùng thì không chấp nhận.

### Quy tắc

- Phản hồi cho người dùng **luôn giống nhau** dù email có tồn tại hay không — tránh dò email hợp lệ.
- Token đặt lại dùng một lần, có thời hạn ngắn.
- Kênh gửi: SMTP (xem [biến môi trường](../ha-tang/bien-moi-truong.md)).

## Phiên đăng nhập & refresh token

| Cơ chế | Quyết định | Vì sao |
|---|---|---|
| Lưu trữ | DB giữ **hash SHA-256**, không giữ token thô | Người đọc được DB (backup rò rỉ, SQL injection) không mạo danh được ai |
| Xoay vòng | Token cũ **thu hồi ngay** khi cấp token mới | Bản sao bị lộ chỉ dùng được tới lần làm mới kế tiếp, không sống tới ngày hết hạn |
| Phát hiện đánh cắp | Dùng lại token **đã thu hồi** → thu hồi **toàn bộ** phiên | Tái sử dụng là dấu hiệu có bản sao trong tay người khác; thà buộc đăng nhập lại còn hơn để phiên bị chiếm chạy tiếp |
| Đổi mật khẩu | Thu hồi mọi phiên đang mở | Đổi mật khẩu thường là phản ứng khi nghi bị lộ |
| Hạn | Access 60 phút · Refresh 30 ngày | |

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

- Bảng `NGUOI_DUNG`, `TENANT`, `REFRESH_TOKEN`, `TOKEN_DATLAI_MATKHAU` — xem [ERD](../database/erd.md).
- Cấu hình JWT & Identity — xem [SECURITY.md](../../SECURITY.md).
