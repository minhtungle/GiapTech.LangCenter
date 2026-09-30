# 30/09/2026 — Bài tập: một ô nộp cho mỗi buổi, bố cục kiểu Classroom

> Yêu cầu: *"phần bài tập có thể tham khảo theo classroom của google, phần bài tập bên trên
> kèm file, bên dưới là danh sách bài tập học sinh đã nộp chứ không cần ấn riêng"* — và khi
> được hỏi lại: *"không cần tách nhiều bài tập, giáo viên có thể tạo nhiều nội dung bài
> nhưng học sinh chỉ nộp bài tập chung 1 lần"*.

## Hỏi lại trước khi làm, và đó là việc đúng

Ba phương án bố cục tôi đưa ra ban đầu đều sai hướng — chủ dự án chọn "Khác" và mô tả một
mô hình **dữ liệu** chứ không phải một cách sắp xếp giao diện. Phải hỏi thêm một câu nữa
(phạm vi nào dùng chung ô nộp: buổi hay cả lớp) mới đủ để bắt tay làm.

Nếu cứ thế dựng theo phương án tôi đề xuất thì đã gộp giao diện xong mà mô hình vẫn sai, rồi
phải làm lại từ đầu kèm một migration thừa.

## `BAI_NOP` chuyển từ trỏ `BAI_TAP` sang trỏ `BUOI_HOC`

Giáo viên giao nhiều đầu việc trong một buổi — Writing task 1, task 2, ngữ pháp — nhưng học
viên làm xong thì nộp một lần, thường là một tệp chứa tất cả. Bắt nộp riêng từng đầu việc
nghĩa là cùng một tệp phải tải lên ba lần, và giáo viên chấm ba điểm cho một buổi rồi tự
cộng lại.

Hệ quả: **điểm và nhận xét là của BUỔI**, không phải của từng đầu việc.

## Ba chỗ suýt sai, bắt được nhờ chạy thật

**1. EF sinh cột bóng `BuoiHocId1`.** Tôi thêm `HasOne(x => x.BuoiHoc).WithMany(t => t.BaiTaps)`
trong khi đã có sẵn một `WithMany()` không tham số — EF coi đó là **hai** quan hệ và sinh
thêm một cột. Lỗi này chỉ hiện ra dưới dạng **một dòng cảnh báo** lúc tạo migration, không
làm đỏ build. Gỡ migration, gộp hai khai báo, tạo lại.

**2. Migration chỉ đổi tên cột thì dữ liệu trỏ sai bảng.** EF sinh `RenameColumn`, mà đổi tên
thì GIÁ TRỊ ở lại nguyên: cột `buoi_hoc_id` sẽ chứa id của `BAI_TAP`. Khoá ngoại mới sẽ từ
chối — hoặc tệ hơn, trùng id với một buổi có thật và dữ liệu âm thầm sai. Phải thêm `UPDATE`
tra ngược id buổi từ bài tập.

**3. Đặt `DropIndex` sai chỗ ⇒ migration chết giữa chừng.** Tôi viết bình luận "bỏ UNIQUE
trước khi đổi dữ liệu" rồi lại đặt `DropIndex` **sau** hai lệnh `UPDATE`. Ràng buộc cũ vẫn
hiệu lực trong lúc UPDATE chạy nên chặn ngay:
`23505: duplicate key value violates unique constraint`.

Bắt được vì đã **dựng bản sao DB dev rồi tạo đúng ca khó** — một học viên nộp cho hai đầu
việc khác nhau trong cùng một buổi. DB dev thật có 0 bài nộp nên chạy thẳng lên đó sẽ xanh
và lỗi chỉ nổ ra trên VPS.

Sau khi sửa: 2 bản ghi trùng được đánh số lại thành `lan_nop` 1 và 2 theo thời gian, **không
mất bản ghi nào**.

## Bỏ một chốt chặn có chủ ý

`BAI_TAP_DA_CO_BAI_NOP` chặn xoá bài tập đã có người nộp — đúng khi bài nộp gắn với bài tập
(xoá là mất bài của học viên, quy tắc #1). Nay bài nộp gắn với buổi, xoá một đầu việc không
đụng tới bài nộp nào.

Giữ lại sẽ thành: buổi có người nộp thì **mọi** đầu việc trong buổi bị khoá cứng, gõ nhầm một
chữ trong tiêu đề cũng không sửa được — một ràng buộc vô nghĩa mà người dùng không đoán được
lý do. Thay bằng test chiều ngược: *xoá đầu việc xong, bài nộp phải còn nguyên*.

## Bố cục

Đề bài + tệp ở trên, chỗ nộp / bảng theo dõi ngay bên dưới. Bỏ hẳn nút "Xem bài nộp" mở hộp
thoại riêng. Học viên nay có ô nộp bài ngay trên màn đó — trước kia không có đường nộp nào
trên giao diện.

Chỉ ở view **một buổi**. Ở view cả lớp thì "ai đã nộp" không có nghĩa: mỗi buổi một bảng
riêng, gộp lại thành hàng trăm dòng mà không trả lời được câu hỏi nào.

## Còn lại

- Thư chào mừng học viên (FR-31) — cần đổi luồng tạo tài khoản trước.
- Tác vụ nền nhắc nợ học phí / nhắc lịch học.
