# Prompt: gắn hộp thư cho cổng thật trên VPS

> Tạo 10/10/2026.
>
> Copy toàn bộ phần dưới (từ dòng `---`) và dán vào Claude Code đang chạy **trên VPS**.
>
> **Thay ba chỗ `<...>` bằng giá trị thật trước khi dán.** Prompt không ghi sẵn mật khẩu —
> ghi sẵn một giá trị sai thì người làm cứ thế dùng mà không nghi ngờ.

---

Tôi cần bạn cấu hình **hộp thư gửi đi** cho hệ thống GiapTech.LangCenter đang chạy trên VPS
này, và kiểm chứng là thư đi được thật.

## Thông tin

- **Thư mục triển khai**: `/opt/langcenter`
- **Mã trung tâm**: `<ma-trung-tam>`
- **Tài khoản SMTP**: `<dia-chi-email>` · mật khẩu `<mat-khau>`
- **Nhà cung cấp**: iNET — máy chủ `mail.<ten-mien>`, **cổng 465** (SSL ngầm)

> Cổng 465 không phải mặc định của hệ thống. Đã thử thật ngày 09/10/2026 với iNET: **587 và 25
> đều không đăng nhập được, chỉ 465 chạy**. Đừng "sửa" thành 587 vì thấy nó phổ biến hơn.

## Đọc trước khi làm

1. `docs/06-nghiep-vu/email.md` — FR-31, cách hệ thống chọn cấu hình SMTP và 4 mẫu nội dung.
2. `docs/07-ha-tang/bien-moi-truong.md` mục `EMAIL_KHOA_MA_HOA` — vì sao khoá mã hoá để ngoài DB.
3. `docs/02-kien-truc/adr/0010-cau-hinh-email-theo-tenant.md` — quyết định mỗi tenant một hộp thư.

## Việc cần làm

### Bước 1 — Kiểm hai biến môi trường

Xem `/opt/langcenter/.env` có đủ hai biến này chưa:

| Biến | Thiếu thì sao |
|---|---|
| `EMAIL_KHOA_MA_HOA` | Màn Thiết lập email **từ chối lưu mật khẩu** và báo trước cho người dùng |
| `APP_BASE_URL` | Thư chào mừng tài khoản có **dòng link rỗng** — người nhận không biết gõ địa chỉ nào |

`EMAIL_KHOA_MA_HOA` sinh bằng `openssl rand -base64 32`.

⚠️ **Nếu biến này ĐÃ CÓ, tuyệt đối không đổi.** Đổi khoá = mọi mật khẩu SMTP đã lưu thành
không giải mã được, mọi trung tâm phải nhập lại. Hệ thống không sập, nó chỉ âm thầm ngừng gửi
từ hộp thư riêng.

`APP_BASE_URL` đặt bằng địa chỉ người dùng thật sự truy cập, **không có dấu `/` cuối**.

Thiếu biến nào thì thêm vào `.env`, rồi `docker compose up -d` để nạp lại. Báo tôi biết bạn
đã thêm gì trước khi khởi động lại.

### Bước 2 — Khai hộp thư trong giao diện

Vào **Quản trị → Thiết lập email**, điền:

| Trường | Giá trị |
|---|---|
| Máy chủ SMTP | `mail.<ten-mien>` |
| Cổng | `465` |
| Tên đăng nhập | `<dia-chi-email>` |
| Mật khẩu | `<mat-khau>` |
| Địa chỉ người gửi | `<dia-chi-email>` |
| Tên hiển thị | tên trung tâm |

Cần quyền `ThietLapEmail` (Xem + Sửa). Tài khoản quản trị có sẵn.

**Làm qua giao diện, không ghi thẳng vào DB.** Mật khẩu phải đi qua đường mã hoá AES-GCM của
hệ thống; ghi tay vào cột `smtp_mat_khau_ma_hoa` sẽ tạo ra bản mã không giải mã được, và lỗi
chỉ lộ ra lúc gửi thư thật.

### Bước 3 — Gửi thử và ĐỌC KẾT QUẢ

Màn đó có ô **Gửi thử**. Nhập một địa chỉ bạn mở được hộp thư, bấm gửi.

- **204** = thư đã rời hệ thống. Vào hộp thư xác nhận nó tới thật, kể cả thư rác.
- **`SMTP_SAI_DANG_NHAP`** = máy chủ từ chối tài khoản/mật khẩu. Kiểm lại mật khẩu — mật khẩu
  webmail/cPanel có thể khác mật khẩu SMTP. **Đừng đoán, hỏi tôi.**
- **Không có lỗi nhưng thư không tới** = xem log `docker compose logs api | grep -i smtp`.

Gửi thử là bước **bắt buộc**, không được bỏ: cấu hình sai mà không thử thì email chết im lặng —
không có lỗi nào ở đâu cho tới khi một học viên không nhận được tài khoản.

### Bước 4 — Kiểm thư chào mừng có link

Tạo một người dùng thử ở **Quản trị → Người dùng**, có điền email, tích **"Gửi email báo thông
tin tài khoản và hồ sơ"**.

Mở thư nhận được và kiểm **có dòng link đăng nhập** không. Không có link = `APP_BASE_URL` chưa
đặt hoặc đặt sai — quay lại bước 1.

Xoá người dùng thử sau khi kiểm xong.

## Ràng buộc

- **Không đổi `EMAIL_KHOA_MA_HOA` nếu nó đã có giá trị** (xem bước 1).
- **Không ghi mật khẩu SMTP thẳng vào DB** — chỉ qua giao diện.
- **Không commit mật khẩu** vào repo, kể cả trong file ví dụ hay ghi chú.
- Mọi thao tác ghi vào `.env` hay restart container: **báo tôi trước khi làm**.
- Nếu bước nào không chạy như mô tả, **dừng lại hỏi** thay vì thử phương án khác — cấu hình
  email sai kiểu "gần đúng" sẽ gửi thư từ địa chỉ không mong muốn.

## Báo cáo

Xong thì cho tôi biết:

1. Hai biến môi trường: đã có sẵn hay bạn vừa thêm?
2. Gửi thử: mã trả về, và thư có tới hộp thư không?
3. Thư chào mừng: có link đăng nhập không, link trỏ đi đâu?
