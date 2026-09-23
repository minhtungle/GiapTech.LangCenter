# 16–21/08/2026 — Bài học từ dự án tiền thân

Repo này tách ra từ **`GiapTech.SoccerRoom`** (quản lý CLB đá bóng phong trào) ngày 05/09/2026:
giữ toàn bộ tầng hệ thống, bỏ hết nghiệp vụ. Sáu nhật ký ngày 16–21/08 mô tả nghiệp vụ đó — sàn
đối thủ, quỹ CLB, đăng ký đá trận qua link — **đã gỡ 08/09/2026** vì không còn liên quan tới dự
án này và gây hiểu nhầm cho người đọc tài liệu. Nội dung đầy đủ còn trong git history.

File này giữ lại **những bài học vẫn còn hiệu lực nguyên vẹn**, vì code hiện tại còn dẫn chiếu
tới chúng.

## 16/08 — Sự cố mất dữ liệu, và quy tắc #1 ra đời

Người dùng báo *"mỗi lần sửa thì dữ liệu bị thay đổi"*. Bắt request PUT từ trình duyệt: form gửi
`diaChi: null` dù bản ghi đang có địa chỉ.

Nguyên nhân: **form sửa không có ô địa chỉ** (bê nguyên form tạo, mà form tạo cũng thiếu), nên
khi gửi thì gán cứng `diaChi: null` — ghi đè lên dữ liệu người dùng chưa từng đụng tới.

Đây là loại lỗi tệ nhất: **không báo lỗi, không log, chỉ âm thầm xoá dữ liệu mỗi lần lưu.**

**Vì sao lọt lưới:** test cũ chỉ kiểm *trường vừa đổi có đúng không*, không kiểm *trường KHÔNG
đổi có còn nguyên không*.

Hệ quả còn hiệu lực hôm nay:

- **Quy tắc bất di bất dịch #1** trong [CLAUDE.md](../../CLAUDE.md).
- `CapNhatKhongMatDuLieuTests` — trong đó có test đối chiếu DTO trả về với danh sách trường mà
  lệnh cập nhật ghi đè, bắt được ngay khi ai đó thêm trường vào command mà quên thêm vào DTO.
- Quy ước **`null` = không gửi → giữ nguyên; chuỗi rỗng = chủ động xoá**, áp cho mọi trường tuỳ
  chọn. Xem `ThietLapDtos.cs`, `LopHocDtos.cs`, `DiemDanhDtos.cs`.

Lỗi này đã lặp lại **hai lần nữa** sau đó (05/09 với mô tả trung tâm, 07/09 với cột `nhan_xet`) —
lần nào cũng bị test bắt, nên cơ chế canh đang hoạt động đúng.

## 21/08 — Rate limit: con số phải kiểm được, không chỉ cái tên

Thêm giới hạn tần suất cho endpoint ẩn danh. Bài học nằm ở **phản chứng khi viết test**: test chỉ
so *tên policy* thì đổi hạn mức từ 10 thành 3000 mà 5/5 test vẫn xanh.

Hệ quả còn hiệu lực: hạn mức là **hằng số công khai** (`GioiHanTanSuat.HanMucTraCuu`,
`HanMucXacThuc`) để test đọc và so được **con số thật**, không chỉ tên.

Cũng từ đợt này: `TestServer` không có kết nối TCP thật nên `RemoteIpAddress` là null và **mọi
test rơi vào chung một phân vùng** — 112 test đỏ vì nhận `QUA_NHIEU_YEU_CAU` thay vì dữ liệu. Nên
có cờ cấu hình `GIOI_HAN_TAN_SUAT` để tắt theo từng factory, chứ không `#if DEBUG`.

## 21/08 — Chốt "còn người quản trị cuối cùng"

Phát hiện khi viết test: người quản trị duy nhất có thể tự thu quyền của chính mình, và trung tâm
mất đường quản trị hoàn toàn. Nay chặn bởi `ChotConNguoiQuanTri.cs`.

Điều đáng nhớ về cách nhận ra "người quản trị": suy từ **dữ liệu quyền**
(`ChucNang.PhanQuyen`), **không** từ tên nhóm quyền — tên là chuỗi người dùng sửa được, đổi
"Quản trị viên" thành "Ban giám hiệu" không được phép làm mất quyền quản trị. Cùng nguyên tắc với
`ChucNang.LopHocToanTrungTam` dùng để nhận ra ai thấy được mọi lớp.

> ⚠️ Các mã nợ **N3/N4/N9** xuất hiện trong nhật ký cũ thuộc **bảng nợ của dự án tiền thân**,
> đánh số khác bảng hiện tại trong [`ke-hoach.md`](../01-tong-quan/ke-hoach.md). Đừng đối chiếu chúng.
