# 18/09/2026 — Trạng thái buổi học · màu trạng thái · chấm tiêu chí riêng từng người

Ba yêu cầu trong một lượt, hoá ra dính vào nhau qua phân quyền.

## Yêu cầu

> - trạng thái buổi học chưa chuẩn, buổi đã qua vẫn hiện đã lên lịch. cần trạng thái đúng cho
>   buổi học. Quản lý lớp có thể chọn trạng thái cho buổi học (đã xong, chuyển lịch, mặc định
>   buổi chưa đến là chưa bắt đầu, buổi tới giờ là đang bắt đầu
> - Thêm màu sắc cho từng trạng thái khi hiển thị trên bảng và lịch để dễ nhận biết đúng
> - Phần đánh sao cho mức hài lòng cần thay bằng tiêu chí đánh giá cho giáo viên và trợ giảng
>   như đã quy định tịa HRM, bố trí lại giao diện phần nhận xét

## Đo trước khi sửa

```
trang_thai | tổng | đã qua | đang diễn ra | chưa đến
         0 |  129 |     85 |            0 |       44
```

**129/129 buổi đều `DaLenLich`, 85 buổi đã qua.** Chưa buổi nào được chốt bao giờ — nên "buổi đã
qua vẫn hiện đã lên lịch" không phải cảm nhận, nó đúng với mọi buổi trong hệ thống.

Cũng đo luôn phần thứ ba: `hv1` gọi `/tieu-chi-danh-gia` → **403**. Frontend có `catch` trả danh
sách rỗng, nên phiếu **âm thầm** rơi về chấm sao. Tính năng "chấm theo tiêu chí" đã viết từ
16/09 nhưng chưa bao giờ chạy được với học viên thật.

## Ba quyết định hỏi chủ sản phẩm

Cả ba đều là chuyện không suy ra được từ code:

| Câu hỏi | Chốt |
|---|---|
| "chưa bắt đầu"/"đang bắt đầu" — suy theo giờ hay lưu DB? | **suy theo giờ** |
| Mở quyền đọc tiêu chí cho học viên thế nào? | **cho đọc danh mục** |
| Chấm GV và trợ giảng chung phiếu hay riêng? | **riêng từng người** |

Câu thứ ba đảo một quyết định đã ghi trong enum `NhomTieuChi` ngày 16/09 ("dùng chung cho giáo
viên và trợ giảng"). Đáng đảo: chấm chung thì xếp hạng trợ giảng ở FR-29 thực chất là điểm của
giáo viên — trợ giảng giỏi trong lớp có giáo viên bị chấm thấp sẽ chịu oan.

## Trạng thái: hai lớp khái niệm

`BUOI_HOC.trang_thai` chỉ lưu thứ **con người quyết định**; phần còn lại suy từ giờ.

| | Ai quyết | Giá trị |
|---|---|---|
| `TrangThaiBuoiHoc` (DB) | con người | `DaLenLich` · `DaHoanThanh` · `ChuyenLich` (mới) · `DaHuy` |
| `TinhTrangBuoiHoc` (hiển thị) | suy ra | `ChuaBatDau` · `DangDienRa` · `ChuaChot` · `DaXong` · `ChuyenLich` · `DaHuy` |

Luật: **trạng thái người đặt luôn thắng giờ** (buổi đã huỷ không bao giờ hiện "đang diễn ra" dù
đang trong khung giờ — người dùng sẽ vào một lớp trống). Chỉ khi còn `DaLenLich` thì giờ mới
quyết định, và buổi quá giờ ra **`ChuaChot`** — gọi thẳng là *việc còn phải làm*, không gọi là
"đã qua".

Vì sao suy chứ không lưu: lưu thì cần job chạy nền đổi trạng thái theo giờ, job chết là trạng
thái đứng im và **sai âm thầm**. Suy thì không có gì để hỏng.

Hai bản sao logic (backend + frontend) là có chủ ý: màn lịch mở cả buổi sáng, buổi 9h phải tự
chuyển trạng thái mà không chờ tải lại. Hai bộ test cố tình lặp lại **cùng bộ ca**.

## Bốn lỗi tìm ra trong lúc làm

### 1. Kết quả mutation test đầu vô nghĩa vì tôi đọc thiếu

Test "trạng thái buổi" đỏ với thông điệp `Expected ChuaBatDau, Actual DangDienRa`. Tôi tưởng
logic sai. Đọc helper `DungLopCoLich` mới thấy nó **cố ý đẩy buổi 1 về `UtcNow.AddMinutes(-30)`**
— buổi đang diễn ra. API đúng, **kỳ vọng của tôi sai**.

Sửa thành ba test ở ba mốc (quá khứ / hiện tại / tương lai) — rộng hơn ý định ban đầu, và ca
"quá khứ → ChuaChot" chính là ca lỗi được báo.

### 2. Cấp quyền đọc tiêu chí làm học viên MẤT MENU LỚP HỌC

Nặng nhất, và **không có test đơn vị nào bắt được**: E2E `doi-nick-khong-giu-quyen-cu` (viết
hôm qua cho việc khác) đỏ với *"học viên vẫn phải thấy mục của mình"* — menu chỉ còn "Tổng quan".

Gốc rễ: `TieuChiDanhGia` thuộc **HRM**. Vừa cấp ô đó cho nhóm "Học viên" là `/toi/he-thong` trả
`["Hrm","Lms"]`, `Layout` chọn HRM và học viên nhìn vào sidebar nhân sự. Kiểm lại trên hệ thống
thật: đúng như vậy.

Chữa bằng `ChucNang.MoLoiVaoHeThong` — danh sách **hẹp** các cặp (chức năng, thao tác) không mở
lối vào hệ thống của chúng, cùng tinh thần với `DungChung`.

Bài học: một ô quyền có thể đổi **cả sidebar**. tsc và lint không biết gì về chuyện đó.

### 3. Lịch hiện sai giờ — tìm ra bằng mắt, không bằng test

Soi ảnh chụp màn hình thì thấy buổi 18:00 hiện **"11 giờ"**. Không test nào đỏ, và bảng danh
sách ngay cạnh hiện đúng 18:00.

FullCalendar bản không có plugin múi giờ chỉ hiểu `'local'`/`'UTC'`; đưa tên IANA
(`Asia/Ho_Chi_Minh`) thì nó **âm thầm rơi về UTC**. Chú thích trong code khẳng định `timeZone`
hoạt động — chú thích sai.

Kiểm chứng đây là lỗi **có từ trước**: `git stash` rồi chạy lại trên bản gốc, vẫn "11 giờ". Chữa
bằng quy đổi sang giờ treo tường trước khi đưa cho lịch, thay vì thêm phụ thuộc chỉ để định dạng.

### 4. Một mutant sống, và nó chỉ đúng một phần

Mutant "loại cả `TieuChiDanhGia` khỏi mọi thao tác" (chữa quá tay) **sống**. Điều tra: trong dữ
liệu hiện tại chỉ quản trị giữ `TieuChiDanhGia.Xem`, mà quản trị còn 16 quyền HRM khác che đi —
khác biệt **không quan sát được**.

Nhưng nó sẽ quan sát được với một vai trò "chỉ phụ trách danh mục tiêu chí". Thêm
`MoLoiVaoHeThongTests` kiểm trực tiếp cái LUẬT thay vì hệ quả: `TuLam` không mở lối, nhưng
`Xem`/`Them`/`Sua` **vẫn** mở. Mutant chết bởi 4 assertion.

## Kiểm chứng trên tenant thật

Gán "Thầy Hoà (trợ giảng)" vào lớp *IELTS 6.5 — K1* (kiểm luôn quy tắc #1: P101 và sức chứa 20
còn nguyên sau lệnh sửa), rồi `hv1` chấm lệch hẳn:

| Người | Truyền đạt | Nhiệt tình | Điểm FR-29 |
|---|---|---|---|
| Cô Lan | 5/5 | 5/5 | **5.0** |
| Thầy Hoà (trợ giảng) | 3/5 | 2/5 | **3.33** |

Trước thay đổi, cả hai đều nhận **3.0** (trung bình trộn). Số 5.0 và 3.33 khớp phép tính tay khi
tính cả điểm "chấm chung" cũ của buổi 2 — tức dữ liệu trước 18/09 vẫn được tính, không mất
(quy tắc #1).

## Mutation test

| Mutant | Kết quả |
|---|---|
| `<=` → `<` ở biên kết thúc (BE + FE) | chết |
| Bỏ nhánh `DaHuy` khỏi luật suy (BE + FE) | chết |
| `ChuaChot` → `ChuaBatDau` (lỗi gốc quay lại) | chết |
| Bỏ lọc theo người được chấm ở thống kê | chết — cả hai ra 3.0 |
| Bỏ ngoại lệ `TieuChiDanhGia.TuLam` | chết |
| Loại cả `TieuChiDanhGia` (chữa quá tay) | **sống** → thêm test, rồi chết |
| Ẩn mục "Đổi trạng thái" | chết |
| Chỉ hiện khối giáo viên, bỏ trợ giảng | chết |

581 test backend · 33 vitest · 42 E2E xanh.

---

## Bổ sung cuối ngày — "lưu điểm danh không lưu được"

Chủ sản phẩm báo tiếp. Tái hiện ngay trên nick `co.lan` vừa cấp: lớp K1 có 6 học viên, mặc định
ai cũng `Vắng` với ô lý do trống, đổi 2 người sang Có mặt rồi bấm Lưu → **400**.

### Lỗi thật không nằm ở validator

Validator bắt vắng phải có lý do là **đúng và có chủ ý**. Lỗi nằm ở chỗ màn hình **không nói
được điều đó**: API trả lỗi theo từng dòng trong `duLieu.truong`
(`{"DanhSach[2]":["THIEU_LY_DO_VANG"], ...}`), nhưng `layMaLoi` chỉ đọc `errorCode` ở tầng ngoài
nên người dùng thấy đúng một câu *"Dữ liệu nhập vào chưa hợp lệ"*.

Mã `THIEU_LY_DO_VANG` **đã có bản dịch sẵn** trong `i18n.ts` từ trước — chỉ là chưa bao giờ tới
được mắt người dùng. Dự án cũng đã có `layDuLieuLoi` cho đúng việc này.

Nên với người dùng nó là *"lỗi không lưu được"*, không phải *"thiếu lý do vắng"*. Hai câu đó
dẫn tới hai hành động khác nhau.

### Hai lần tự bẫy mình khi thăm dò

1. Gọi API tay bằng `dongs` và `trangThaiChinhThuc` → 400. Tưởng đã thấy lỗi, thực ra là tôi gửi
   sai tên trường. Đọc `GhiDiemDanhCommand` mới biết là `danhSach` / `trangThai`; gửi đúng thì
   **204**. Nếu dừng ở bước đó thì đã đi sửa backend đang chạy tốt.
2. Script thăm dò bấm Lưu rồi **không trả lời hộp xác nhận**, nên không có request nào bay đi —
   đúng triệu chứng "không lưu được" nhưng vì lý do khác hẳn. Ảnh chụp màn hình lộ ra hộp thoại
   còn mở, và lộ luôn 4 ô lý do trống — mới là nguyên nhân thật.

Cả hai lần đều được ảnh chụp/đọc hợp đồng API gỡ ra, không phải suy luận.

### Còn một lần tự bẫy nữa, ở locator

Điền lý do bằng `nth(i)` rồi timeout: điền xong thì `aria-invalid` mất, locator co lại và
`nth(3)` biến mất **giữa vòng lặp**. `.all()` cũng vậy vì vẫn theo chỉ số. Phải chốt danh sách
`aria-label` trước rồi nhắm theo tên — đã ghi lại cách này trong test để người sau không mất
thời gian.

### 22 test đỏ vì tôi cấu hình sai, không phải regression

Chạy cả bộ E2E thì **22/43 đỏ**, rải rác ở những test không liên quan. Tôi đã khởi động lại API
với hạn mức tần suất **BẬT** (đúng cho dùng thường), mà cả bộ E2E tạo hàng chục trung tâm nên
đụng hạn mức — CLAUDE.md đã ghi rõ phải tắt. Khởi động lại với `GIOI_HAN_TAN_SUAT=false`: **43/43
xanh**.

Bài học lặp lại của ngày hôm qua: test đỏ phải đọc *lý do* đỏ. Lần này triệu chứng còn dễ quy oan
hơn vì nó đỏ ở 22 chỗ cùng lúc, rất giống "vừa làm hỏng gì đó to".

### Mutation test

| Mutant | Kết quả |
|---|---|
| Bỏ dòng cảnh báo (quay về lỗi gốc) | chết |
| Bỏ khoá nút (cho bấm rồi nhận 400) | chết |
| Khoá nút vĩnh viễn (chữa quá tay) | chết |

Ca thứ ba là lý do test phải kiểm **cả hai chiều** — chỉ kiểm "bị chặn khi thiếu" thì một bản sửa
khoá nút vĩnh viễn cũng xanh, và người dùng mất hẳn tính năng lưu.
