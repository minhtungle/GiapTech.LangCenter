# 30/09/2026 — FR-31: gửi email theo trung tâm và mẫu nội dung

> Yêu cầu: *"tại quản trị hệ thống, tạo thêm module cho phép thiết lập email để gửi trong hệ
> thống, lưu trữ các mẫu email có sẵn"*.

Trước hôm nay hệ thống chỉ có **một** cấu hình SMTP cho cả VPS, đặt trong biến môi trường.
Email quên mật khẩu của mọi trung tâm đi từ cùng một địa chỉ. Với sản phẩm bán cho nhiều
trung tâm thì đó là vấn đề thật: học viên nhận thư từ một tên miền lạ sẽ nghi lừa đảo, và
trung tâm không có cách nào dùng tên miền của chính họ.

## Quyết định: mã hoá mật khẩu, khoá để ngoài DB

Chi tiết ở [ADR-0010](../02-kien-truc/adr/0010-cau-hinh-email-theo-tenant.md). Ba điểm đáng
nhắc lại vì chúng là chỗ dễ làm sai:

**Khoá ở biến môi trường, không trong DB.** Khoá nằm cùng chỗ với dữ liệu nó bảo vệ thì mã
hoá chỉ là thủ tục — ai lấy được bản backup thì lấy luôn khoá. Để ngoài nghĩa là một bản
backup rò ra ngoài vẫn không mở được hộp thư của các trung tâm.

**AES-GCM chứ không AES-CBC.** GCM là AEAD: nó tự kiểm toàn vẹn, sửa một byte trong bản mã
sẽ bị phát hiện lúc giải mã. CBC thì vẫn giải ra *một cái gì đó* và nơi gọi không biết mình
đang dùng dữ liệu đã bị can thiệp. Ghép CBC với HMAC cũng đạt được điều đó nhưng làm đúng
thứ tự (encrypt-then-MAC, so sánh theo thời gian hằng) là việc dễ sai — GCM cho sẵn.

**Chưa có khoá thì TỪ CHỐI lưu, không lưu bản rõ.** Một lỗi rõ ràng lúc cấu hình tốt hơn một
cơ sở dữ liệu đầy mật khẩu trần mà không ai biết.

Cột đặt tên `smtp_mat_khau_ma_hoa` chứ không phải `smtp_mat_khau`: người đọc schema sau này
biết ngay giá trị trong đó đã mã hoá, không tưởng là bản rõ rồi đi "sửa cho gọn".

## Một test đậu vì lý do sai — bắt được nhờ mutation testing

Test quan trọng nhất của FR-31 là *"API không bao giờ trả mật khẩu"*. Viết xong, 8/8 xanh
ngay lần chạy đầu. Xanh ngay lần đầu là lúc đáng nghi nhất, nên đem đi đột biến.

Mutant thứ nhất — thêm `SmtpMatKhauMaHoa` vào DTO trả về — **đi lọt**.

Lý do: test chỉ kiểm bản RÕ không có trong phản hồi. Nhưng thứ lưu trong DB là bản MÃ, nên
trả thẳng bản mã ra ngoài thì phép kiểm không thấy gì. Mà bản mã lọt ra ngoài **vẫn là rò
rỉ**: nó nằm lại trong cache trình duyệt, trong log proxy, và chỉ còn chờ khoá rò theo là
đọc được.

Test nay kiểm ba lớp: bản rõ, bản mã đọc thẳng từ DB, và **duyệt tên trường** để chặn cả
trường mới mà người thêm chưa nghĩ tới việc nó đi ra ngoài. Cờ `coMatKhau` được miễn trừ
**đích danh** chứ không theo mẫu tên — một trường kiểu `matKhauCu` thêm sau này vẫn bị bắt.

Cả 7 mutant sau đó đều chết đúng bởi test dự định: lưu bản rõ · xoá mật khẩu khi để trống ·
bỏ kiểm lần đầu · đọc chéo tenant · duyệt theo bảng thay vì enum · mọi mẫu báo tự gửi được.

## Ngoại lệ có chủ ý với quy tắc #1

Quy tắc #1 nói: lệnh cập nhật ghi đè trường nào thì trường đó phải có trong DTO **và** trong
form. Mật khẩu SMTP phá quy tắc đó — nó không hiển thị được nên form không thể gửi lại.

Cách xử lý: **để trống = giữ nguyên**, không phải xoá. Muốn xoá thì xoá cả cấu hình bằng một
lệnh riêng. Không có điều này, một lần sửa tên người gửi sẽ âm thầm xoá mật khẩu và email
ngừng đi mà không ai biết vì sao — đúng kiểu hỏng mà quy tắc #1 sinh ra để chặn.

Đã kiểm trên PostgreSQL thật, không chỉ in-memory: lưu mật khẩu → đọc cột thấy bản mã, bản
rõ không có trong DB; lưu lại với ô trống + đổi tên hiển thị → tên đổi, mật khẩu còn nguyên.

## Mẫu mặc định nằm trong mã, không seed xuống DB

Trung tâm chưa soạn thì **không có hàng nào** trong `MAU_EMAIL`; nội dung mặc định nằm ở
`MauMacDinh.cs`. Hai hệ quả tốt:

- Sửa mẫu mặc định là sửa mã, tự áp cho mọi trung tâm chưa soạn riêng — không cần migration
  đi cập nhật hàng loạt.
- Xoá mẫu đã soạn = **quay về mặc định**, không phải mất mẫu. Màn hình không bao giờ có ô trắng.

Danh sách mẫu duyệt theo **enum** chứ không theo bảng. Duyệt theo bảng thì loại chưa soạn
biến mất khỏi màn hình, và không ai biết nó tồn tại để mà soạn.

## Hai mẫu chưa gửi được — phải nói ra

`NhacNoHocPhi` và `NhacLichHoc` cần tác vụ nền chạy theo lịch, mà hệ thống chưa có. Chúng
vẫn hiện trên màn soạn nhưng mang cờ `tuGuiDuoc: false` và một dải cảnh báo vàng.

Giấu chúng đi thì gọn hơn, nhưng để trung tâm soạn xong rồi ngồi đợi một email không bao giờ
tới là tệ hơn nhiều — và không có gì trên màn hình nói cho họ biết vì sao.

## Soạn thảo: Tiptap, không phải CKEditor

Chủ dự án chọn *"ckeditor hoặc các thư viện hiện đại phổ biến khác"*. CKEditor 5 phát hành
kép GPL-2.0-or-later / thương mại — dự án này không mở mã theo GPL nên dùng bản miễn phí là
sai giấy phép, còn bản thương mại từ ~160 USD/tháng. Nêu ra, chủ dự án chốt **Tiptap** (MIT).

Thanh công cụ **cố ý chỉ có 6 nút**: đậm, nghiêng, H2, H3, hai loại danh sách, liên kết.
Đây là ô soạn **email**, không phải soạn trang web — Outlook trên Windows dựng HTML bằng
engine của Word: không flexbox, không grid, không `border-radius`, bảng mới là thứ chạy được.
Mỗi nút thừa là một cách để người dùng tạo ra email vỡ trên máy người nhận mà họ không bao
giờ nhìn thấy.

Cố ý **không có**: bảng (người dùng tự dựng bảng sẽ hỏng khung bao ngoài), ảnh (phải là URL
tuyệt đối công khai, mà ảnh lên MinIO thì API làm proxy sau đăng nhập ⇒ dán vào email sẽ
hiện ô vỡ), màu chữ và cỡ chữ (dễ tạo chữ không đọc được trên nền tối của trình đọc mail).

### Một bẫy nhỏ: `prose` không tồn tại

Viết đầu tiên dùng class `prose` của `@tailwindcss/typography`. Dự án **không cài** plugin
đó, mà Tailwind thì **im lặng bỏ qua** class nó không biết — tiêu đề và danh sách sẽ hiện ra
không định dạng gì và không có lỗi nào chỉ ra vì sao. Thay bằng `soan-thao.css` viết tay.

Cùng họ với bẫy mà `scripts/check-token-mau.py` canh: Tailwind hỏng theo kiểu không báo gì.

## Nối mẫu vào chỗ gửi: một cái làm được, một cái chưa

`TraLoiLienHe` **đã nối**: khách để lại form trên trang đích và có điền email thì nhận thư xác
nhận ngay. Gửi **sau khi đã lưu** và **nuốt lỗi có chủ ý** — liên hệ nằm trong sổ mới là phần
việc thật, email chỉ là phép lịch sự. Ném lỗi ở đó sẽ trả 500 cho khách sau khi dữ liệu ĐÃ
ghi, và họ gửi lại form ⇒ sinh bản ghi trùng.

Khác `QuenMatKhauCommand`: chỗ đó phải `Task.Run` để thời gian phản hồi không tố cáo email nào
có thật. Ở đây không có bí mật nào để rò — ai cũng gửi được form — nên `await` thẳng.

`ChaoMungHocVien` **chưa nối, và không nên nối vội**. Luồng tạo tài khoản hiện tại để quản trị
viên **tự gõ mật khẩu**, không sinh mật khẩu tạm, và handler không nhận email của học viên.
Muốn gửi được thư chào mừng thì phải đổi chính luồng đó — sinh mật khẩu ngẫu nhiên, lấy email
từ hồ sơ, quyết định gửi lúc nào. Đó là thay đổi nghiệp vụ riêng, không phải phần của FR-31.

### Một interface ở `Common/` thay vì gọi thẳng

Nơi gửi email nằm rải khắp: `Ldp/` trả lời liên hệ, `QuanTri/` cấp tài khoản, sau này job nền
ở CRM và LMS. Nếu mỗi chỗ `using ...Application.QuanTri.Email` thì `RanhGioiHeThongConTests`
bắt đúng — đó là LDP gọi sang module khác, thứ ADR-0005 dựng ra để chặn.

Mẫu email không thuộc hệ thống con nào, nó là hạ tầng gửi thư cùng loại với `IEmailSender`.
Nên đặt `IMauEmail` ở `Common/Interfaces/`, hiện thực ở `QuanTri/Email/`, và mỗi hệ thống con
chỉ thấy interface. Test ranh giới xanh, không phải khai ngoại lệ nào.

## Ba lỗi chỉ lộ ra khi gửi thật

Hỏi "dùng Gmail thì cấu hình thế nào" hoá ra lại là câu hỏi hay: nó buộc phải gửi thử thật,
và ba thứ hỏng lộ ra — không cái nào test in-memory bắt được.

**1. Cổng 465 không bao giờ gửi được.** `System.Net.Mail.SmtpClient` với `EnableSsl = true`
**chỉ làm STARTTLS**, không nói được SSL ngầm. Thử thật với Gmail: cổng 587 tới được bước xác
thực, cổng 465 trả `Syntax error, command unrecognized` — một thông báo không hề gợi ý nguyên
nhân là sai kiểu mã hoá. Nhiều hosting Việt Nam **chỉ** mở 465, nên đây không phải ca hiếm.

Đổi sang **MailKit 4.18.1** (MIT — thư viện Microsoft khuyến nghị thay `SmtpClient` ở code
mới), suy kiểu mã hoá từ số cổng: 465 → SSL ngầm, 587 → STARTTLS, còn lại → TLS nếu có.
Không thêm ô "chọn kiểu mã hoá" trên giao diện: số cổng đã quyết định điều đó, hỏi thêm chỉ
tạo cơ hội cho hai ô mâu thuẫn nhau.

**2. Sai mật khẩu chỉ báo `LOI_HE_THONG` 500.** Nút "Gửi thử" sinh ra để CHẨN ĐOÁN, mà lại
giấu đúng thông tin chẩn đoán. Tách ba mã: `SMTP_SAI_DANG_NHAP` · `SMTP_KHONG_KET_NOI_DUOC` ·
`SMTP_GUI_THAT_BAI`. Bản dịch của mã đầu nói thẳng về mật khẩu ứng dụng Gmail — vì đó là
nguyên nhân phổ biến nhất, và thông báo gốc của Gmail (`535 Username and Password not
accepted`) không nhắc gì tới nó.

Bắt **mọi** lỗi ở bước đăng nhập chứ không riêng `AuthenticationException`: thử thật thấy
cùng một mật khẩu sai mà Gmail lúc ném `AuthenticationException`, lúc ném
`SmtpProtocolException` (ngắt kết nối khi bị thử sai nhiều lần). Bắt hẹp theo kiểu thì nửa số
ca rơi xuống `LOI_HE_THONG` — đúng thứ đang tìm cách tránh.

**3. MailKit kiểm danh sách thu hồi chứng chỉ, và nó hỏng.** Ngay trên máy dev macOS: Gmail
báo `An incomplete certificate revocation check occurred` và đứt kết nối, dù cấu hình hoàn
toàn đúng. VPS sau firewall chặt cũng sẽ gặp y hệt. Tắt `CheckCertificateRevocation`.

Đánh đổi có cân nhắc: **chứng chỉ vẫn được xác thực đầy đủ** — đúng tên miền, đúng chuỗi tin
cậy, còn hạn — chỉ bỏ bước hỏi "có bị thu hồi sớm không". Thu hồi là sự kiện hiếm; mạng không
ra được máy chủ CRL là chuyện thường ngày. Đã ghi rõ trong mã: **không** được "sửa" thành
`ServerCertificateValidationCallback = () => true`, cái đó tắt xác thực hoàn toàn và mở đường
cho tấn công xen giữa.

### Trả lời câu hỏi gốc

**Phải thiết lập thủ công ở Gmail trước** — Google chặn đăng nhập SMTP bằng mật khẩu thường
từ 30/05/2022. Bật xác minh 2 bước → tạo mật khẩu ứng dụng → dán 16 ký tự vào hệ thống.

Ba bước đó nay in **ngay trên form**, kèm nút "Điền sẵn thông số Gmail". Giấu trong tài liệu
thì không ai đọc, và người dùng sẽ đi đổi mật khẩu Gmail (thứ vốn đúng).

## Còn lại

- Tác vụ nền theo lịch cho `NhacNoHocPhi` và `NhacLichHoc`.
- Thư chào mừng học viên — cần đổi luồng tạo tài khoản trước (xem trên).
