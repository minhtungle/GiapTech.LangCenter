# Module Quản trị hệ thống (FR-03 → FR-06)

## FR-03 — Tài khoản người dùng

CRUD tài khoản đăng nhập.

**Trường dữ liệu:** username, password, cờ "bắt buộc đổi mật khẩu", email, số điện thoại, địa chỉ,
danh sách quyền (nhiều nhóm quyền / tài khoản), hồ sơ cầu thủ liên kết (0..1).

### Quy trình chuẩn tạo tài khoản (wizard tuần tự)

1. **Tạo hồ sơ cầu thủ** nếu chưa có (FR-04) — hoặc chọn hồ sơ đã tồn tại, hoặc bỏ qua nếu tài khoản
   không gắn cầu thủ nào.
2. **Tạo/chọn nhóm quyền** nếu chưa có (FR-05).
3. **Tạo tài khoản**, gán quyền + hồ sơ cầu thủ.

### Quy tắc

- Chỉ **Admin** được đổi mật khẩu cho tài khoản khác. Người dùng thường chỉ đổi mật khẩu của chính mình.
- Một tài khoản liên kết **tối đa 1** hồ sơ cầu thủ (`NGUOI_DUNG.cau_thu_id` nullable).
- Username duy nhất trong phạm vi tenant, không phải toàn hệ thống.
- Xóa tài khoản không xóa hồ sơ cầu thủ liên kết — hai thực thể độc lập.

## FR-04 — Hồ sơ cầu thủ

CRUD hồ sơ: ảnh đại diện, họ tên, ngày sinh, ngày tham gia, ghi chú.

### Quy tắc

- **Độc lập hoàn toàn với tài khoản đăng nhập** — một cầu thủ có thể chưa có tài khoản (ví dụ cầu thủ
  mới, chỉ cần có mặt trong đội hình và danh sách đóng quỹ).
- Ảnh đại diện upload lên MinIO qua presigned URL, DB chỉ lưu đường dẫn.
- Trước khi xóa hồ sơ cầu thủ: cảnh báo nếu cầu thủ đang có dữ liệu liên quan (đội hình trận, đánh giá,
  đóng góp quỹ, vote MVP).

## FR-05 — Phân quyền truy cập

CRUD **nhóm quyền**. Mỗi nhóm cấu hình chi tiết theo **chức năng + thao tác**.

- Thao tác: `xem` / `thêm` / `sửa` / `xóa`.
- Một tài khoản có thể gán **nhiều nhóm quyền**; quyền hiệu lực = **hợp (union)** của tất cả các nhóm.
- Đây **không phải** role cố định kiểu `[Authorize(Roles=...)]` — quyền đọc động từ bảng
  `QUYEN_CHUC_NANG` tại runtime. Chi tiết triển khai: [phân quyền động](../backend/phan-quyen-dong.md).

### Giao diện

Ma trận chức năng × thao tác (checkbox), ưu tiên desktop vì thao tác phức tạp — xem
[nguyên tắc UI/UX](../frontend/ui-ux-nguyen-tac.md).

## FR-06 — Thiết lập chung

Thông tin CLB: tên đội, tên viết tắt, ngày thành lập, logo, ảnh bìa, mô tả.

### Quy tắc

- Tenant mới chưa cấu hình → dùng **giá trị mặc định**, không chặn người dùng vào hệ thống.
- Logo và ảnh bìa upload lên MinIO qua presigned URL.

## Tham chiếu

- Bảng `NGUOI_DUNG`, `CAU_THU`, `QUYEN`, `QUYEN_CHUC_NANG`, `NGUOIDUNG_QUYEN`, `TENANT` — xem
  [ERD](../database/erd.md).
