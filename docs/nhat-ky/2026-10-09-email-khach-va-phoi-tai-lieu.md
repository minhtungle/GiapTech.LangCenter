# 2026-10-08 → 09

Bốn đợt việc, chung một trục: **đưa nội dung ra khỏi hệ thống** — gửi cho khách, in ra giấy.

## Đã làm

1. **Tab Email ở chi tiết khách hàng** — soạn thư, gửi, xem lịch sử. Bảng `LICH_SU_EMAIL`.
2. **Sửa tên đăng nhập + 4 bộ lọc** cho màn Tài khoản.
3. **Thiết lập file** (`PHOI_TAI_LIEU`) — phôi .docx có biến `{{key}}`, điền giá trị, xuất bản
   in, xem trước ngay trong hệ thống, đính kèm vào mail.
4. **Trình soạn email đầy đủ** — 6 nút thành 27; và **mật khẩu mặc định** cho tài khoản mới.

## Quyết định

### Word CẮT RỜI `{{key}}` thành nhiều `<w:r>`

Đây là phát hiện quyết định cả thiết kế. Giải nén phôi thật trước khi viết code:
`{{hoc_vien-so_dien_thoai}}` nằm trong **ba** `<w:r>` riêng vì Word chèn `<w:proofErr>` vào
giữa. Đo được: **0 key** tìm thấy trên XML thô, **4 key** sau khi gỡ thẻ.

Nghĩa là `xml.Replace("{{key}}", giaTri)` không khớp gì cả. Cách làm: trong mỗi `<w:p>`, nối
nội dung mọi `<w:t>`, thay biến trên chuỗi đã nối, rồi đặt kết quả vào `<w:t>` đầu tiên.

**Không thêm thư viện nào.** Chứng minh `System.IO.Compression` + regex làm được, và
`textutil` của macOS mở được file sinh ra. `DocumentFormat.OpenXml` 400 KB cho việc một file
200 dòng làm được là trả giá bảo trì không đổi lấy gì.

### Preview: Mammoth.js, chạy ở trình duyệt

Chủ sản phẩm chỉ tới `GiapTech.BindingDocx` — dự án của chính anh, trong `PROMPT.md` đã chốt
Mammoth.js. Dùng đúng thứ đó thay vì tự viết bộ chuyển docx→HTML bằng regex (kém hơn nhiều).

Chuyển ở client: server không giữ HTML tài liệu trong bộ nhớ, phôi hỏng chỉ ảnh hưởng một tab.
Nạp bằng `import()` động — đo bằng cách bỏ ra rồi build lại: gói chính **2433 KB cả hai
trường hợp**, nên 1,9 MB của Mammoth thật sự nằm ngoài.

Preview KHÔNG giống hệt Word (mất font, căn lề, ngắt trang). Nói trước bằng dòng cố định dưới
khung thay vì để người dùng tự phát hiện.

### Mật khẩu mặc định: MÃ HOÁ, không băm

Mật khẩu tài khoản thì băm. Nhưng đây là **giá trị điền sẵn vào form** mà admin phải đọc lại
được để đọc cho người dùng mới — băm sẽ làm trường này vô dụng.

Đánh đổi đã chấp nhận: ai lấy được cả DB lẫn khoá mã hoá thì đọc được. Nhưng nó chỉ là mật
khẩu TẠM, mọi tài khoản tạo mới đều bật `phai_doi_mat_khau`.

### Trình soạn email: tiêu chí là "Outlook dựng được không"

Thanh công cụ ít nút trước đây là chủ ý, có ghi lý do. Nên khi mở rộng, mỗi thứ thêm vào đều
phải qua tiêu chí đó — và **kiểm bằng test**, không bằng suy luận: 8 test chạy HTML y hệt
Tiptap sinh ra qua chính `ChuanBiHtml`, khẳng định màu chữ, căn lề, gạch chân, cỡ chữ, bảng
sống sót qua PreMailer.

Vẫn không thêm: màu tự do (chữ xám nhạt trên nền tối là không đọc được, người soạn không bao
giờ thấy), cỡ chữ tuỳ ý, ảnh (chủ sản phẩm chốt dùng đính kèm tệp).

## Vướng mắc & phát hiện

### Stream dùng hai lần: tệp lưu lên kho THIẾU ĐẦU

`TaoPhoiHandler` gọi `DocKey(stream)` rồi `TaiLen(stream)` trên cùng một stream của HTTP
request. `ZipArchive` seek khắp tệp, nên `TaiLen` đọc tiếp từ vị trí còn sót: phôi
**4.476.206 byte lưu thành 4.457.755 byte**.

Điều đáng nói là lỗi **không lộ lúc tải lên** mà lộ lúc xuất file, với thông báo chẳng liên
quan gì tới nguyên nhân: *"Offset to Central Directory cannot be held in an Int64"*. Sửa bằng
cách đọc vào bộ nhớ một lần, và viết test hồi quy canh đúng điều kiện đó.

### Giao diện hứa một đằng, backend làm một nẻo

Modal xuất file gửi ô trống là `""`. Backend coi chuỗi rỗng là **giá trị thật** và thay vào,
trong khi dòng cảnh báo ngay bên trên nói "sẽ giữ nguyên chuỗi đó". Sửa frontend gửi `null`.

Loại lỗi này không test nào bắt được nếu chỉ test backend — phải chạy thật qua giao diện rồi
mở file ra xem.

### `display: contents` ghi đè thuộc tính `hidden`

Chia Thiết lập chung thành ba tab, dùng `<div hidden={...} className="contents">`. Tab Đăng
nhập hiện nguyên cả nội dung tab Trung tâm: nhóm cần `display: contents` để ô con nhận grid
của `<form>`, mà `display: contents` **thắng** `display: none` của thuộc tính `hidden`.

`tsc`, lint và test đều xanh. Chỉ chụp màn mới thấy.

### Tab ẩn KHÔNG được gỡ khỏi DOM

Trang Thiết lập dùng một `<form>` và dựng dữ liệu từ `FormData`. Chia tab theo kiểu thường
thấy sẽ làm **lưu tab Trung tâm xoá sạch thông tin chuyển khoản** — lỗi 16/08/2026 đến từ
hướng thứ ba: ô vẫn trên màn nhưng không còn trong DOM.

### Bộ chuyển hệ thống con nhấp nháy — chẩn đoán đầu của tôi SAI

Tôi nói lỗi do `useHeThong()` trả object mới mỗi render. Sửa theo hướng đó, đo lại: **vẫn 3
lần ghi**. Đổi thứ tự `navigate`/`doi`: vẫn 3 lần.

Chỉ khi lấy **stack trace** của từng lần ghi mới ra thủ phạm: `navigate()` không đổi
`location` ngay, nên effect "URL thắng" chạy ở giữa và ghi đè ngược lựa chọn vừa bấm. Sửa
bằng một `useRef` đánh dấu "người dùng đang tự chuyển".

Bài học: đo trước, đừng sửa theo giả thuyết đầu tiên nghe hợp lý.

### 174 tài khoản không còn nick `admin`

Đăng nhập thất bại, tôi tưởng API trỏ nhầm DB. Truy ra: chính tôi đã đổi đuôi cho cả 174 tài
khoản khi nạp dữ liệu VIETGEN — tên đúng là `admin@vietgeneducation.edu.vn`.

## Thư báo tài khoản khi tạo người dùng

Mẫu `ChaoMungHocVien` có từ 30/09 nhưng **chưa nơi nào gọi** — enum ghi "đã có chỗ gọi" và
`TuGuiDuoc()` trả `true`, tức giao diện vẫn nói với người dùng là mẫu này tự gửi. Nay nối thật.

### Mặc định TẮT, và vì sao điều đó quan trọng hơn vẻ ngoài của nó

Gửi email là hành động **không rút lại được**: mật khẩu tạm đã nằm trong hộp thư người ta rồi.
Nên ô tích mặc định không chọn, và cờ ở DTO mặc định `false` — client cũ với `TenantSeeder`
tạo `admin` không vô tình gửi thư cho ai.

Test canh điều này (`Khong_tich_chon_thi_khong_gui_du_co_email`) là test quan trọng nhất của
đợt. Kiểm bằng đột biến: lật mặc định thành `true` ⇒ đỏ đúng test đó.

### Vì sao gửi trong handler chứ không làm nút "gửi lại"

Thư mang **mật khẩu dạng rõ**, mà hệ thống chỉ lưu hash. Qua khỏi lệnh tạo là không ai đọc lại
được nữa — muốn gửi lại thì phải đặt lại mật khẩu trước. Nên hoặc gửi ngay trong handler, hoặc
không gửi được.

### Lỗi gửi không được làm hỏng lệnh

Người và tài khoản đã `SaveChanges` xong. Ném lỗi ở đây trả 500 cho một lệnh **đã thành công**,
và người tạo sẽ bấm Lưu lần nữa — lần này nhận `USERNAME_DA_TON_TAI` và tưởng mình làm sai gì
đó. Nuốt lỗi, ghi log. Cùng lựa chọn với `GuiLienHeHandler`, cùng lý do.

### Một service chung cho hai đường tạo

Có hai đường: tạo người kèm tài khoản, và cấp tài khoản cho người đã có hồ sơ. Viết logic hai
lần thì hai bản sẽ trôi khỏi nhau — sửa nội dung thư ở một chỗ, chỗ kia vẫn gửi bản cũ mà
không ai biết. Nên tách `IThuChaoMung`, cùng lý do `IMauEmail` đã nêu cho chính nó.

Lần đầu tôi viết thẳng vào `TaoNguoiDungHandler` rồi mới nhận ra đường thứ hai cũng cần — gỡ
ra thành service trước khi commit.

### Tên đăng nhập trong thư phải là tên ĐÃ GHÉP ĐUÔI

Trung tâm khai đuôi thì DB lưu `ten@duoi` và đăng nhập phải gõ đủ. Gửi tên thô là gửi một tên
đăng nhập **không tồn tại**: người nhận thử mãi không vào được, không hiểu vì sao, và không ai
nghĩ ra để kiểm tra lá thư. Đột biến kiểm: đổi sang gửi `request.TaiKhoan.Username` ⇒ đỏ.

## Việc kế tiếp

- Gửi mail HÀNG LOẠT từ màn danh sách khách: backend đã nhận `khachHangIds` nhiều phần tử
  (trần 100), thiếu UI tick chọn.
- Hai test `NhanXetBuoiHocTests` hỏng sẵn trên `main` — đã xác nhận không phải do các thay đổi
  này (kiểm bằng cách stash hết rồi chạy lại). Chưa đụng vào.
- `frontend/edu-temp/` (60 MB, chưa theo dõi) vẫn chờ quyết định về giấy phép.
