# Module Tài chính — Quỹ đội (FR-15, FR-16)

> Đây là dữ liệu nhạy cảm. Mọi PR đụng tới module này **bắt buộc có security review** — xem
> [CONTRIBUTING.md](../../CONTRIBUTING.md) mục 4.

## FR-15 — Danh sách quỹ

Lịch sử đóng quỹ, hiển thị:

| Cột | Mô tả |
|---|---|
| Thời gian | Thời điểm tạo đợt quỹ / thời hạn |
| Tên quỹ | Ví dụ "Quỹ tháng 3", "Quỹ thuê sân giải X" |
| Tiến độ đóng | Đã đóng / Tổng — hiển thị dạng thanh tiến độ |

Màu trạng thái theo quy ước chung: **xanh** = đã đóng đủ, **đỏ** = quá hạn, **vàng** = đang chờ.

## FR-16 — Thêm / Cập nhật quỹ

**Trường dữ liệu:** tên quỹ, chọn thành viên + số tiền mỗi người, ghi chú, thời hạn.

### Nhắc nhở

**Hiện tại:** nút **Sao chép danh sách nợ** — chép tên + số tiền còn thiếu ra clipboard để dán
vào Zalo/Messenger. Đó là cách CLB phong trào nhắc nợ thật, và dùng được ngay mà không cần
tài khoản dịch vụ nào.

**Chưa làm:** gửi tự động qua SMS / Email. Hạ tầng gửi chưa cấu hình.

- Email: SMTP (SendGrid / Gmail API).
- SMS: SMS Gateway nội địa (eSMS / Speedsms).
- Cấu hình: xem [biến môi trường](../ha-tang/bien-moi-truong.md).

### Quy tắc

- Số tiền mỗi người có thể khác nhau trong cùng một đợt quỹ (`DONGGOP_QUY.so_tien_can_dong` theo từng
  cầu thủ).
- `so_tien_da_dong` cho phép đóng **từng phần** — tiến độ tính theo `so_tien_da_dong / so_tien_can_dong`.
- Đối tượng đóng quỹ là **hồ sơ cầu thủ** (`CAU_THU`), không phải tài khoản đăng nhập — cầu thủ chưa có
  tài khoản vẫn nằm trong danh sách đóng quỹ.
- Player chỉ **xem** tiến độ quỹ, không được sửa số tiền: endpoint đọc dùng quyền `Xem`,
  mọi endpoint đổi tiền dùng `Sua`/`Them`/`Xoa`.

### Bảo toàn tiền (quy tắc #1)

Ba ràng buộc, cả ba đã kiểm chứng bằng phản chứng:

- **Sửa đợt quỹ không đụng `so_tien_da_dong`.** Lệnh lưu quỹ chỉ gán `so_tien_can_dong`; gán
  cả số đã đóng sẽ xoá trắng tiền thật đã vào túi mỗi lần thủ quỹ sửa tên đợt.
  Canh bởi `Sua_quy_khong_lam_mat_tien_da_thu`.
- **Không gỡ được người đã đóng tiền** khỏi đợt quỹ (`KHONG_XOA_NGUOI_DA_DONG_TIEN`) — xoá là
  mất vết một khoản tiền có thật. Muốn gỡ thì hoàn số tiền về 0 trước.
- **Đợt quỹ đã thu tiền không xoá được** (`QUY_DA_THU_TIEN_KHONG_XOA_DUOC`). Muốn ẩn khỏi
  danh sách thì đóng đợt quỹ.

Hoàn tiền về 0 sẽ **xoá luôn `ngay_dong`** — không thì báo cáo thấy "đóng ngày X, số tiền 0".

## Khoản chi và số dư *(ngoài phạm vi FR-15/16)*

Bảng `KHOAN_CHI`. Thu tiền vào mà không ghi được tiền ra thì con số "đã thu" không nói lên quỹ
còn bao nhiêu.

- `quy_id` **nullable**: chi có thể thuộc một đợt quỹ hoặc là chi chung của CLB. Bắt buộc gắn
  đợt sẽ khiến thủ quỹ tạo đợt quỹ giả chỉ để ghi một khoản chi.
- Xoá đợt quỹ **không xoá** khoản chi (`SetNull`, không Cascade): tiền đã tiêu là sự thật kế
  toán, giữ lại dưới dạng chi chung. Kiểm bằng tay trên PostgreSQL thật — in-memory không
  thực thi ràng buộc FK.
- Khoản chi **xoá được** (khác đợt quỹ đã thu tiền): thủ quỹ gõ nhầm một dòng chi là chuyện
  thường, không xoá được thì họ phải sửa nó thành "0 đồng" — bẩn hơn.
- **Số dư = đã thu − đã chi**, dùng tiền *thực nhận* chứ không phải tiền phải thu: quỹ chỉ
  tiêu được số đã vào túi. Số dư âm hiện màu đỏ.

## Tham chiếu

- Bảng `QUY`, `DONGGOP_QUY`, `CAU_THU` — xem [ERD](../database/erd.md).
