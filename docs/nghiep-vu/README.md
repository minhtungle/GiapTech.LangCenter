# Nghiệp vụ

> ⚠️ **Tài liệu của DỰ ÁN CŨ** (quản lý CLB đá bóng phong trào). Từ 05/09/2026 repo này là
> base cho hệ thống quản lý trung tâm ngoại ngữ — **phần nghiệp vụ dưới đây không còn trong
> code**. Giữ lại để tham khảo cách viết đặc tả. Xem [`CLAUDE.md`](../../CLAUDE.md) mục 1.

Phân tích nghiệp vụ chi tiết theo từng module. Mỗi chức năng có **mã FR-xx** dùng để tham chiếu chéo
giữa tài liệu, commit, PR, test và code review.

## Actor

| Actor | Mô tả | Đặc quyền riêng |
|---|---|---|
| **Admin** | 1 tài khoản mặc định mỗi tenant (`admin` / `123456`) | Toàn quyền; **duy nhất** được đổi mật khẩu cho tài khoản khác; bắt buộc đổi mật khẩu ở lần đăng nhập đầu |
| **Manager** | Tài khoản được cấp quyền theo từng chức năng/thao tác | Thường phụ trách vận hành trận đấu, tài chính, thống kê |
| **Player** | Tài khoản gắn 1 hồ sơ cầu thủ (0..1) | Xem lịch/thống kê, vote MVP, xem tiến độ quỹ |

## Bảng tra mã FR

| Mã | Chức năng | Module |
|---|---|---|
| FR-07 | Lớp học | [Lớp học](./lop-hoc.md) |
| FR-08 | Học viên trong lớp | [Lớp học](./lop-hoc.md) |
| FR-01 | Đăng nhập | [Đăng nhập](./dang-nhap.md) |
| FR-02 | Quên mật khẩu | [Đăng nhập](./dang-nhap.md) |
| FR-03 | Tài khoản người dùng | [Quản trị hệ thống](./quan-tri-he-thong.md) |
| FR-04 | Hồ sơ cầu thủ | [Quản trị hệ thống](./quan-tri-he-thong.md) |
| FR-05 | Phân quyền truy cập | [Quản trị hệ thống](./quan-tri-he-thong.md) |
| FR-06 | Thiết lập chung | [Quản trị hệ thống](./quan-tri-he-thong.md) |
| FR-07 | Lọc thông tin (trận đấu) | [Lịch thi đấu](./lich-thi-dau.md) |
| FR-08 | Danh sách trận đấu (Calendar/Datatable) | [Lịch thi đấu](./lich-thi-dau.md) |
| FR-09 | Chấp nhận lời mời từ đối thủ | [Lịch thi đấu](./lich-thi-dau.md) |
| FR-10 | Thêm/Cập nhật trận đấu (3 tab) | [Lịch thi đấu](./lich-thi-dau.md) |
| FR-11 | Xóa trận đấu | [Lịch thi đấu](./lich-thi-dau.md) |
| FR-12 | Lọc thông tin (thống kê) | [Thống kê](./thong-ke.md) |
| FR-13 | Biểu đồ diễn biến | [Thống kê](./thong-ke.md) |
| FR-14 | Bảng xếp hạng MVP | [Thống kê](./thong-ke.md) |
| FR-15 | Danh sách quỹ | [Tài chính](./tai-chinh.md) |
| FR-16 | Thêm/Cập nhật quỹ | [Tài chính](./tai-chinh.md) |
| FR-19 | Đăng ký đá trận qua link/QR | [Đăng ký nhanh](./dang-ky-nhanh-qua-link.md) |

## Quy tắc nghiệp vụ xuyên suốt

1. **Cách ly tenant**: mọi truy vấn nghiệp vụ lọc theo `tenant_id` — xem [multi-tenant](../backend/multi-tenant.md).
2. **Vote MVP**: mỗi người tối đa **1 tim / trận**, ràng buộc UNIQUE ở tầng DB chứ không chỉ chặn ở UI.
3. **Phân quyền động**: đọc từ bảng `QUYEN_CHUC_NANG` tại runtime — xem [phân quyền động](../backend/phan-quyen-dong.md).
4. **API trả mã lỗi**, không trả text cứng một ngôn ngữ; frontend tự dịch qua `react-i18next`.
