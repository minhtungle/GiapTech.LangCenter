# Module Học liệu (FR-11 → FR-13)

Bài tập giao trong buổi học, bài học viên nộp, và tài liệu giảng dạy.

## Tệp đính kèm — một bảng dùng chung

`TEP_DINH_KEM` có **năm cột FK nullable loại trừ nhau** (`bai_tap_id`, `bai_nop_id`,
`bai_kiem_tra_id`, `bai_lam_id`, `tai_lieu_id`), ép bằng `CHECK` constraint "đúng một khác null".

Không dùng mảng chuỗi/jsonb: chết ở chỗ dọn tệp mồ côi trong kho (phải quét mảng của năm
bảng), không mang được tên gốc/kích thước/MIME mà UI bắt buộc hiển thị, và mảng không có
`tenant_id` nên nằm ngoài Global Query Filter.

Không dùng năm bảng riêng: năm config gần giống hệt nhau, và job dọn rác phải UNION cả năm —
ai thêm loại thứ sáu mà quên sửa job thì tệp loại đó không bao giờ được dọn.

### Quy tắc về tệp

- **Khoá dùng GUID, không dùng tên gốc.** Tên người dùng đặt có thể chứa `../`, ký tự điều
  khiển, hoặc trùng nhau. Tên gốc lưu riêng ở DB để hiển thị và đặt tên lúc tải về.
- Whitelist: PDF, Word, Excel, PowerPoint, ZIP, text, ảnh, MP3. Tối đa **20 MB**.
  **Cố tình loại SVG và HTML** — cả hai chạy được script khi trình duyệt mở trực tiếp.
- Endpoint `/tep` gác bằng `ChucNang.Anh` vì nó dùng **chung** cho mọi module. Gác bằng quyền
  của một module cụ thể sẽ khiến người thiếu quyền đó không tải được tệp ở nơi khác — đúng cái
  bẫy đã gặp với endpoint đọc ảnh.

### ⚠️ Quyền trên TỆP không suy ra quyền trên NỘI DUNG

`Anh.Xoa` chỉ nói *"được xoá tệp"*, **không** nói *"xoá tệp nào"*. `XoaTepHandler` phải kiểm
chủ sở hữu:

| Loại tệp | Ai được gỡ |
|---|---|
| Bài nộp, bài làm | **Chính học viên đó** (không phải cả lớp) |
| Bài tập, bài kiểm tra | Người có quyền sửa lớp chứa nó |
| Tài liệu | Người sửa được tài liệu |

Thiếu kiểm này thì học viên gỡ được tệp trong bài nộp của bạn cùng lớp, chỉ cần đoán đúng id —
mà id nằm ngay trong danh sách bài nộp. Trả **404**, không phải 403: 403 xác nhận tệp tồn tại.

## FR-11 — Bài tập

Giao trong một buổi học cụ thể. Có tiêu đề, mô tả, hạn nộp (tuỳ chọn), tệp đính kèm.


### Chấm điểm: nhập cả bảng rồi lưu một lần

Bảng chấm cho nhập điểm và nhận xét cho **mọi bài nộp**, rồi bấm **Lưu điểm** một lần. Nút chỉ
bật khi có thay đổi, và hiện số bài đã sửa chưa lưu.

Bản trước gọi API ngay ở `onBlur` mỗi ô điểm. Khi hệ thống bắt đầu hỏi xác nhận trước mọi thao
tác ghi (07/09/2026), điều đó thành ra hỏi mỗi lần rời một ô — chấm lớp 20 học viên là 20 hộp
thoại. Gom lại vừa hợp với việc chấm cả lớp, chỉ hỏi một lần, và cho người chấm sửa lại trước
khi ghi.

Chỉ gửi lên **những dòng đã sửa**; dòng không đụng tới thì không gọi API. Gửi **tuần tự** chứ
không song song: mỗi lượt là một bản ghi nhật ký và một lần `SaveChanges`, bắn 20 request cùng
lúc chỉ để tiết kiệm vài trăm mili-giây là đánh đổi sai.

Phụ phẩm: nay có ô **nhận xét**. Trước đây `nhanXet` chỉ được gửi lại giá trị cũ nên giáo viên
không có đường nhập nó.

### Quy tắc
- **Xoá bài tập đã có học viên nộp bị chặn** (`BAI_TAP_DA_CO_BAI_NOP`) — bài nộp là kết quả
  học tập của họ (quy tắc #1).
- Xoá bài tập xoá luôn tệp đính kèm **cả trong kho lẫn DB** — không thì tệp mồ côi ở lại
  vĩnh viễn.
- Cascade từ buổi học: bài tập được giao *trong* buổi, buổi mất thì nó vô nghĩa. An toàn vì
  buổi đã điểm danh vốn đã bị `Restrict` chặn từ trước.

## FR-12 — Bài nộp

### Nộp nhiều lần, giữ lịch sử

`UNIQUE(bai_tap_id, hoc_vien_id, lan_nop)`. Mỗi lần nộp tạo một hàng mới với `LanNop` tăng dần.

Nhưng **danh sách cho giáo viên chỉ hiện lần mới nhất** của mỗi học viên — trả hết thì một
người ba dòng, giáo viên không biết chấm cái nào. Số lần nộp vẫn hiện để họ biết học viên đã
sửa mấy lần.

### Quy tắc

- Endpoint nộp bài **không nhận id học viên** — lấy từ token. Cùng lý do với tự điểm danh:
  không có tham số nào để nộp hộ người khác.
- Nộp sau `han_nop` tự đánh dấu `NopMuon`.
- **Chấm điểm là endpoint riêng** (`POST /bai-nop/{id}/cham`), command cố ý **không có trường
  nội dung**: giáo viên có quyền `Sua` trên bài nộp là để chấm, không phải để sửa bài của học
  viên. Không có tham số thì không có đường lạm dụng.

## FR-13 — Tài liệu

Tài liệu giảng dạy, phân loại (giáo trình / bài giảng / tham khảo / đề thi / khác).

### Quy tắc

- **Không gắn lớp nào = tài liệu chung** cho cả trung tâm. Biểu diễn bằng *không có hàng nào*
  trong `TAI_LIEU_LOP_HOC` — ở đây "danh sách rỗng" và "dùng chung" là **cùng một nghĩa**, nên
  không cần cờ phân biệt như trường hợp danh sách học viên của buổi.
- Người không xem được mọi lớp chỉ thấy: tài liệu chung + tài liệu của lớp họ liên quan. Không
  lọc thì giáo viên đọc được giáo trình lớp khác.
- Chỉ gắn được vào lớp mình có quyền sửa (`LOP_HOC_KHONG_HOP_LE`) — nếu không, một giáo viên
  đẩy được tài liệu vào lớp của người khác.

## Mã lỗi

| Mã | Khi nào |
|---|---|
| `LOAI_TEP_KHONG_HO_TRO` | Định dạng ngoài whitelist |
| `TEP_QUA_LON` | Vượt 20 MB |
| `TEP_RONG` | Tệp 0 byte |
| `BAI_TAP_DA_CO_BAI_NOP` | Xoá bài tập đã có học viên nộp |
| `KHONG_THUOC_LOP_NAY` | Nộp bài ở lớp mình không học |
| `LOP_HOC_KHONG_HOP_LE` | Gắn tài liệu vào lớp không có quyền |
| `LOAI_DINH_KEM_KHONG_HO_TRO` | Loại đối tượng đính kèm không hợp lệ |

## Chưa làm

- **Bài kiểm tra (`BAI_KIEM_TRA`, `BAI_LAM`)** — schema đã có, chưa có API và UI.
- **Trắc nghiệm tự chấm** — enum `LoaiBaiKiemTra.TracNghiemOnline` đã giữ sẵn chỗ.
- **Job dọn tệp mồ côi trong kho** — hiện chỉ xoá khi người dùng chủ động xoá đối tượng.
