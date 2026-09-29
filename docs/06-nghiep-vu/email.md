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
| `ChaoMungHocVien` | Cấp tài khoản cho học viên | `tenHocVien` · `tenTrungTam` · `maTrungTam` · `tenDangNhap` · `matKhauTam` |
| `TraLoiLienHe` | Khách điền form trang đích (FR-30) | `tenKhach` · `tenTrungTam` · `hotline` |
| `NhacNoHocPhi` | *(cần job nền — đợt sau)* | `tenHocVien` · `tenLop` · `soTienConThieu` · `hanDong` |
| `NhacLichHoc` | *(cần job nền — đợt sau)* | `tenHocVien` · `tenLop` · `thoiGian` · `phongHoc` · `tenGiaoVien` |

Hai mẫu cuối **có trong danh mục nhưng chưa tự gửi** — hệ thống chưa có job nền chạy theo lịch.
Người dùng vẫn soạn trước được; khi job nền xong thì chúng chạy ngay mà không phải sửa gì.

Đưa chúng vào danh mục ngay từ đầu chứ không đợi, vì thêm loại sau nghĩa là trung tâm phải
quay lại soạn thêm — còn có sẵn thì họ soạn một lần.

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
