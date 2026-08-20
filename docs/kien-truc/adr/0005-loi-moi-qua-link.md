# ADR-0005 — Lời mời thách đấu qua link/QR: token công khai, không FK

- **Trạng thái:** Đã chốt
- **Ngày:** 20/08/2026
- **Liên quan:** [ADR-0004](./0004-ha-tang-tu-host-vps.md) · [FR-18](../../nghiep-vu/loi-moi-qua-link.md)

## Bối cảnh

Đội phong trào hẹn đá với nhau qua Zalo. Bên mời gõ tên đối thủ vào sổ ("FC Sông Hàn") — một
chuỗi, không liên kết gì. Nếu đối thủ cũng dùng hệ thống này thì hai bên vẫn có hai bộ dữ liệu
rời rạc, không đối đầu được.

Cần một cách để bên mời **kéo bên kia vào hệ thống** mà không đòi họ đăng ký trước.

## Ba quyết định

### 1. Token công khai cho việc XEM, đăng nhập cho việc CHẤP NHẬN

Người nhận chưa có tài khoản. Bắt đăng nhập trước khi xem lời mời là yêu cầu họ tạo đội cho một
thứ họ chưa biết nội dung — gần như chắc chắn họ bỏ.

Nên tách: **xem** chỉ cần token, **chấp nhận** cần đăng nhập.

Token lưu **hash**, không lưu thô — dùng lại đúng cơ chế của token đặt lại mật khẩu (FR-02).

**Đánh đổi:** ai giữ link cũng xem được nội dung lời mời. Chấp nhận được vì nội dung đó nhẹ (tên
CLB, giờ, sân, lời nhắn) và không lộ cầu thủ/quỹ/thành tích. Bù lại bằng bước xác nhận danh tính
trước khi chấp nhận, và cho người mời huỷ liên kết nếu sai người.

### 2. Bảng lời mời link RIÊNG, không mở rộng `LOI_MOI_BAT_DOI`

`LoiMoiThachDau` yêu cầu **cả hai** `TenantGuiId` và `TenantNhanId`. Lời mời qua link về bản chất
chưa biết bên nhận là tenant nào — nó trỏ tới một **đối thủ trong sổ của người gửi**.

Hai cách:

| Cách | Vấn đề |
|---|---|
| Cho `TenantNhanId` nullable | Mọi truy vấn hiện có phải xử lý null; ràng buộc "một lời mời đang chờ mỗi cặp CLB" mất nghĩa. Sửa một bảng đang chạy để nhồi ca mới vào |
| **Bảng riêng** `LOI_MOI_LINK` ✅ | Thêm một bảng. Nhưng `LOI_MOI_BAT_DOI` giữ nguyên bất biến của nó, và hai luồng không lẫn nhau |

Chọn bảng riêng. Khi lời mời link được chấp nhận, nó **sinh ra** một `LoiMoiThachDau` đã ở trạng
thái `DaChapNhan` — để lịch sử ở Hòm thư nhất quán với luồng FR-17.

### 3. Vẫn KHÔNG dùng FK tới `TENANT` cho đối thủ

Giữ nguyên quyết định của FR-10: `DoiThu.MaDoiHeThong` là **chuỗi mã đội**.

FK sẽ cho phép join xuyên tenant, và Cascade sẽ xoá lịch sử đối đầu của ta khi CLB kia xoá tài
khoản. Lời mời qua link không đổi gì ở lập luận này.

## Hệ quả

**Buộc mở đăng ký CLB ở production** (nợ N4). Không mở thì ca "đối thủ chưa có tài khoản" — ca phổ
biến nhất — không chạy được. Kèm theo: **rate limit bắt buộc** trước khi lên Internet, vì đăng ký
mở tự do là cửa cho script tạo CLB rác, và chúng hiện hết lên Cộng đồng.

**Thêm chỗ thứ tư đọc/ghi ngoài tenant hiện tại.** Ba chỗ cũ ở
[multi-tenant.md](../../backend/multi-tenant.md); FR-18 thêm: tra token → đọc `TENANT` của người
gửi, và khi chấp nhận thì **ghi vào tenant của người gửi** (gán `MaDoiHeThong`, tạo trận). Việc
ghi đó chỉ hợp lệ vì chính bên nhận vừa bấm đồng ý — cùng lập luận với `TraLoiThachDauHandler`.

## Đã cân nhắc và bỏ

**Gửi email/SMS thay cho link copy tay.** Chưa có hạ tầng (nợ), mà CLB phong trào vẫn dán link vào
Zalo là xong. Làm sau, không chặn tính năng.

**Sinh mã QR ở backend.** Frontend tự vẽ QR từ URL bằng thư viện là đủ; backend sinh ảnh QR thì
phải lưu file, dọn file, và không thêm giá trị gì.

**Cho phép chấp nhận mà không cần tài khoản** (kiểu "khách"). Bỏ: không có tenant thì không có
lịch để tạo trận, và toàn bộ mục đích của tính năng là hai bên có dữ liệu khớp nhau.
