# 22/09/2026 — "Nhớ đăng nhập" và rà soát bảo mật luồng đăng nhập

Yêu cầu chủ sản phẩm: *"thêm nút nhớ mật khẩu / kiểm tra kỹ có lỗ hổng nào về bảo mật khi đăng
nhập không, rà soát thật kỹ dựa theo các kỹ thuật tham khảo được trên mạng"*.

## 1. "Nhớ mật khẩu" — hỏi lại trước khi làm

Đọc theo nghĩa đen thì đây là lưu mật khẩu vào `localStorage`. Nhưng phiên hiện tại **đã sống
30 ngày** và `localStorage` giữ token qua cả lần đóng trình duyệt — tức thứ người dùng tưởng
mình đang thiếu thì thực ra đã có. Nên "nhớ mật khẩu" ở đây có thể mang ba nghĩa rất khác nhau,
và ba nghĩa đó dẫn tới ba công việc khác nhau.

Hỏi lại, kèm đánh đổi của từng phương án. Chủ sản phẩm chọn: **nhớ mã trung tâm + tên đăng nhập,
không đụng tới mật khẩu.**

Đó cũng là phương án giải đúng phiền toái thật: mật khẩu do người dùng tự đặt nên còn nhớ được,
còn **mã 7 ký tự vô nghĩa** (`W686AE9`) thì không ai thuộc.

### Vì sao không lưu mật khẩu

- `localStorage` đọc được bằng JavaScript cùng origin. XSS lấy được token thì token còn **thu
  hồi được** (đổi mật khẩu thu hồi mọi refresh token); mật khẩu thô thì mở được cả những nơi
  khác mà người dùng dùng lại nó.
- Trình duyệt đã có sẵn chức năng lưu mật khẩu, mã hoá theo tài khoản hệ điều hành. Các ô trên
  form đã khai `autoComplete="username" / "current-password"` đúng chuẩn để nó nhận ra. Tự dựng
  lại bằng `localStorage` là làm **tệ hơn** thứ đã có.

### Hai quyết định nhỏ, đều có lý do

**Mặc định TẮT.** Trung tâm có máy dùng chung ở quầy lễ tân. Điền sẵn tên đăng nhập của người
trước là nói cho người sau biết ai vừa dùng máy.

**Bỏ tích thì quên NGAY**, không đợi lần đăng nhập thành công kế tiếp. Người ở máy dùng chung bỏ
tích rồi đổi ý không đăng nhập nữa — thông tin của họ phải biến mất ngay lúc đó, chứ không nằm
lại chờ một sự kiện có thể không bao giờ tới.

### Test

`nhoDangNhap.test.ts` (7 test) + `nho-dang-nhap.spec.ts` (E2E).

Test đáng nói nhất kiểm **chuỗi thô** trong `localStorage` chứ không kiểm object trả về:

```ts
expect(raw).not.toMatch(/matkhau|password|mat_khau/i)
expect(Object.keys(JSON.parse(raw)).sort()).toEqual(['maTrungTam', 'username'])
```

Lý do: nếu sau này ai đó thấy tên chức năng là "nhớ mật khẩu" rồi "bổ sung cho đủ", object trả
về vẫn có đúng hai khoá và assert ở mức object sẽ không thấy gì.

**Mutation test — 6 đột biến, 6 chết:**

| # | Đột biến | Kết quả |
|---|---|---|
| M1 | Lưu lén mật khẩu vào cùng chỗ | ✝ chết |
| M2 | Bỏ `try/catch` khi đọc | ✝ chết (2 test) |
| M3 | Bỏ kiểm trường rỗng | ✝ chết |
| E1 | Quên gọi `luuDaNho` sau khi đăng nhập | ✝ chết |
| E2 | Nhớ nhưng không điền sẵn vào form | ✝ chết |
| E3 | Điền sẵn cả mật khẩu | ✝ chết |

Không dùng `jsdom` cho test đơn vị — bộ test frontend cố ý chỉ gồm phép tính thuần, chạy ở môi
trường `node`. Thêm `jsdom` để test một tệp đọc/ghi hai khoá chuỗi là đổi môi trường chạy của 33
test còn lại. Dùng `localStorage` giả ~25 dòng, giả được cả hành vi "ném lỗi khi bị chặn".

## 2. Rà soát bảo mật

Kết quả đầy đủ: [`docs/02-kien-truc/ra-soat-bao-mat-dang-nhap.md`](../02-kien-truc/ra-soat-bao-mat-dang-nhap.md).

Tóm tắt: **27 hạng mục đạt**, tập trung ở mật mã học và quản lý token — PBKDF2, refresh token
xoay vòng có phát hiện tái sử dụng, hash token trong DB, `ClockSkew = 0`, fail-fast `JWT_SECRET`.
**Không có lỗ hổng nào cho phép vượt qua xác thực hay đọc dữ liệu trung tâm khác.**

Khoảng trống thật nằm ở hai chỗ khác:

- **Vòng đời phiên** — `POST /auth/dang-xuat` **không tồn tại**. Bấm "Đăng xuất" chỉ xoá
  `localStorage`; access token vẫn sống tới 60 phút, refresh token tới 30 ngày. Chú thích ở
  `TaiKhoan.cs:52` đã viết *"`null` = ... hoặc đã đăng xuất"* — ý định có, cài đặt thì chưa.
- **Chống dò/chống thử** — không khoá tài khoản sau N lần sai (rate limit chỉ theo IP, nên
  botnet phân tán thử vào cùng một tài khoản không bao giờ bị chặn), và kênh thời gian lộ
  username (không tìm thấy tài khoản thì `throw` ngay, **không chạy PBKDF2**).

Cộng thêm: mọi trung tâm mới vẫn có `admin` / `123456` trong DB cho tới khi ai đó đăng nhập lần
đầu.

**Đã tự kiểm chứng lại** 5 phát hiện nặng nhất trên mã nguồn thật trước khi ghi vào tài liệu —
không nhận báo cáo ở mức tin lời.

Tài liệu cũng ghi lại **5 thứ nghi ngờ nhưng kết luận KHÔNG phải lỗ hổng** (thứ tự kiểm
`VoHieuHoa` sau kiểm mật khẩu, `TraTenTrungTamQuery`, so hash trong B-tree index, cache phiên 10
giây, không cấu hình CORS) — để lần rà soát sau không mất công điều tra lại.

**Chưa sửa gì trong số này** — chờ chủ sản phẩm quyết thứ tự ưu tiên. Lưu ý khi làm: mục 1 (đăng
xuất) và mục 8 (fail-open của `PhienDuyNhatMiddleware`) **phải làm cùng nhau**, vì nếu đăng xuất
set `PhienHienTai = null` mà nhánh fail-open còn đó thì middleware sẽ cho qua đúng những token
vừa bị đăng xuất.
