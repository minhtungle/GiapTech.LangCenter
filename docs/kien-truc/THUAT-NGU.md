# Thuật ngữ & quy ước tên gọi dễ nhầm lẫn

| Thuật ngữ | Ý nghĩa trong dự án này | Lưu ý tránh nhầm lẫn |
|---|---|---|
| Tenant | Một **trung tâm ngoại ngữ** độc lập, dữ liệu cách ly qua `tenant_id` | Không nhầm với "user" hay "organization" trong tài liệu bên thứ ba |
| Quyền (`QUYEN`) | **Nhóm quyền** tự định nghĩa theo chức năng + thao tác | Khác `[Authorize(Roles=...)]` cố định của ASP.NET Core — đây là hệ phân quyền **động** đọc từ DB |
| Vai trò (Giáo viên, Học viên…) | Chỉ là **nhóm quyền dựng sẵn** do seeder tạo, người dùng đổi được | **Không** phải enum cứng trong code. `LoaiNguoiDung` chỉ dùng lọc danh sách, không bao giờ dùng phân quyền |
| Phạm vi (`IPhamViLopHoc`, `IPhamViHocPhi`) | Lọc **dữ liệu** bên trong một tenant: "lớp mình dạy", "sổ thu của mình" | Khác với **quyền** (gác cửa endpoint) và khác **Query Filter** (chỉ lọc tenant). Ba tầng riêng biệt |
| Buổi học (`BUOI_HOC`) | Một buổi cụ thể trong lịch của lớp, có `bat_dau`/`ket_thuc` tuyệt đối | Không có cột "ngày học" — ngày suy từ `TENANT.mui_gio` khi hiển thị |
| Trạng thái tự khai / chính thức | Hai cột riêng trong `DIEM_DANH` | `trang_thai_chinh_thuc` là **nguồn duy nhất cho mọi báo cáo**; tự khai chỉ là lời khai của học viên |
| Học phí áp dụng | `LOP_HOC_HOC_VIEN.hoc_phi_ap_dung` — snapshot lúc ghi danh | **Không** phải `LOP_HOC.hoc_phi`. Sửa học phí lớp không đổi hồi tố công nợ người đã đóng |
| Công nợ | Tính động `hoc_phi_ap_dung − SUM(so_tien)` | **Không có cột nào lưu nó.** Đừng thêm — xem [FR-14](../nghiep-vu/hoc-phi.md) |
| Bài tập vs Bài kiểm tra | Bài tập gắn **buổi học**, nộp nhiều lần; Bài kiểm tra gắn **lớp**, làm một lần | Hai bảng riêng và hai chức năng quyền riêng, vì ma trận quyền phân biệt chúng |
| Hệ thống con (HRM · CRM · LMS) | Cách **nhóm chức năng phân quyền** để lọc sidebar — `ChucNang.HeThongCua()` | **Không phải ba ứng dụng**: một API, một database, một lần đăng nhập. Không lưu trong DB — là thuộc tính của mã nguồn |
| Chức năng dùng chung | `TaiKhoan`, `PhanQuyen`, `ThietLapChung`, `Anh`, `DoiMatKhauNguoiKhac`, `NhatKyHeThong` | **Không thuộc hệ thống nào** và **không mở lối vào** hệ thống nào — người chỉ có chúng thì `/toi/he-thong` trả về rỗng |
| Nhân sự vs Học viên | Hai màn hồ sơ con người: nhân viên/giáo viên/trợ giảng ở **HRM**, học viên ở **LMS** | Vẫn **một bảng `NGUOI_DUNG`** — khác góc nhìn và khác quyền, không phải hai thực thể. Học viên là *khách*, không phải nhân sự |
| `trang_thai_nhan_su` vs `TAI_KHOAN.trang_thai` | Còn làm việc không (chặn phân công lớp mới) **vs** còn đăng nhập được không | Hai cột khác nhau hoàn toàn. Vô hiệu hoá tài khoản **không** đụng dữ liệu người dùng |
| `ICurrentUser.UserId` vs `.TaiKhoanId` | Khoá ngoại nghiệp vụ dùng `UserId` (con người); tra quyền dùng `TaiKhoanId` | Lẫn hai thứ này **trả rỗng một cách im lặng**, không có lỗi biên dịch. Đã gây 3 lỗi thật (07/09) |
| FR-xx | Mã chức năng (Functional Requirement) | Dùng tham chiếu chéo giữa tài liệu, commit, test, code review |
| v1/v2 (API) | Phiên bản hợp đồng API theo URL segment | Không nhầm với version ứng dụng (semver trong `CHANGELOG.md`) |
