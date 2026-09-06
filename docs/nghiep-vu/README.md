# Nghiệp vụ

Phân tích nghiệp vụ chi tiết theo từng module. Mỗi chức năng có **mã FR-xx** dùng để tham chiếu
chéo giữa tài liệu, commit, PR, test và code review.

Nguồn gốc: đặc tả kỹ thuật LMS Vietgenedu. Repo trước đây là hệ quản lý CLB bóng đá; phần nghiệp
vụ cũ đã gỡ khỏi cả code lẫn tài liệu, chỉ còn dấu vết trong [nhật ký](../nhat-ky/README.md) và
git history.

## Actor

| Actor | Mô tả | Đặc quyền riêng |
|---|---|---|
| **Admin** | 1 tài khoản mặc định mỗi trung tâm (`admin`) | Toàn quyền; **duy nhất** được đổi mật khẩu cho tài khoản khác; bắt buộc đổi mật khẩu ở lần đăng nhập đầu; là người duy nhất thấy toàn bộ lớp và toàn bộ sổ học phí |
| **Giáo viên** | Dạy một hoặc nhiều lớp | Chỉ thao tác trên **lớp mình phụ trách**. Không thấy học phí |
| **Trợ giảng** | Hỗ trợ lớp được phân công | Như giáo viên nhưng hẹp hơn ở bài kiểm tra và xoá buổi học |
| **Học viên** | Tài khoản gắn với các lớp đã ghi danh | Xem lịch, tự điểm danh, nộp bài, tra **công nợ của chính mình** |

> Vai trò là **nhóm quyền có sẵn**, không phải enum cứng trong code. Phân quyền đọc động từ
> `QUYEN_CHUC_NANG` (quy tắc #9); `LoaiNguoiDung` chỉ dùng để lọc danh sách, **không bao giờ**
> dùng để phân quyền.

## Bảng tra mã FR

| Mã | Chức năng | Module |
|---|---|---|
| FR-01 | Đăng nhập | [Đăng nhập](./dang-nhap.md) |
| FR-02 | Quên mật khẩu | [Đăng nhập](./dang-nhap.md) |
| FR-03 | Người dùng (hồ sơ con người) | [Quản trị hệ thống](./quan-tri-he-thong.md) |
| FR-04 | Tài khoản đăng nhập | [Quản trị hệ thống](./quan-tri-he-thong.md) |
| FR-05 | Phân quyền truy cập | [Quản trị hệ thống](./quan-tri-he-thong.md) |
| FR-06 | Thiết lập chung | [Quản trị hệ thống](./quan-tri-he-thong.md) |
| FR-07 | Lớp học | [Lớp học](./lop-hoc.md) |
| FR-08 | Học viên trong lớp | [Lớp học](./lop-hoc.md) |
| FR-09 | Buổi học & sinh lịch | [Buổi học & Điểm danh](./buoi-hoc-diem-danh.md) |
| FR-10 | Điểm danh | [Buổi học & Điểm danh](./buoi-hoc-diem-danh.md) |
| FR-11 | Bài tập | [Học liệu](./hoc-lieu.md) |
| FR-12 | Bài nộp | [Học liệu](./hoc-lieu.md) |
| FR-13 | Tài liệu | [Học liệu](./hoc-lieu.md) |
| FR-14 | Học phí & công nợ | [Học phí](./hoc-phi.md) |
| FR-15 | Thống kê / Dashboard | *(chưa làm)* |

## Quy tắc nghiệp vụ xuyên suốt

1. **Cách ly tenant**: mọi truy vấn nghiệp vụ lọc theo `tenant_id` — xem
   [multi-tenant](../backend/multi-tenant.md).
2. **Phân quyền động**: đọc từ bảng `QUYEN_CHUC_NANG` tại runtime — xem
   [phân quyền động](../backend/phan-quyen-dong.md).
3. **Phạm vi trong tenant**: `[RequirePermission]` chỉ gác cửa endpoint, Query Filter chỉ lọc
   tenant. Việc "chỉ lớp mình phụ trách" do `IPhamViLopHoc` lo, "chỉ sổ học phí của mình" do
   `IPhamViHocPhi` lo — **hai tầng riêng**, xem [Học phí](./hoc-phi.md#phạm-vi-truy-cập--tách-khỏi-phạm-vi-lớp).
4. **API trả mã lỗi**, không trả text cứng một ngôn ngữ; frontend tự dịch qua `react-i18next`.
5. **Người ≠ tài khoản.** `NGUOI_DUNG` là con người, `TAI_KHOAN` là cách họ đăng nhập. Vô hiệu
   hoá tài khoản không đụng tới dữ liệu người dùng — đó là lý do hai bảng tách nhau.
6. **Không xoá cứng dữ liệu đã dùng**: học viên nghỉ thì đổi trạng thái, lớp đã chạy thì huỷ —
   xoá làm điểm danh và học phí thành dữ liệu treo (quy tắc #1).
