# Email — cấu hình gửi và mẫu nội dung (FR-31)

> Thuộc nhóm **quản trị hệ thống**, dùng chung cho mọi hệ thống con.
>
> Quyết định kiến trúc: [ADR-0010](../02-kien-truc/adr/0010-cau-hinh-email-theo-tenant.md).

## FR-31 — Thiết lập email & mẫu nội dung

### Vấn đề

Hệ thống đã gửi email từ 05/09/2026 (quên mật khẩu), nhưng:

- **Cấu hình SMTP nằm ở biến môi trường** ⇒ mọi trung tâm gửi từ cùng một hộp thư. Học viên
  nhận email từ một tên lạ, và vì không có SPF/DKIM của domain trung tâm nên dễ vào spam.
- **Nội dung email viết cứng trong mã.** Trung tâm muốn đổi một câu chữ phải sửa mã và
  triển khai lại.

### Hai phần

| Phần | Việc |
|---|---|
| **Thiết lập gửi** | Trung tâm nhập SMTP của mình, gửi thử để kiểm |
| **Mẫu nội dung** | Soạn sẵn nội dung cho từng loại email, có biến thay thế |

## Thiết lập gửi

Nằm trong màn **Thiết lập chung**, tab riêng. Các trường: máy chủ, cổng, tên đăng nhập, mật
khẩu, địa chỉ gửi, tên người gửi.

**Mật khẩu mã hoá trước khi lưu, và không bao giờ trả về qua API** — kể cả cho quản trị viên
của chính trung tâm đó. Giao diện hiện "đã có mật khẩu" thay vì giá trị; để trống khi lưu =
giữ nguyên. Xem ADR-0010 để biết vì sao.

**Nút gửi thử** là bắt buộc, không phải tiện ích: nhập sai cấu hình thì email không đi mà lỗi
chỉ nằm trong log server — người dùng không có cách nào biết.

**Chưa cấu hình thì vẫn gửi được**, rơi về SMTP chung của VPS. Không bắt mọi trung tâm phải có
hộp thư riêng mới dùng được hệ thống.

## Mẫu nội dung

### Bốn loại, cố định trong mã

Không cho người dùng tự thêm loại mới: mỗi loại gắn với một chỗ gọi trong mã và một bộ biến
riêng. Thêm loại mà không có chỗ gọi thì đó là mẫu chết.

| Mã | Khi nào gửi | Biến dùng được |
|---|---|---|
| `ChaoMungHocVien` | Tạo người dùng kèm tài khoản, hoặc cấp tài khoản cho người đã có hồ sơ — người tạo **tích chọn** mới gửi | `tenHocVien` · `tenTrungTam` · `maTrungTam` · `tenDangNhap` · `matKhauTam` · `vaiTro` · `emailHoSo` · `soDienThoai` · `ngaySinh` |
| `TraLoiLienHe` | Khách điền form trang đích (FR-30) | `tenKhach` · `tenTrungTam` · `hotline` |
| `NhacNoHocPhi` | *(cần job nền — đợt sau)* | `tenHocVien` · `tenLop` · `soTienConThieu` · `hanDong` |
| `NhacLichHoc` | *(cần job nền — đợt sau)* | `tenHocVien` · `tenLop` · `thoiGian` · `phongHoc` · `tenGiaoVien` |

Hai mẫu cuối **có trong danh mục nhưng chưa tự gửi** — hệ thống chưa có job nền chạy theo lịch.
Người dùng vẫn soạn trước được; khi job nền xong thì chúng chạy ngay mà không phải sửa gì.

Đưa chúng vào danh mục ngay từ đầu chứ không đợi, vì thêm loại sau nghĩa là trung tâm phải
quay lại soạn thêm — còn có sẵn thì họ soạn một lần.

### Thư báo tài khoản khi tạo người dùng (09/10/2026)

Hai đường tạo tài khoản đều gửi được, qua chung một service `IThuChaoMung`:

- **Tạo người kèm tài khoản** — `POST /api/v1/nguoi-dung`, cờ `taiKhoan.guiEmailThongBao`;
- **Cấp tài khoản cho người đã có hồ sơ** — `POST /api/v1/tai-khoan`, cờ `guiEmailThongBao`.

Thư gồm **bộ ba đăng nhập** (mã trung tâm · tên đăng nhập · mật khẩu tạm) và **hồ sơ** đã khai
hộ người đó, để họ soát lại và báo sai ngay — thay vì phát hiện sai số điện thoại vào lúc
trung tâm cần gọi gấp.

**Ba quyết định đáng biết:**

1. **Mặc định TẮT.** Gửi email không rút lại được: mật khẩu tạm đã nằm trong hộp thư người ta.
   Nên người tạo phải tích chọn; client cũ và `TenantSeeder` không vô tình gửi.
2. **Gửi trong handler, không để gửi lại sau.** Thư mang mật khẩu dạng rõ, mà hệ thống chỉ lưu
   hash — qua khỏi lệnh tạo là không ai đọc lại được. Muốn gửi lại thì phải đặt lại mật khẩu.
3. **Lỗi gửi KHÔNG làm hỏng lệnh.** Người và tài khoản đã ghi xong; ném lỗi sẽ trả 500 cho một
   lệnh đã thành công, và người tạo bấm Lưu lần nữa sẽ nhận `USERNAME_DA_TON_TAI` rồi tưởng
   mình làm sai. Lỗi SMTP vào log. Cùng lựa chọn với `GuiLienHeHandler`.

Hồ sơ không có email thì **không gửi và không báo lỗi** — ca thường gặp, không phải sự cố. Giao
diện khoá sẵn ô tích và nói rõ vì sao, để người dùng không tưởng chức năng hỏng.

Tên đăng nhập trong thư là tên **đã ghép đuôi**: trung tâm khai đuôi thì đăng nhập phải gõ đủ
cả đuôi, gửi tên thô là gửi một tên không tồn tại. Canh bởi `ThuChaoMungTests`.

### Biến thay thế

Dạng `{{tenHocVien}}`. Trong trình soạn thảo, biến hiện dạng **chip không tách rời được** —
xoá là mất cả biến, không thể thành `{{tenHoc}}`.

Lý do: biến hỏng không gây lỗi nào. Email vẫn gửi, chỉ là học viên nhận được một chuỗi khó
hiểu giữa câu văn. Lỗi im lặng thì phải chặn từ chỗ nhập.

**Biến không thuộc danh sách của loại mẫu đó bị bỏ qua khi gửi**, giữ nguyên dạng `{{...}}` —
không thay bằng chuỗi rỗng. Chuỗi rỗng làm câu văn cụt mà không ai biết vì sao.

### Soạn thảo

Trình soạn thảo có định dạng (đậm, nghiêng, danh sách, liên kết) — **Tiptap**, giấy phép MIT.

**Nội dung lưu dạng HTML ngữ nghĩa.** Lúc gửi, backend bọc nó vào khung `<table>` cố định rồi
inline CSS bằng PreMailer.Net. Xem ADR-0010 để biết vì sao không lưu sẵn HTML đã xử lý.

### Mẫu mặc định

Mỗi loại có sẵn một mẫu tiếng Việt viết sẵn trong mã, dùng khi trung tâm chưa soạn. Không để
trống — trống nghĩa là email không gửi được cho tới khi có người vào soạn, mà người đó không
biết mình cần làm việc đó.

## Cấu hình thực tế: phải làm gì ở nhà cung cấp

**Nhập tài khoản vào hệ thống là chưa đủ** với mọi nhà cung cấp lớn. Đây là phần người dùng
hay mắc, nên màn thiết lập in sẵn ba bước Gmail ngay trên form.

### Gmail / Google Workspace

Google **chặn đăng nhập SMTP bằng mật khẩu thường từ 30/05/2022** (bỏ "Less secure app
access"). Nhập mật khẩu Gmail vào hệ thống sẽ nhận `535-5.7.8 Username and Password not
accepted` — và thông báo đó **không nói gì** về mật khẩu ứng dụng, nên người dùng sẽ đi đổi
mật khẩu Gmail (thứ vốn đúng).

Phải làm ở phía Google trước:

1. Bật **xác minh 2 bước** cho tài khoản. Bắt buộc — không bật thì mục ở bước 2 không tồn tại.
2. Vào `myaccount.google.com/apppasswords`, tạo **mật khẩu ứng dụng**, sao chép 16 ký tự.
3. Dán 16 ký tự đó vào ô Mật khẩu (không phải mật khẩu đăng nhập).

| Ô | Giá trị |
|---|---|
| Máy chủ SMTP | `smtp.gmail.com` |
| Cổng | `587` |
| Tên đăng nhập | địa chỉ Gmail đầy đủ |
| Mật khẩu | 16 ký tự mật khẩu ứng dụng |
| Địa chỉ người gửi | **cùng** địa chỉ Gmail — Gmail viết đè nếu khác |

Hạn mức: Gmail thường ~500 thư/ngày, Workspace ~2.000. Vượt thì bị khoá gửi 24 giờ. Trung
tâm gửi hàng loạt nên dùng dịch vụ chuyên (SendGrid, Amazon SES, Mailgun) thay vì Gmail.

### Nhà cung cấp khác

Cách làm giống nhau: lấy thông số SMTP của họ rồi điền. Nhiều nơi cũng yêu cầu mật khẩu
riêng cho ứng dụng thay vì mật khẩu đăng nhập.

**Về số cổng** — hệ thống tự suy kiểu mã hoá từ cổng, không có ô chọn riêng:

| Cổng | Kiểu | Khi nào |
|---|---|---|
| `587` | STARTTLS | Mặc định, Gmail và phần lớn nhà cung cấp khuyến nghị |
| `465` | SSL/TLS ngầm | Nhiều hosting Việt Nam chỉ mở cổng này |
| khác | TLS nếu máy chủ có | Máy chủ thư nội bộ không có chứng chỉ |

Đây là lý do FR-31 dùng **MailKit** chứ không `System.Net.Mail.SmtpClient`: `SmtpClient` với
`EnableSsl = true` **chỉ làm STARTTLS**, nên cổng 465 không bao giờ gửi được — Gmail trả
`Syntax error, command unrecognized` (thử thật 30/09/2026). Microsoft cũng khuyến nghị
MailKit cho code mới.

### Luôn bấm "Gửi thử" sau khi lưu

Cấu hình sai **không** báo lỗi lúc lưu — hệ thống chỉ lưu thông số, không kết nối thử. Sai
thì email chết im lặng và chỉ lộ ra khi học viên phàn nàn. Nút gửi thử tồn tại vì lý do đó.

## Quyền

| Chức năng | Thao tác | Ghi chú |
|---|---|---|
| `ThietLapEmail` | `Xem` · `Sua` · `GuiThu` | Tách khỏi `ThietLapChung`: người sửa tên trung tâm chưa chắc được đụng vào cấu hình gửi thư |
| `MauEmail` | `Xem` · `Sua` | Soạn nội dung — việc của người làm nội dung |

`GuiThu` tách riêng vì nó **gửi thật một email ra ngoài**, khác với sửa cấu hình trong DB.

## Chưa làm trong phạm vi này

- **Job nền** cho nhắc nợ học phí và nhắc lịch học (nợ N10) — hai mẫu đã có danh mục, chỉ
  thiếu lịch chạy
- Nhật ký email đã gửi (gửi cho ai, lúc nào, thành công không)
- Đính kèm tệp
- Mẫu riêng cho từng ngôn ngữ — hiện một mẫu dùng chung, đúng nguyên tắc i18n hiện có (dịch
  giao diện, không dịch nội dung người dùng nhập)
