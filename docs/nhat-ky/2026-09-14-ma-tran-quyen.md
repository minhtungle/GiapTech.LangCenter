# 2026-09-14 — Ma trận quyền: 31 ô bật cũng không làm gì

Yêu cầu của chủ sản phẩm: *"tôi cần đầy đủ danh sách chức năng kèm thao tác thực tế trong chức
năng đó chứ không chỉ thêm sửa xóa xem cơ bản"*. Nghe như một việc thêm cột. Hoá ra là việc dọn
một thứ đã hỏng âm thầm từ lâu.

## Đếm trước, sửa sau

Trước khi đề xuất gì, tôi đếm xem ma trận hiện tại có bao nhiêu ô **thật sự** được đọc: lấy
mọi cặp `[RequirePermission(ChucNang.X, HanhDong.Y)]` trong `Controllers/`, so với 27 chức
năng × 4 thao tác đang hiện trên màn.

**31 trên 108 ô không có endpoint nào đọc.** `NhatKyHeThong.Xoa` — nhật ký chỉ ghi thêm, không
có đường xoá. `ChucVu.Xem` — màn chức vụ đọc qua `NhanSu.Xem`. `HocOnline.Sua` — học viên
không sửa bài giảng.

Con số đó mới là vấn đề thật, và nó giải thích vì sao yêu cầu xuất hiện: người cấu hình quyền
không có cách nào phân biệt ô có tác dụng với ô không, nên **tick bừa cho chắc**. Một ma trận
mà cách dùng hợp lý nhất là tick hết thì không còn là phân quyền.

## Bốn ô CRUD không đủ để diễn đạt nghiệp vụ

Chiều ngược lại cũng hỏng. "Chốt buổi học" và "sửa điểm danh" đều phải mượn `DiemDanh.Sua` —
nên **không tách được** quyền giáo viên chính với trợ giảng, dù đó chính xác là khác biệt cần
phân quyền: trợ giảng ghi điểm danh được, nhưng chốt buổi sinh ra "Vắng mặc định" cho mọi
người chưa khai, đó là quyết định của giáo viên chính.

Thêm 14 thao tác đặc thù, đánh số **từ 10 trở lên**. Số 0–3 giữ nguyên vì
`QUYEN_CHUC_NANG.hanh_dong` lưu **số nguyên**: đổi `Sua` từ 2 thành 3 là âm thầm biến quyền
"Sửa" của mọi nhóm trong mọi trung tâm thành "Xoá". Có `[Theory]` khoá cứng bốn giá trị đó.

## Lỗi rò rỉ tìm thấy giữa đường

Khi rà `NhanXetBuoiHoc` để quyết định nó cần thao tác nào, tôi đọc handler và thấy: quyền `Xem`
ở đây có nghĩa **"đọc nhận xét của MỌI người trong buổi"**. Mà nhóm Học viên mặc định đang được
cấp đúng quyền đó.

Tức là học viên đọc được phản hồi riêng của bạn cùng lớp.

Sửa: học viên chỉ giữ `TuLam` (gửi và đọc lại nhận xét của chính mình). Endpoint `GET
/buoi-hoc/{id}/nhan-xet` gác bằng **`TuLam`** — thao tác hẹp nhất, ai cũng đọc được nhận xét
của mình — rồi handler mới đọc thêm `Xem` để quyết định trả về của mọi người hay chỉ của mình.

Gác endpoint bằng `Xem` thì chặn oan học viên đọc nhận xét chính họ; `[RequirePermission]`
**chỉ nhận một quyền**, không có ngữ nghĩa OR. Đây là cặp `Xem`/`TuLam` — chỗ dễ cấp sai nhất
trong toàn bộ ma trận, nay có bảng riêng trong tài liệu.

## Một lần tách hụt

Tôi đã tách `KhoaHoc.Sua` thành `CauHinhTien` riêng, lý do nghe rất xuôi: giá là dữ liệu tiền,
phải tách. Rồi mở `LuuKhoaHocCommand` ra đọc — nó ghi **tên, ghi chú và giá trong cùng một
lệnh**. Attribute gác ở endpoint không có cách nào tách hai việc đó.

Hoàn lại. Muốn tách thật thì phải tách lệnh trước. Khai một ô không gác được gì chính là lỗi
tôi đang đi dọn.

## Chốt chặn: bốn cái, và một cái tự kiểm

Việc này chỉ có nghĩa nếu lần sau không lặp lại, nên phần lớn công sức đổ vào chốt chặn hai
chiều — cùng khuôn với sáu test kiến trúc đã có:

| Canh gì | Hỏng thì sao |
|---|---|
| Endpoint gác bằng quyền **chưa khai** | Ô không hiện ⇒ không ai cấp được ⇒ 403 cho **cả quản trị** |
| Quyền **đã khai** mà không endpoint nào dùng | Ô chết quay lại, tích tụ dần |
| Nhóm mặc định cấp ô màn phân quyền không hiện | Hàng trong DB mà quản trị **không thấy, không bỏ được** |
| Thiếu nhãn tiếng Việt hoặc thiếu giá trị trong kiểu TS | Người dùng thấy `GhiDanhKhoaOnline` giữa bảng tiếng Việt |

Cái thứ ba phải đọc `NhomQuyenMacDinh.cs` bằng **phân tích mã nguồn** chứ không tham chiếu
assembly — `Application.UnitTests` không được phụ thuộc `Infrastructure` (quy tắc #10).

Test đọc mã nguồn bằng regex có một kiểu hỏng riêng: regex sai thì nó đọc được **0 mục** và
xanh một cách vô nghĩa. Nên nó tự kiểm trước: *bắt được ít hơn 5 chức năng = regex hỏng, đỏ
ngay*. Tôi thử đột biến đúng chỗ đó và nó bắt được.

## Ảnh chụp màn hình, lần nữa

`tsc` sạch, test xanh, endpoint trả đúng dữ liệu. Mở ảnh chụp ra thì bảng **tràn khỏi modal**,
cột cuối bị cắt, và nhãn cột dài gói thành bốn dòng.

Sửa lần một: xoay nhãn bằng `-rotate-90`. Chụp lại — nhãn còn đúng ba ký tự: "Xen", "Thê",
"Sửa". `transform` xoay hình ảnh nhưng **không đổi ô mà phần tử chiếm**, nên chữ tràn ra rồi
bị cắt. Đổi sang `writing-mode: vertical-rl` — nó đổi cả hộp nên trình duyệt tự tính đúng
chiều cao. Lần này kiểm bằng code (`scrollHeight > clientHeight`) chứ không bằng mắt: 0 nhãn
bị cắt, trên cả ba tab.

## Chốt chặn của tôi nói dối tôi

Test "ô chết" báo `KhoaOnline.Xem` không endpoint nào dùng. Tôi tin nó và xoá.

Nó nói đúng phần nó biết: test đó quét `[RequirePermission]` trong `Controllers/`, và
`KhoaOnline.Xem` thật sự không xuất hiện ở đó. Cái nó không biết là `PhamViKhoaOnline.LocKhoa`
đọc quyền ấy để phân biệt **người SOẠN nội dung** (thấy mọi khoá, kể cả bản nháp) với **người
HỌC** (chỉ thấy khoá mình được ghi danh).

Xoá xong thì không ai là người soạn nữa. Tạo một khoá mới → khoá **biến mất khỏi màn của chính
người vừa tạo**. Không 403, không lỗi, không dòng log nào: dữ liệu chỉ đơn giản không có trong
danh sách.

489 test backend xanh hết. Bắt được nó là **một test E2E**, thứ duy nhất đi qua cả tầng phạm vi
lẫn giao diện.

Bài học không phải "đừng tin test" mà cụ thể hơn: **quyền phạm vi trông y hệt ô chết** với bất
kỳ công cụ nào chỉ đọc attribute. Nên viết thêm một test quét ngược từ mã nguồn tầng phạm vi —
mọi `ChucNang.X` xuất hiện trong lời gọi `CoQuyenAsync` phải còn thao tác trong bảng khai. Tôi
tiêm lại đúng đột biến vừa mắc: nó đỏ.

Và vì test này cũng đọc mã nguồn bằng regex, nó mang cùng kiểu hỏng như test trước — quét ra 0
chỗ thì xanh vô nghĩa. Nên nó cũng tự kiểm trước, và tôi cũng thử đột biến đúng chỗ đó.

## Dọn dữ liệu cũ: hỏi trước khi xoá

Sau khi ma trận thu còn 106 ô, các trung tâm đang có vẫn giữ hàng cho những ô đã bỏ — vô hại
(không endpoint nào đọc) nhưng làm màn phân quyền cảnh báo "còn giữ quyền cũ" mãi.

`BoKhuyetQuyenQuanTri` chạy lúc khởi động và **cố ý chỉ thêm, không xoá**. Tôi không đổi hành
vi đó: xoá hàng quyền là động vào dữ liệu đang có (quy tắc #1). Viết thành
`scripts/don-quyen-o-chet.sql` chạy tay, có phần in ra **những gì sắp xoá** trước khi xoá.

Sao lưu, dựng một database bản sao, chạy thử trên đó: 211 → 154 hàng, nhóm quản trị vẫn **đúng
106 ô** — không mất khả năng nào. Rồi mới chạy trên DB thật, và khởi động lại API để chắc chắn
seeder không cấp lại những hàng vừa xoá. Nó không cấp lại.

## Số cuối ngày

540 ô nếu hiện đủ 18 thao tác × 30 chức năng → **106 ô thật sự có tác dụng**. 33 chức năng
(30 có API + 3 giữ chỗ), 18 thao tác. 490 test backend + 24 E2E xanh.

Ba thứ bắt lỗi hôm nay, không cái nào là mắt tôi đọc code: một phép đếm (31/108 ô chết), một
ảnh chụp màn hình (bảng tràn, nhãn cắt còn 3 ký tự), và một test E2E (quyền phạm vi bị xoá
nhầm).
