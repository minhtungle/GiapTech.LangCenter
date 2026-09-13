# Nghiệp vụ

Phân tích nghiệp vụ chi tiết theo từng module. Mỗi chức năng có **mã FR-xx** dùng để tham chiếu
chéo giữa tài liệu, commit, PR, test và code review.

Nguồn gốc: đặc tả kỹ thuật LMS Vietgenedu. Repo tách ra từ một dự án khác trên cùng nền tảng; phần nghiệp
vụ cũ đã gỡ khỏi cả code lẫn tài liệu, chỉ còn dấu vết trong [nhật ký](../nhat-ky/README.md) và
git history.

## Ba hệ thống con (08/09/2026)

Chức năng phân quyền được **nhóm** thành ba hệ thống, người dùng chọn hệ thống đang làm việc và
sidebar chỉ hiện phần của nó. Đây là cách nhóm để lọc giao diện — **không phải ba ứng dụng**:
một API, một database, một lần đăng nhập.

| Hệ thống | Module | Trạng thái |
|---|---|---|
| **HRM** | Hồ sơ nhân sự (nhân viên · giáo viên · trợ giảng) | ✅ Chạy |
| | **Cơ cấu tổ chức** (FR-22) — cây phòng ban, người quản lý | ✅ Chạy |
| | **Hồ sơ nhân sự mở rộng** (FR-23) — CCCD, số TK, MXH nhiều dòng, tệp hồ sơ xem online | ✅ Chạy |
| | **Danh mục chức vụ** (FR-24) — gồm "Ban quản lý" | ✅ Chạy |
| **CRM** | Khách hàng · Doanh thu · Khoá học · Sản phẩm (FR-17 → FR-20) | ✅ Chạy |
| **CRM → LMS** | Yêu cầu xếp lớp (FR-21) | ✅ Chạy |
| **LMS** | FR-07 → FR-14 + Học viên | ✅ Chạy |
| **LMS** | **Học tập trực tuyến** (FR-25 → FR-27) — kênh học thứ hai, song song lớp offline | 📝 Đặc tả |
| **Dùng chung** | **Tổng quan** (FR-15) — màn chủ, một màn cho mọi vai trò | ✅ Chạy |
| **Dùng chung** | FR-03 → FR-06, FR-16 (tài khoản, phân quyền, thiết lập, nhật ký) | ✅ Chạy |

Hai điều dễ nhầm:

- **Hồ sơ con người tách theo hệ thống**: nhân viên/giáo viên/trợ giảng ở **HRM**, học viên ở
  **LMS**. Học viên là *khách*, không phải nhân sự — người phụ trách tuyển sinh không nên thấy
  hợp đồng, lương của giáo viên. Vẫn **một bảng `NGUOI_DUNG`**, khác góc nhìn và khác quyền.
- **Tài khoản đăng nhập** ở nhóm dùng chung, không thuộc HRM: đó là quyền *đăng nhập* (việc của
  quản trị), và nó gán cho cả bốn vai trò.

→ Chi tiết: [phan-quyen-dong.md](../backend/phan-quyen-dong.md#ba-hệ-thống-con-hrm--crm--lms)

## Actor

| Actor | Mô tả | Đặc quyền riêng |
|---|---|---|
| **Admin** | 1 tài khoản mặc định mỗi trung tâm (`admin`) | Toàn quyền; **duy nhất** được đổi mật khẩu cho tài khoản khác; bắt buộc đổi mật khẩu ở lần đăng nhập đầu; là người duy nhất thấy toàn bộ lớp và toàn bộ sổ học phí |
| **Giáo viên** | Dạy một hoặc nhiều lớp | Chỉ thao tác trên **lớp mình phụ trách**. Không thấy học phí |
| **Trợ giảng** | Hỗ trợ lớp được phân công | Như giáo viên nhưng hẹp hơn ở bài kiểm tra và xoá buổi học |
| **Học viên** | Tài khoản gắn với các lớp đã ghi danh | Xem lịch, tự điểm danh, nộp bài, nhận xét buổi học, tra **công nợ của chính mình**. **Không** đọc được danh sách học viên khác |

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
| FR-15 | Thống kê / Tổng quan | [Thống kê](./thong-ke.md) |
| FR-16 | Nhật ký hệ thống | [Nhật ký hệ thống](./nhat-ky-he-thong.md) |
| FR-17 | Khách hàng (CRM) | [CRM](./crm.md) |
| FR-18 | Doanh thu — đăng ký khoá học (CRM) | [CRM](./crm.md) |
| FR-19 | Khoá học — danh mục khoá bán ra (CRM) | [CRM](./crm.md) |
| FR-20 | Sản phẩm khác — sách, học cụ (CRM) | [CRM](./crm.md) |
| FR-21 | Yêu cầu xếp lớp — cầu nối CRM → LMS | [CRM](./crm.md#fr-21--yêu-cầu-xếp-lớp-crm--lms) |
| FR-22 | Cơ cấu tổ chức (HRM) | [HRM](./hrm.md#fr-22--cơ-cấu-tổ-chức) |
| FR-23 | Hồ sơ nhân sự mở rộng (HRM) | [HRM](./hrm.md#fr-23--hồ-sơ-nhân-sự-mở-rộng) |
| FR-24 | Danh mục chức vụ (HRM) | [HRM](./hrm.md#fr-24--danh-mục-chức-vụ) |
| FR-28 | Thống kê CRM — doanh thu, phễu, công nợ | [Thống kê CRM](./thong-ke-crm.md) |
| FR-25 | Tài khoản cho khách hàng — cầu CRM → LMS | [Học tập trực tuyến](./hoc-tap-truc-tuyen.md#fr-25--tài-khoản-gán-cho-khách-hàng) *(đặc tả)* |
| FR-26 | Khoá học trực tuyến + tiến độ | [Học tập trực tuyến](./hoc-tap-truc-tuyen.md#fr-26--khoá-học-trực-tuyến) *(đặc tả)* |
| FR-27 | Bài tập cho khoá online | [Học tập trực tuyến](./hoc-tap-truc-tuyen.md#fr-27--bài-tập-cho-khoá-online) *(đặc tả)* |

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
