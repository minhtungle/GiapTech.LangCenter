# Tổng thuật — đọc 1 mạch trước khi đào sâu

**LMS cho trung tâm ngoại ngữ** (Vietgenedu) — ứng dụng web **multi-tenant**: mỗi trung tâm đăng
ký là 1 tenant độc lập, dữ liệu cách ly hoàn toàn theo `tenant_id`. Đăng nhập bằng bộ ba
**{mã trung tâm, tên đăng nhập, mật khẩu}**.

## Ba hệ thống con

Chức năng phân quyền được nhóm thành **HRM** (nhân sự) · **CRM** (khách hàng) · **LMS** (đào
tạo). Người dùng có quyền ở nhiều hệ thống thì có **bộ chuyển** cạnh nút Đăng xuất, và sidebar
chỉ hiện phần của hệ thống đang chọn.

Đây là cách **nhóm để lọc giao diện, không phải ba ứng dụng**: một API, một database, một lần
đăng nhập. Nhóm **dùng chung** (tài khoản, phân quyền, thiết lập, nhật ký) hiện ở mọi hệ thống.

Phần LMS đầy đủ nghiệp vụ; HRM và CRM hiện chỉ có hồ sơ nhân sự chạy thật, ba module còn lại là
khung trống. → [phan-quyen-dong.md](./backend/phan-quyen-dong.md#ba-hệ-thống-con-hrm--crm--lms)

## 5 nhóm chức năng

| # | Module | Nội dung | Mã FR |
|---|---|---|---|
| 1 | [Đăng nhập](./nghiep-vu/dang-nhap.md) | Xác thực theo trung tâm, quên mật khẩu, refresh token có xoay vòng | FR-01 → FR-02 |
| 2 | [Quản trị hệ thống](./nghiep-vu/quan-tri-he-thong.md) | **Người dùng** (hồ sơ con người + hồ sơ riêng theo vai trò) tách khỏi **tài khoản** (đăng nhập) — từ 08/09 hồ sơ chia hai màn: Nhân sự (HRM) · Học viên (LMS); phân quyền động 20 chức năng × thao tác, có tab theo hệ thống; thiết lập chung | FR-03 → FR-06 |
| 3 | [Lớp học](./nghiep-vu/lop-hoc.md) | Vòng đời lớp (nháp → sắp khai giảng → đang học → kết thúc), phân công giáo viên/trợ giảng, ghi danh học viên với học phí riêng từng người | FR-07 → FR-08 |
| 4 | [Buổi học & Điểm danh](./nghiep-vu/buoi-hoc-diem-danh.md) | Sinh lịch tự động theo thứ trong tuần, lịch dạng calendar, view chi tiết buổi 5 tab; điểm danh **2 nguồn** (học viên tự khai + giáo viên chốt); **nhận xét hai chiều** | FR-09 → FR-10 |
| 5 | [Học liệu](./nghiep-vu/hoc-lieu.md) · [Học phí](./nghiep-vu/hoc-phi.md) | Bài tập (nộp nhiều lần, giữ lịch sử), tài liệu, tệp đính kèm; sổ thu và công nợ | FR-11 → FR-14 |

## 4 actor

- **Admin** — 1 tài khoản mặc định mỗi trung tâm, toàn quyền, duy nhất được đổi mật khẩu cho tài
  khoản khác, bắt buộc đổi mật khẩu ở lần đăng nhập đầu. Người duy nhất thấy **mọi lớp** và
  **toàn bộ sổ học phí**.
- **Giáo viên** — chỉ thao tác trên lớp mình phụ trách. **Không** thấy học phí.
- **Trợ giảng** — như giáo viên, hẹp hơn ở bài kiểm tra và xoá buổi học.
- **Học viên** — xem lịch, tự điểm danh trong khung giờ, nộp bài, nhận xét buổi học, tra **công
  nợ của chính mình**. **Không** đọc được danh sách hay nhận xét của học viên khác.

Vai trò là **nhóm quyền có sẵn**, không phải enum cứng trong code.

## Điểm cần nắm trước khi code

1. **Cách ly tenant** là quy tắc số một — [multi-tenant.md](./backend/multi-tenant.md).
2. **Phân quyền đọc động từ DB**, không dùng role cố định —
   [phan-quyen-dong.md](./backend/phan-quyen-dong.md).
3. **Người ≠ tài khoản.** `NGUOI_DUNG` giữ con người và sống lâu hơn `TAI_KHOAN`. Hai cột
   trạng thái riêng: `trang_thai_nhan_su` (còn làm không) và `trang_thai` của tài khoản (còn
   đăng nhập được không). Tra quyền dùng **id tài khoản**; khoá ngoại nghiệp vụ dùng **id
   người** — lẫn hai thứ này sẽ trả rỗng một cách im lặng.
4. **Phạm vi bên trong tenant là tầng riêng.** `[RequirePermission]` chỉ gác cửa endpoint;
   Query Filter chỉ lọc tenant. "Chỉ lớp mình dạy" và "chỉ sổ học phí của mình" là **hai tầng
   khác nhau** (`IPhamViLopHoc`, `IPhamViHocPhi`) — gộp chúng là mở sổ thu cho mọi giáo viên.
5. **Mọi thời điểm là `DateTimeOffset` UTC**; giờ địa phương suy từ `TENANT.mui_gio` khi hiển
   thị. Không lưu cột "ngày" tách rời — nó sẽ lệch khi trung tâm đổi múi giờ.
6. **API trả mã lỗi**, frontend dịch qua `react-i18next` —
   [cqrs-mediatr.md](./backend/cqrs-mediatr.md#trả-lỗi).

## Tiến độ

Xem [ke-hoach.md](./ke-hoach.md) — bảng trạng thái mã FR, lộ trình và nợ kỹ thuật.

## Đi tiếp

- Kiến trúc & bản đồ công nghệ: [kien-truc/TONG-QUAN-KIEN-TRUC.md](./kien-truc/TONG-QUAN-KIEN-TRUC.md)
- Quyết định kiến trúc (ADR): [kien-truc/adr/](./kien-truc/adr/)
- Thuật ngữ dễ nhầm: [kien-truc/THUAT-NGU.md](./kien-truc/THUAT-NGU.md)
- Mô hình dữ liệu: [database/erd.md](./database/erd.md)
