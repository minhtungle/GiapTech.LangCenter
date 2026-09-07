# 07/09/2026 — Xác nhận trước mọi thao tác + nhật ký hệ thống (FR-16)

Hai yêu cầu: hỏi xác nhận trước mọi thao tác thêm/sửa/xoá, và bổ sung module nhật ký ghi lịch
sử thao tác mọi module.

## Xác nhận: đánh đổi đã nêu và được chấp nhận

Tôi đã nêu rằng hỏi trước **mọi** thao tác — kể cả bấm Lưu trong form — có rủi ro: người dùng
bấm Đồng ý theo phản xạ, làm hộp thoại mất tác dụng đúng lúc cần nhất. Chủ dự án chọn phương án
này, nên tôi làm theo và ghi rõ đánh đổi vào chú thích của `HopXacNhan`.

Để giảm rủi ro đó, mọi lời văn nói **cụ thể đổi gì hoặc mất gì**:

- `Lưu thay đổi cho "IELTS 6.5"? Thông tin hiện tại sẽ bị ghi đè.`
- `Chốt buổi này? Người chưa điểm danh sẽ được ghi là Vắng, và sau khi chốt không sửa được giờ,
  không huỷ, không xoá buổi nữa.`
- `Gỡ "Học viên Số 1" khỏi lớp? Điểm danh và học phí đã ghi vẫn được giữ.`

Câu "Bạn có chắc không?" thì bị loại hẳn — nó là câu làm cho việc bấm theo phản xạ chắc chắn
xảy ra.

Dùng hook `useXacNhan()` chứ không state ở từng màn: 11 màn × nhiều thao tác = hàng chục cặp
`useState` + `<HopXacNhan>` gần giống hệt nhau. Một chỗ duy nhất thì lời văn, hành vi focus và
cách tô màu nhất quán.

Hai chi tiết nhỏ: hộp **đóng trước khi chạy hành động** (nếu hành động mở modal khác thì hai
lớp chồng nhau), và biểu tượng cảnh báo chỉ hiện với thao tác phá huỷ — hiện ở mọi hộp thì nó
mất nghĩa.

## Nhật ký: hai điểm cắm, một bản ghi

Chủ dự án chọn "cả hai" — behavior cho ngữ cảnh và ChangeTracker cho chi tiết. Tôi nêu trước
rằng ghi từng trường sẽ làm bảng log lớn hơn cả `DIEM_DANH`, và làm theo cách **gom batch**:
một bản ghi cho mỗi LỆNH, kèm `so_ban_ghi_anh_huong` và danh sách trường đổi (cắt 40 mục).

`NhatKyBehavior` cắm vào pipeline MediatR nên 46 lệnh hiện có và mọi lệnh thêm sau này **tự
động** được ghi. Nhận biết bằng quy ước tên `...Command` — thêm marker interface vào 46 lệnh là
46 chỗ có thể quên.

### Lỗi thứ nhất: ChangeTracker rỗng sau khi save

Bản đầu đọc `ChangeTracker` trong `GhiAsync`, tức **sau** khi handler đã `SaveChanges`. Chạy
thật thì nhật ký ghi `so_ban_ghi_anh_huong = 0` và `chi_tiet = null` dù vừa sửa một trường:
EF đã đặt `OriginalValue = CurrentValue` nên mọi so sánh cho kết quả bằng nhau.

Sửa bằng `ChanBatThayDoi` — một `SaveChangesInterceptor` chụp `ChangeTracker` **trước** khi EF
ghi, gom vào một bộ đếm scoped theo request. Sau khi vá, nhật ký ghi đúng:
`LOP_HOC.PhongHoc  P.999 → P.201`.

Hệ quả cho test: `ApiFactory` gọi `AddDbContext` thay hoàn toàn cấu hình của Infrastructure nên
**mất interceptor** — phải đăng ký lại, nếu không mọi test về nhật ký xanh sai.

### Lỗi thứ hai: mật khẩu lộ nguyên văn

Kiểm tay bằng cách tạo người dùng với mật khẩu `MAT-KHAU-RAT-BI-MAT` rồi tìm nó trong nhật ký:
**tìm thấy**.

`chi_tiet` lọc đúng (nó đọc ChangeTracker nên chỉ thấy `PasswordHash` đã băm), nhưng `tham_so`
là **command thô** — nó mang mật khẩu dạng chữ. Hai cột, hai nguồn, phải lọc ở hai chỗ.

Bản vá đầu chỉ che trường **cấp một**, mà `TaoNguoiDungCommand` mang mật khẩu **lồng** trong
khối `TaiKhoan`. Phải che đệ quy, đi vào cả mảng (`DanhSach` của lệnh ghi điểm danh là mảng
đối tượng).

Giá trị thay bằng `***` nhưng **giữ tên trường**: người đọc cần biết lệnh có mang mật khẩu để
hiểu đây là lệnh đổi mật khẩu.

Test cũ của tôi bỏ sót vì chỉ kiểm `chi_tiet`. Đã bổ sung kiểm cả hai cột và thêm một test
riêng cho lệnh đặt lại mật khẩu.

## Vài quyết định cố ý

- **Chỉ ghi lệnh GHI, không ghi truy vấn đọc.** Mỗi lần tải một trang danh sách là vài truy
  vấn; ghi hết sẽ làm bảng phình gấp hàng chục lần mà gần như không ai tra tới.
- **Đăng ký sau `ValidationBehavior`**: request sai định dạng là lỗi client, không phải thao
  tác nghiệp vụ. Nhưng lỗi **nghiệp vụ** thì có ghi — "ai đó đã cố xoá buổi đã chốt" là thông
  tin đáng lưu.
- **Lưu bản chụp `username`/`ho_ten`** thay vì join: tài khoản bị xoá hay đổi tên thì nhật ký
  vẫn đọc được. Nhật ký phụ thuộc dữ liệu hiện tại thì mất giá trị đúng lúc cần nhất.
- **`GhiAsync` tự bắt lỗi.** Người dùng đã thu học phí thành công không thể nhận 500 chỉ vì
  bảng nhật ký gặp vấn đề; lỗi đó ghi ra log kỹ thuật cho người vận hành.
- **Nhật ký không tự ghi về chính nó** — nếu không thì mỗi lần ghi lại sinh thêm một bản ghi.
- **Không có endpoint sửa/xoá nhật ký**, và có test canh việc không ai vô tình thêm chúng.
- IP lấy từ `X-Forwarded-For` trước `RemoteIpAddress`: hệ thống chạy sau Nginx nên không có nó
  thì mọi bản ghi mang IP của reverse proxy.

## Kiểm chứng

- **264 test xanh** (54 unit + 210 integration), build 0 warning, frontend sạch.
- `NhatKyHeThongTests` — 11 test, gồm: ghi đúng giá trị trước/sau (so khớp **đúng mục** `HoTen`
  chứ không tìm chuỗi con trong cả JSON), không ghi mật khẩu ở cả hai cột, truy vấn đọc không
  ghi, nhật ký không tự ghi về nó, giáo viên nhận 403, không có đường sửa/xoá, không lọt tenant.
- **Chạy thật**: admin xem được nhật ký (200), giáo viên và học viên bị chặn (403); bản ghi hiện
  đúng `LOP_HOC.PhongHoc P.999 → P.201`; mật khẩu hiện `"MatKhau":"***"`. Dữ liệu thử đã dọn,
  DB về 9 người dùng ban đầu.

## Còn nợ

- **N13 (mới)**: chưa có cơ chế dọn nhật ký cũ. Bảng tăng vô hạn — cần chính sách lưu giữ (ví
  dụ 12 tháng) trước khi chạy production lâu dài.
- Không ghi được thao tác **không đi qua MediatR**: `TenantSeeder` (tạo trung tâm mới) và
  `BoKhuyetQuyenQuanTri` (chạy lúc khởi động).
- ~~Chấm điểm bài nộp dùng `onBlur` nên nay hỏi xác nhận mỗi lần rời ô~~ — **đã sửa cùng
  ngày**: bảng chấm nay nhập cả bảng rồi bấm "Lưu điểm" một lần, và có thêm ô nhận xét (trước
  đây `nhanXet` không có đường nhập).
