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

Gửi nhắc nhở qua **SMS / Email** tới những người **chưa đóng đủ**.

- Email: SMTP (SendGrid / Gmail API).
- SMS: SMS Gateway nội địa (eSMS / Speedsms).
- Cấu hình: xem [biến môi trường](../ha-tang/bien-moi-truong.md).

### Quy tắc

- Số tiền mỗi người có thể khác nhau trong cùng một đợt quỹ (`DONGGOP_QUY.so_tien_can_dong` theo từng
  cầu thủ).
- `so_tien_da_dong` cho phép đóng **từng phần** — tiến độ tính theo `so_tien_da_dong / so_tien_can_dong`.
- Đối tượng đóng quỹ là **hồ sơ cầu thủ** (`CAU_THU`), không phải tài khoản đăng nhập — cầu thủ chưa có
  tài khoản vẫn nằm trong danh sách đóng quỹ.
- Player chỉ **xem** tiến độ quỹ, không được sửa số tiền.

## Tham chiếu

- Bảng `QUY`, `DONGGOP_QUY`, `CAU_THU` — xem [ERD](../database/erd.md).
