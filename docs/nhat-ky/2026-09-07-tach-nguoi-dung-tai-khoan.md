# 07/09/2026 — Tách người dùng khỏi tài khoản (FR-03/FR-04)

Yêu cầu ban đầu: *"tài khoản chỉ gồm thông tin cơ bản để đăng nhập và được chọn gán cho người
dùng nào… khi người dùng không còn dùng hệ thống thì vô hiệu hoá tài khoản nhưng vẫn giữ dữ
liệu người dùng"*.

## Điều đáng nói nhất: mục tiêu đã đạt được từ trước, cái hỏng nằm chỗ khác

`VoHieuHoa` chỉ chặn ở hai chỗ (`DangNhapCommand:64`, `LamMoiTokenCommand:64`) — không xoá gì,
20 khoá ngoại còn nguyên, tên vẫn hiện đúng trong bảng điểm danh. Nghĩa là "giữ dữ liệu" đã
đúng rồi.

Cái thật sự hỏng là **một cột `TrangThai` gánh hai nghĩa**: "còn đăng nhập được" và "còn làm ở
trung tâm". Hệ quả cụ thể ở `LopHocDtos.cs:182` và `HocVienTrongLopDtos.cs:93` — cả hai đòi
`TrangThai == HoatDong`. Vô hiệu hoá tài khoản một giáo viên đã nghỉ thì **không phân công
được họ vào lớp cũ nữa**, tức không sửa nổi dữ liệu lịch sử.

Đã trình bày ba phương án kèm chi phí thật (phương án rẻ nhất chỉ thêm một cột, không đụng khoá
ngoại nào). Chủ dự án chọn tách ba bảng hồ sơ — nặng hơn nhưng mở đường cho thông tin đặc thù
từng vai trò.

## Quyết định: NGUOI_DUNG giữ nguyên tên và id

| | Trỏ về đâu |
|---|---|
| 12 khoá ngoại **nghiệp vụ** (`hoc_vien_id`, `giao_vien_chinh_id`, `nguoi_cham_id`…) | `NGUOI_DUNG` — **không đổi một dòng nào** |
| 3 khoá ngoại **đăng nhập** (`NGUOIDUNG_QUYEN`, `REFRESH_TOKEN`, `TOKEN_DATLAI_MATKHAU`) | `TAI_KHOAN` (mới) |

Phương án thay thế là đổi tên `NGUOI_DUNG` → `HO_SO_NGUOI` rồi tái dùng tên cũ cho bảng đăng
nhập. Loại vì tên `NGUOI_DUNG` sẽ **đổi nghĩa giữa chừng** — code cũ đọc lại sẽ hiểu sai, và
phải sửa 12 config khoá ngoại.

## Migration: EF tự sinh sẽ xoá sạch mật khẩu

`dotnet ef migrations add` cảnh báo "may result in the loss of data". Đọc kỹ thì nó định:

1. `DropColumn` `username`, `password_hash`, `phai_doi_mat_khau` → **11 tài khoản mất mật khẩu**
2. Tạo `TAI_KHOAN` **rỗng** → không ai đăng nhập được nữa
3. `RenameColumn nguoi_dung_id → tai_khoan_id` nhưng **giá trị vẫn là id người** → quyền trỏ sai

Viết tay lại theo thứ tự: tạo bảng → chuyển dữ liệu → đổi khoá ngoại → **rồi mới** xoá cột. Đã
`pg_dump` (124K) trước khi áp.

Một chi tiết may mắn có kiểm chứng: `RenameColumn trang_thai → trang_thai_nhan_su` giữ nguyên
giá trị, và hai enum trùng số (`HoatDong=0=DangLamViec`, `VoHieuHoa=1=DaNghi`) nên chép thẳng
là đúng nghĩa cả hai phía.

Kết quả trên DB thật: 11 người ↔ 11 tài khoản, hash mật khẩu còn nguyên, hồ sơ vai trò đúng số
(3 giáo viên/trợ giảng, 5 học viên, 3 nhân viên), 11 gán quyền không mất dòng nào. Đăng nhập
bằng mật khẩu cũ chạy ngay.

## Claim JWT: đổi nghĩa claim cũ là bẫy im lặng

`NameIdentifier` **giữ nguyên là id NGƯỜI**, thêm claim mới `tai_khoan_id`. Không làm ngược lại
vì 12 khoá ngoại nghiệp vụ trỏ tới `NGUOI_DUNG` và mọi handler đang so `currentUser.UserId` với
chúng — đổi nghĩa claim cũ sẽ làm mọi so sánh đó sai **mà không có lỗi biên dịch**, chỉ trả về
rỗng.

Token cũ không có claim mới → `ICurrentUser.TaiKhoanId` trả null, và ba chỗ dùng nó (đổi mật
khẩu, tự vô hiệu hoá, tự xoá) từ chối bằng lỗi rõ ràng thay vì đoán nhầm. Phiên cũ hết hạn
trong tối đa 60 phút.

## Lỗi tự bắt được: cổng quyền và hai tầng phạm vi

Sau khi `NGUOIDUNG_QUYEN` chuyển sang khoá theo tài khoản, ba chỗ tra quyền vẫn truyền id
người:

- `QuyenAuthorizationHandler` — cổng quyền của **toàn hệ thống**, không sửa thì mọi endpoint 403.
- `PhamViLopHoc.ThayMoiLop` và `PhamViHocPhi.ThayToanBoSo` — admin không thấy lớp và **không
  thấy sổ học phí nào**.

Cái thứ ba chỉ lộ ra khi 15 test học phí đỏ. Ranh giới rút ra, nay ghi vào `CLAUDE.md`:
**tra quyền dùng `TaiKhoanId`, khoá ngoại nghiệp vụ dùng `UserId`**.

## Vá luôn một lỗ hổng khảo sát chỉ ra

Việc lọc theo vai trò trước nay **chỉ nằm ở frontend** (`LopHoc.tsx:223/227/612`); backend chỉ
kiểm "tồn tại + đang hoạt động". Gọi API trực tiếp là gán được một **học viên** làm giáo viên
chính. Nay `KiemNhanSu` kiểm cả vai trò, có test canh.

Cùng đợt: `LopHoc.tsx` giờ gọi `/nguoi-dung?trangThaiNhanSu=DangLamViec` — người đã nghỉ không
còn hiện trong dropdown rồi mới bị backend từ chối.

## Kiểm chứng

- **227 test xanh** (54 unit + 173 integration), build 0 warning, frontend `tsc`+`build`+`oxlint` sạch.
- 7 test mới trong `TachNguoiDungTaiKhoanTests`, trong đó test trung tâm:
  `Vo_hieu_hoa_tai_khoan_van_phan_cong_duoc_vao_lop` — điều trước đây **không làm được**.
- `DongThoiTests` thêm 5 `InlineData` cho ràng buộc mới (quy tắc #8).
- **Chạy tay trên PostgreSQL thật:**

| Thử | Kết quả |
|---|---|
| Đăng nhập 3 tài khoản bằng mật khẩu **cũ** sau migration | ✅ |
| Tạo người + tài khoản một lượt gọi | ✅ |
| Tạo học viên **không** tài khoản (bé không cần đăng nhập) | ✅ |
| Vô hiệu hoá tài khoản → không đăng nhập được | ✅ `TAI_KHOAN_BI_VO_HIEU_HOA` |
| …nhưng hồ sơ, địa chỉ, bằng cấp còn nguyên | ✅ |
| …và lớp vẫn hiện đúng tên giáo viên | ✅ |

Dữ liệu thử đã dọn, DB về đúng 9 người dùng ban đầu.

## Còn nợ

- **N2 vẫn còn**: frontend chưa ẩn nút theo quyền — nay có thêm tab Người dùng cùng vấn đề.
- Chưa có màn gộp "xem một người và tài khoản của họ" — hiện phải chuyển tab.
- `LopHoc.tsx` vẫn giới hạn cứng `soDong: 200`; trung tâm trên 200 người sẽ mất người khỏi
  dropdown mà không báo gì.
