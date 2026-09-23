# Module Nhật ký hệ thống (FR-16)

Ghi nhận lịch sử thao tác của mọi module: ai làm gì, lúc nào, đổi từ giá trị gì sang gì.

## FR-16 — Nhật ký thao tác

### Ghi tự động ở pipeline, không rải trong handler

`NhatKyBehavior` cắm vào MediatR pipeline nên **mọi lệnh đi qua MediatR đều được ghi**, kể cả
lệnh thêm sau này. Rải lời gọi trong từng handler thì chỉ cần một người quên là mất vết — mà
mất vết chỉ phát hiện được lúc cần tra, tức là quá muộn.

Nhận biết lệnh bằng quy ước tên `...Command`, không bằng marker interface: thêm interface vào
46 lệnh là 46 chỗ có thể quên, còn quy ước tên đã được cả dự án tuân thủ.

**Chỉ ghi lệnh GHI, không ghi truy vấn đọc.** Ghi mọi lượt xem sẽ làm bảng phình gấp hàng chục
lần mà gần như không ai tra tới; mỗi lần tải một trang danh sách là vài truy vấn.

Đăng ký **sau** `ValidationBehavior` nên request sai định dạng không vào nhật ký — đó là lỗi
client, không phải thao tác nghiệp vụ. Nhưng lỗi **nghiệp vụ** thì có ghi: "ai đó đã cố xoá
buổi đã chốt" là thông tin đáng lưu.

### Một bản ghi cho mỗi LỆNH, không phải mỗi dòng dữ liệu

Đây là quyết định quan trọng nhất về kích thước. `GhiDiemDanhCommand` ghi 20–30 dòng điểm danh
một lần, `SinhLichChoLop` ghi 24 buổi. Nếu log một dòng cho mỗi dòng dữ liệu thì bảng này lớn
hơn cả `DIEM_DANH` — bảng tăng nhanh nhất hệ thống.

Thay vào đó gom lại: `so_ban_ghi_anh_huong` nói có bao nhiêu dòng đổi, `chi_tiet` nói những
trường nào (cắt ở 40 mục).

### Giá trị trước/sau lấy từ interceptor, không từ ChangeTracker sau khi save

`ChanBatThayDoi` là một `SaveChangesInterceptor` chụp `ChangeTracker` **TRƯỚC** khi EF ghi
xuống DB.

Bản đầu tiên đọc `ChangeTracker` **sau** `SaveChanges` và trả về rỗng: EF đã đặt
`OriginalValue = CurrentValue`, nên mọi so sánh cho kết quả bằng nhau. Phát hiện khi chạy thật
— nhật ký ghi `so_ban_ghi_anh_huong = 0` và `chi_tiet = null` dù vừa sửa một trường.

**Test dùng InMemory DB phải đăng ký lại interceptor**: `ApiFactory` gọi `AddDbContext` thay
hoàn toàn cấu hình của Infrastructure, nên interceptor bị mất và test về nhật ký sẽ xanh sai.

### Không ghi thông tin nhạy cảm

Mật khẩu, token, hash bị che ở **hai chỗ khác nhau** vì hai cột có nguồn khác nhau:

| Cột | Nguồn | Nơi lọc |
|---|---|---|
| `chi_tiet` | ChangeTracker (đã băm) | `ChanBatThayDoi` |
| `tham_so` | Command **thô** — mang mật khẩu dạng chữ | `NhatKyBehavior` |

Lọc phải **đệ quy vào đối tượng lồng**: `TaoNguoiDungCommand` mang mật khẩu trong khối
`TaiKhoan` bên trong. Kiểm tay đã bắt được đúng ca này — bản vá đầu chỉ che trường cấp một nên
mật khẩu vẫn lộ nguyên văn.

Giá trị bị thay bằng `***` nhưng **giữ tên trường**: người đọc nhật ký cần biết lệnh có mang
mật khẩu (để hiểu đây là lệnh đổi mật khẩu) mà không thấy nội dung.

### Chỉ ghi thêm

**Không có endpoint sửa hay xoá.** Nhật ký sửa được thì không còn là nhật ký. Bảng cũng không
có cột nào để đánh dấu đã xoá.

`ChucNang.NhatKyHeThong` chỉ `Xem` có nghĩa thực chất — seeder cấp cả 4 hành động cho nhóm
quản trị theo vòng lặp chung, nhưng không có đường nào dùng `Sua`/`Xoa`.

### Quy tắc

- **Lưu bản chụp `username` và `ho_ten`**, không join. Tài khoản bị xoá hay đổi tên thì nhật ký
  vẫn đọc được — nhật ký phụ thuộc dữ liệu hiện tại thì mất giá trị đúng lúc cần nhất.
- **Ghi id NGƯỜI** (`NGUOI_DUNG.id`), không phải id tài khoản: người sống lâu hơn tài khoản.
- Khoá ngoại `SetNull`: `Restrict` sẽ khoá cứng mọi tài khoản vĩnh viễn vì ai cũng có vết.
- **Nhật ký hỏng không được làm hỏng nghiệp vụ.** `GhiAsync` tự bắt lỗi; người dùng đã thu học
  phí thành công không thể nhận 500 chỉ vì bảng nhật ký gặp vấn đề. Lỗi đó ghi ra log kỹ thuật.
- **Nhật ký không tự ghi về chính nó** — nếu không thì mỗi lần ghi lại sinh thêm một bản ghi.
- IP lấy từ `X-Forwarded-For` trước `RemoteIpAddress`: hệ thống chạy sau Nginx (ADR-0004), không
  có nó thì mọi bản ghi đều mang IP của reverse proxy.

### Ai xem được

| Vai trò | Xem nhật ký |
|---|---|
| Quản trị viên (có `NhatKyHeThong.Xem`) | Toàn bộ trung tâm |
| Giáo viên / trợ giảng / học viên | **Không** (403) |

Không cần tầng phạm vi riêng: chức năng này chỉ nhóm quản trị có, và họ được xem toàn bộ.
Query Filter lo phần cách ly giữa các trung tâm.

### Chưa làm

- **Chưa có cơ chế dọn nhật ký cũ.** Bảng sẽ tăng vô hạn; cần chính sách lưu giữ (ví dụ giữ
  12 tháng) trước khi chạy production lâu dài.
- Không ghi được thao tác **không đi qua MediatR**: `TenantSeeder` (tạo trung tâm mới) và
  `BoKhuyetQuyenQuanTri` (chạy lúc khởi động).
- Chưa xuất được nhật ký ra tệp để lưu trữ ngoài.
