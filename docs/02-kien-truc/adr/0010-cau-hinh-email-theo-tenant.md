# ADR-0010: Cấu hình email theo tenant, mật khẩu SMTP mã hoá trong DB

- **Ngày:** 30/09/2026
- **Trạng thái:** Đã chốt (chủ sản phẩm chọn "mỗi tenant một hộp thư")
- **Bối cảnh liên quan:** [ADR-0004](./0004-ha-tang-tu-host-vps.md) (biến môi trường trên VPS),
  [multi-tenant.md](../../03-backend/multi-tenant.md), FR-31

## Bối cảnh

Tới 30/09/2026, email gửi qua `SmtpEmailSender` đọc cấu hình từ **biến môi trường**
(`SMTP_HOST`, `SMTP_USER`, `SMTP_PASSWORD`…). Nghĩa là **một hộp thư dùng chung cho toàn VPS**:
mọi trung tâm gửi email từ cùng một địa chỉ.

Chủ sản phẩm muốn mỗi trung tâm gửi từ **hộp thư của chính họ**. Hai lý do thật:

1. **Nhận diện.** Học viên nhận email từ `no-reply@abc-english.edu.vn` chứ không phải một tên
   lạ. Với một sản phẩm bán cho nhiều trung tâm, đây là khác biệt nhìn thấy được.
2. **Tỉ lệ vào hộp thư.** Gửi thay mặt một domain mà không có SPF/DKIM của domain đó thì email
   rơi vào spam. Trung tâm dùng SMTP của chính họ thì các bản ghi ấy đã đúng sẵn.

## Điều làm quyết định này khó

**Đây là bí mật ĐẦU TIÊN hệ thống lưu trong DB ở dạng giải mã ngược được.**

Mọi bí mật tới nay đều thuộc một trong hai loại:

| Loại | Ví dụ | Vì sao an toàn |
|---|---|---|
| Biến môi trường | `JWT_SECRET`, `MINIO_SECRET_KEY` | Không nằm trong DB, không nằm trong backup DB |
| Băm một chiều | `TAI_KHOAN.password_hash`, `REFRESH_TOKEN.token_hash` | Lộ ra cũng không dùng ngược được |

Mật khẩu SMTP **không thuộc loại nào**: phải khôi phục được nguyên văn để đăng nhập máy chủ
thư. Nên nó buộc phải là **mã hoá đối xứng**, và điều đó kéo theo một chuỗi hệ quả.

## Quyết định

**Cấu hình SMTP lưu trên `TENANT`; mật khẩu mã hoá bằng AES-GCM, khoá đặt ở biến môi trường.**

### Schema

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `smtp_host` | `varchar(200)` NULL | `null` = dùng cấu hình chung của VPS |
| `smtp_port` | `int` NULL | Mặc định 587 |
| `smtp_user` | `varchar(200)` NULL | Thường là email hoặc `apikey` |
| `smtp_mat_khau_ma_hoa` | `varchar(1000)` NULL | **Đã mã hoá** — tên cột nói rõ điều đó |
| `smtp_nguoi_gui` | `varchar(200)` NULL | Địa chỉ hiện ở ô "From" |
| `smtp_ten_nguoi_gui` | `varchar(200)` NULL | Tên hiện cạnh địa chỉ |

Tên cột là `smtp_mat_khau_ma_hoa`, **không** phải `smtp_mat_khau`. Người đọc schema phải thấy
ngay đây là dữ liệu đã mã hoá — đặt tên trung tính là mời người sau ghi thẳng mật khẩu thô vào.

### Mã hoá: AES-GCM, khoá từ biến môi trường

```
EMAIL_KHOA_MA_HOA   — khoá 32 byte, base64. KHÔNG nằm trong DB.
```

**AES-GCM chứ không phải AES-CBC:** GCM có kiểm tính toàn vẹn sẵn (AEAD), nên sửa đổi bản mã
bị phát hiện. CBC không có, và ghép CBC với HMAC đúng cách là việc dễ sai.

**Khoá ở biến môi trường, không trong DB:** nếu khoá nằm cùng chỗ với dữ liệu nó bảo vệ thì
mã hoá chỉ là thủ tục. Backup DB rò ra ngoài mà không kèm khoá thì mật khẩu vẫn an toàn.

**Thiếu khoá ⇒ từ chối lưu, không âm thầm lưu thô.** Lỗi rõ ràng lúc cấu hình tốt hơn một cơ
sở dữ liệu đầy mật khẩu trần mà không ai biết.

### API không bao giờ trả mật khẩu ra

Kể cả cho quản trị viên của chính trung tâm đó. DTO trả `CoMatKhau: bool` thay vì giá trị.

Lý do: không có lý do chính đáng nào để đọc lại mật khẩu SMTP qua giao diện. Người cấu hình
đã có nó trong tay; người khác đọc được là rò rỉ. Muốn đổi thì nhập lại — bất tiện đúng một
lần, an toàn mãi mãi.

Ô mật khẩu để trống khi cập nhật = **giữ nguyên giá trị cũ**, không phải xoá. Đây là ngoại lệ
có chủ ý với quy tắc #1 (lệnh cập nhật ghi đè trường nào thì trường đó phải có trong form):
trường này không hiển thị được nên không thể yêu cầu form gửi lại nó.

## Tenant không cấu hình thì sao

Rơi về biến môi trường của VPS — đúng hành vi hiện tại. Không bắt mọi trung tâm phải có SMTP
riêng mới gửi được email.

Thứ tự:

```
tenant.smtp_host có giá trị?
  có    → dùng SMTP của tenant
  không → dùng SMTP_HOST của VPS
            không có nữa → ghi log cảnh báo, BỎ QUA (hành vi hiện tại)
```

Nhánh cuối giữ nguyên vì nó có lý do tốt: môi trường dev không có SMTP, mà luồng quên mật khẩu
vẫn phải chạy đầu-cuối được.

## Ràng buộc đã phát hiện: quên mật khẩu chạy khi CHƯA đăng nhập

`QuenMatKhauCommand` gửi email trước khi có phiên nào — `ICurrentTenant` rỗng, handler tự tra
tenant bằng `IgnoreQueryFilters()`.

Nên `IEmailSender` **phải nhận tenant tường minh**, không đoán từ ngữ cảnh:

```csharp
Task GuiAsync(Guid tenantId, string den, string tieuDe, string noiDungHtml, CancellationToken ct);
```

Đọc từ `ICurrentTenant` sẽ làm luồng quên mật khẩu gửi bằng SMTP chung trong khi trung tâm đã
cấu hình riêng — và hỏng **im lặng**, vì không có gì báo. Tham số tường minh cũng là thứ job
nền sau này cần: chúng chạy ngoài HTTP nên không có `ICurrentTenant` nào.

## HTML email khác HTML web — cần một bước chuyển đổi

Outlook desktop render bằng engine của Microsoft Word: **không flexbox, không grid, không
`border-radius`**, và CSS trong `<style>` phần lớn bị bỏ qua.

Nên nội dung soạn bằng trình soạn thảo web **không gửi thẳng được**. Kiến trúc ba tầng:

```
Tiptap soạn (HTML ngữ nghĩa)  →  bọc khung <table>  →  inline CSS  →  gửi
   lưu trong DB                    backend, lúc gửi     PreMailer.Net
```

**Inline CSS chạy lúc GỬI, không lúc SOẠN.** Mẫu lưu trong DB giữ dạng ngữ nghĩa, nên đổi khung
template sau này không phải sửa lại dữ liệu cũ.

Thư viện: **Tiptap** (MIT) ở frontend, **PreMailer.Net 2.7.4** (MIT, phát hành 28/08/2026) ở
backend. Cả hai miễn phí và không phụ thuộc dịch vụ ngoài — CKEditor bị loại vì GPL không hợp
sản phẩm đóng, và bản thương mại tính phí theo số lần mở editor nên chi phí trôi theo số tenant.

## Hệ quả

**Tích cực**
- Trung tâm gửi email mang thương hiệu của họ, tỉ lệ vào hộp thư cao hơn.
- Trung tâm chưa cấu hình vẫn dùng được — không phá luồng hiện có.
- `IEmailSender` nhận tenant tường minh ⇒ job nền sau này dùng được ngay.

**Tiêu cực**
- **Thêm một bí mật phải quản.** Mất `EMAIL_KHOA_MA_HOA` là mọi cấu hình SMTP thành rác —
  không giải mã lại được, phải nhập lại từng trung tâm. Khoá này phải vào quy trình sao lưu.
- **Đổi chữ ký `IEmailSender`** ⇒ mọi nơi gọi phải sửa. Hiện chỉ một chỗ, nên rẻ — nhưng làm
  sau sẽ đắt hơn.
- Trung tâm nhập sai cấu hình thì email của họ không đi, mà lỗi chỉ hiện trong log. Cần nút
  **gửi thử** ngay trên màn cấu hình.

## Test bắt buộc

| Test | Canh gì |
|---|---|
| Mật khẩu lưu xuống DB **không phải bản rõ** | Chốt chính của ADR này |
| API không bao giờ trả mật khẩu | Kể cả cho admin của tenant đó |
| Để trống ô mật khẩu ⇒ giữ nguyên giá trị cũ | Quy tắc #1 — không âm thầm xoá |
| Tenant chưa cấu hình ⇒ rơi về SMTP của VPS | Không phá luồng hiện có |
| Thiếu `EMAIL_KHOA_MA_HOA` ⇒ từ chối lưu | Không âm thầm lưu thô |
| Mã hoá rồi giải mã ra đúng chuỗi ban đầu | Vòng tròn đóng |
| Hai tenant không đọc được cấu hình của nhau | Cách ly tenant |
