# Thuật ngữ & quy ước tên gọi dễ nhầm lẫn

| Thuật ngữ | Ý nghĩa trong dự án này | Lưu ý tránh nhầm lẫn |
|---|---|---|
| Tenant | Một **trung tâm ngoại ngữ** độc lập, dữ liệu cách ly qua `tenant_id` | Không nhầm với "user" hay "organization" trong tài liệu bên thứ ba |
| Quyền (`QUYEN`) | **Nhóm quyền** tự định nghĩa theo chức năng + thao tác | Khác `[Authorize(Roles=...)]` cố định của ASP.NET Core — đây là hệ phân quyền **động** đọc từ DB |
| Vai trò (Giáo viên, Học viên…) | Chỉ là **nhóm quyền dựng sẵn** do seeder tạo, người dùng đổi được | **Không** phải enum cứng trong code. `LoaiNguoiDung` chỉ dùng lọc danh sách, không bao giờ dùng phân quyền |
| Phạm vi (`IPhamViLopHoc`, `IPhamViHocPhi`) | Lọc **dữ liệu** bên trong một tenant: "lớp mình dạy", "sổ thu của mình" | Khác với **quyền** (gác cửa endpoint) và khác **Query Filter** (chỉ lọc tenant). Ba tầng riêng biệt |
| Buổi học (`BUOI_HOC`) | Một buổi cụ thể trong lịch của lớp, có `bat_dau`/`ket_thuc` tuyệt đối | Không có cột "ngày học" — ngày suy từ `TENANT.mui_gio` khi hiển thị |
| Trạng thái tự khai / chính thức | Hai cột riêng trong `DIEM_DANH` | `trang_thai_chinh_thuc` là **nguồn duy nhất cho mọi báo cáo**; tự khai chỉ là lời khai của học viên |
| Ba trường "ai phụ trách khách" | `KHACH_HANG.nguoi_tao_id` (ai TẠO hồ sơ — cố định) · `LICH_SU_CHAM_SOC.nguoi_phu_trach_id` (ai đang chăm — đổi theo thời gian) · `YEU_CAU_XEP_LOP.nguoi_gui_id` (ai đẩy sang đào tạo) | **Ba câu hỏi khác nhau**, đừng dùng thay nhau. "Nhân viên kinh doanh" trong màn Học viên của lớp lấy từ cái ĐẦU TIÊN |
| **Khoá học** (`KHOA_HOC`) vs **Lớp học** (`LOP_HOC`) | `KHOA_HOC` là **mặt hàng** CRM bán ra ("IELTS 6.5 cấp tốc", có giá và số buổi niêm yết). `LOP_HOC` là **một lần mở cụ thể** ở LMS (có giáo viên, phòng, lịch, danh sách học viên) | Một khoá mở được nhiều lớp; một lớp dạy **tối đa 3 khoá** (`LOP_HOC_KHOA_HOC`, 12/09/2026). Đơn CRM trỏ `KHOA_HOC`, ghi danh trỏ `LOP_HOC` — duyệt lệch hai thứ này thì hệ thống **cảnh báo**, không chặn |
| Trạng thái tham gia lớp | Suy động từ `LOP_HOC_HOC_VIEN` mỗi lần đọc | **Không** suy từ `YEU_CAU_XEP_LOP.trang_thai`: yêu cầu `DaXep` là *sự kiện quá khứ* ("đã từng được duyệt"), còn "đang học lớp nào" là *trạng thái hiện tại*. Gỡ khỏi lớp không đổi yêu cầu |
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
